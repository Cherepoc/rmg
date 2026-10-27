using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Bass;

/// <summary>A boogie: the bass's strong notes climbing its chord's root, third, fifth, sixth and seventh and back while the chord lasts.</summary>
public sealed class BoogieTest
{
    private static Genre With(double chance) => GenrePresets.With("blues", GenreChoices.AlternatingBass, ("sections", 0), ("boogie", chance));

    /// <summary>The bass's strong notes after a chord's first in a song's boogieing sections or the others: how many, and how many on the boogie's step.</summary>
    private static (int OnStep, int Strong) Measure(CorpusSong song, bool boogie)
    {
        var sections = song.Trace.Where(t => t.Point == TracePoints.Boogie && (bool)t.Value! == boogie).Select(t => t.Section).ToHashSet();
        var bass = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Bass).SelectMany(x => x.Value).ToArray();
        var (onStep, strong, place) = (0, 0, 0);
        TimelineItem<RealizedNote>? first = null;
        foreach (var note in bass.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.BeatRank) == 0))
        {
            var sameChord = first is { } f && f.Value.State.GetStateValue(StateKinds.ChordChange) == note.Value.State.GetStateValue(StateKinds.ChordChange)
                                           && song.Map.SectionAt(f.Position) == song.Map.SectionAt(note.Position);
            if (!sameChord)
                (first, place) = (note, 0);
            else
                place++;
            if (place == 0 || !sections.Contains(song.Map.SectionAt(note.Position)?.SectionId ?? -1) || note.Value.Pitches.Length != 1)
                continue;
            strong++;
            // its sixth and seventh the major sixth and the flat seventh over the root, as the boogie plays them
            var chord = Rmg.Core.Composition.Realizer.GetChord(note.Value.State).Chord;
            var step = SectionGenerator.Boogie[place % SectionGenerator.Boogie.Length] switch { 5 => chord.GetPitch(0) + 9, 6 => chord.GetPitch(0) + 10, var other => chord.GetPitch(other) };
            onStep += ((note.Value.Pitches[0] - step) % 12 + 12) % 12 == 0 ? 1 : 0;
        }

        return (onStep, strong);
    }

    /// <summary>In a section that walks a boogie, the bass's strong notes after a chord's first sound the boogie's step, far more than in one that does not.</summary>
    [Test]
    public async Task ABoogie_ClimbsItsChordsStepsAndBack_OnItsStrongNotes()
    {
        (int OnStep, int Strong) Sum(double chance, bool boogie) => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With(chance))))
            .Select(x => Measure(x, boogie)).Aggregate((0, 0), (a, b) => (a.Item1 + b.OnStep, a.Item2 + b.Strong));
        var (onStep, strong) = Sum(1, true);
        var (plainOnStep, plainStrong) = Sum(0, false);
        Console.WriteLine($"the boogie's step on {onStep} of {strong} strong notes after a chord's first, against {plainOnStep} of {plainStrong} where none walks one");
        await Assert.That(strong).IsGreaterThan(50);
        await Assert.That(plainStrong).IsGreaterThan(50);
        // (measured over 64: 94% against 14%)
        await Assert.That(onStep / (double)strong).IsGreaterThan(0.7);
        await Assert.That(onStep / (double)strong).IsGreaterThan(2 * plainOnStep / (double)plainStrong);
    }

    /// <summary>
    ///     A boogie's third and fifth are its chord's own, as the band's chord holds them: a sus chord's fourth or second, a
    ///     power chord's fifth, never the third the chord leaves out; where no chord sounds, the scale's.
    /// </summary>
    [Test]
    [Arguments("blues")]
    [Arguments("rock")]
    public async Task ABoogiesThirdAndFifth_AreNotesOfItsChord(string preset)
    {
        var (steps, unlike) = (0, 0);
        // the bass's own line, not one doubling another's, nor a pedal, which the boogie leaves as they are
        var genre = GenrePresets.With(preset, GenreChoices.AlternatingBass, ("sections", 0), ("boogie", 1));
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            var sections = song.Trace.Where(t => t.Point == TracePoints.Boogie && (bool)t.Value!).Select(t => t.Section).ToHashSet();
            var bass = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Bass).SelectMany(x => x.Value).ToArray();
            var place = 0;
            TimelineItem<RealizedNote>? first = null;
            foreach (var note in bass.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.BeatRank) == 0))
            {
                var sameChord = first is { } f && f.Value.State.GetStateValue(StateKinds.ChordChange) == note.Value.State.GetStateValue(StateKinds.ChordChange)
                                               && song.Map.SectionAt(f.Position) == song.Map.SectionAt(note.Position);
                (first, place) = sameChord ? (first, place + 1) : (note, 0);
                if (place == 0 || !sections.Contains(song.Map.SectionAt(note.Position)?.SectionId ?? -1) || note.Value.Pitches.Length != 1
                    || note.Value.State.GetStateValue(CompositionStateKinds.BassPedal) != 0 || note.Value.State.GetStateValue(CompositionStateKinds.Doubled) > 0
                    || note.Value.State.GetStateValue(CompositionStateKinds.LineSolo) != 0 || SectionGenerator.Boogie[place % SectionGenerator.Boogie.Length] is not (2 or 4))
                    continue;
                var (chord, _, classes) = Rmg.Core.Composition.Realizer.GetChordNotes(note.Value.State);
                // a chord of its root alone, where none sounds, or of none, the scale's; and a note a push moved off its beat ahead of the
                // next chord
                var start = song.Map.SectionAt(note.Position)!.Start;
                if (classes.Count == 1 || !classes.Contains(chord.Root.Mod(12)) || Math.Abs(Math.IEEERemainder(note.Position - start, 1)) > 1e-6)
                    continue;
                steps++;
                unlike += classes.Contains(chord.GetPitch(2).Mod(12)) ? 0 : 1;
                await Assert.That(classes.Contains(note.Value.Pitches[0].Mod(12))).IsTrue().Because($"seed {song.Seed} at {note.Position}");
            }
        }

        Console.WriteLine($"{preset}: {steps} thirds and fifths of a boogie, {unlike} over a chord without the scale's third");
        await Assert.That(steps).IsGreaterThan(50);
    }
}
