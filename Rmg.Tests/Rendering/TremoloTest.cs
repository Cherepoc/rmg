using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A riff's or a lead's long notes tremolo-picked (<see cref="Articulations.Tremolo" />), a genre's, at its speed.</summary>
public sealed class TremoloTest
{
    /// <summary>
    ///     In a section a part tremolo-picks in, every note of its plucked instrument a beat or longer is picked, but one it
    ///     mutes, bends or ornaments, its strokes no quicker than a picker's hand but at an 8th, a fast genre's at the finest
    ///     step the tempo allows, a measured one's at an 8th or a 16th.
    /// </summary>
    [Test]
    [Arguments("blackMetal", true)]
    [Arguments("thrash", false)]
    public async Task APickingSectionsLongNotes_AreTremoloPicked_NoQuickerThanTheHand(string genre, bool fast)
    {
        var picking = GenrePresets.With(genre, GenreChoices.Articulation, ("tremolo", 1));
        var (sections, strokes, rates) = (0, 0, new List<double>());
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: picking))))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Tremolo))
        {
            sections++;
            var definition = (PitchInstrumentTrack)song.Song.TrackDefinitions[entry.Track];
            var notes = song.Song.Notes!.GetValueOrDefault(entry.Track)?.Where(x => song.Map.PlayingAt(x.Position)?.Span.SectionId == entry.Section).ToArray() ?? [];
            foreach (var note in notes.Where(x => InstrumentTraits.Has(InstrumentsOverTime.ProgramOf(x.Value.State, definition.InstrumentCode), InstrumentTrait.Strums)
                                                  && x.Value.State.GetStateValue(CompositionStateKinds.PalmMute) == 0 && x.Value.State.GetStateValue(CompositionStateKinds.Bend) == 0
                                                  && x.Value.State.GetStateValue(CompositionStateKinds.Ornament).IsDefaultOrEmpty))
                await Assert.That(note.Value.Duration).IsLessThan(Articulations.TremoloFrom).Because($"seed {song.Seed}, track {entry.Track} at {note.Position}");
            // the strokes, each a step after the one before, no quicker than the hand but at a 16th, a fast
            // picker's never slower than it could, a measured one's 16ths
            // (a note's strokes among those of another struck with it apart, by their pitches)
            foreach (var (a, b) in notes.GroupBy(x => string.Join(",", x.Value.Pitches)).SelectMany(x => x.Zip(x.Skip(1)))
                         .Where(x => x.First.Value.State.GetStateValue(CompositionStateKinds.Picked) is var stroke and > 0 && x.Second.Value.State.GetStateValue(CompositionStateKinds.Picked) == stroke + 1))
            {
                strokes++;
                var step = b.Position - a.Position;
                var perSecond = 1 / (song.Rendered.SecondsAt(b.Position) - song.Rendered.SecondsAt(a.Position));
                rates.Add(perSecond);
                await Assert.That(Articulations.TremoloSteps.Take(fast ? 4 : 2).Any(s => Math.Abs(step - s) < 1e-6)).IsTrue().Because($"seed {song.Seed} at {a.Position}");
                // no quicker than the hand, but at an 8th, at a tempo too fast for 16ths
                if (step < Articulations.TremoloSteps[0] - 1e-6)
                    await Assert.That(perSecond).IsLessThanOrEqualTo(Articulations.FastStrokes + 0.01).Because($"seed {song.Seed} at {a.Position}");
            }
        }

        rates.Sort();
        Console.WriteLine($"{genre}: {sections} sections picking, {strokes} strokes, a median {rates.ElementAtOrDefault(rates.Count / 2):F1} a second, from {rates.FirstOrDefault():F1} to {rates.LastOrDefault():F1}");
        await Assert.That(sections).IsGreaterThan(3);
        await Assert.That(strokes).IsGreaterThan(100);
    }

    /// <summary>The plainest of the songs pick no tremolo, the songs' own chance of it none.</summary>
    [Test]
    public async Task ThePlainestSongs_PickNoTremolo()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0))))
            await Assert.That(song.Trace.Any(x => x.Point == TracePoints.Tremolo)).IsFalse().Because($"seed {song.Seed}");
    }
}
