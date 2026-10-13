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
            var step = Rmg.Core.Composition.Realizer.GetChord(note.Value.State).Chord.GetPitch(SectionGenerator.Boogie[place % SectionGenerator.Boogie.Length]);
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
        await Assert.That(onStep / (double)strong).IsGreaterThan(0.7);
        await Assert.That(onStep / (double)strong).IsGreaterThan(2 * plainOnStep / (double)plainStrong);
    }
}
