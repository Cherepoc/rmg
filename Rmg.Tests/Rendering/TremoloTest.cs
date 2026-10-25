using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A riff's or a lead's long notes tremolo-picked (<see cref="Articulations.Tremolo" />), a genre's, at its speed.</summary>
public sealed class TremoloTest
{
    /// <summary>
    ///     In an appearance a part tremolo-picks in, every note of its plucked instrument a beat or longer is picked, but one it
    ///     mutes, bends or ornaments, its strokes no quicker than a picker's hand but at an 8th, a fast genre's at the finest
    ///     step the tempo allows, a measured one's at an 8th or a 16th.
    /// </summary>
    [Test]
    [Arguments("blackMetal", true)]
    [Arguments("thrash", false)]
    public async Task APickingAppearancesNotes_AreTremoloPicked_NoQuickerThanTheHand(string genre, bool fast)
    {
        var picking = GenrePresets.With(genre, GenreChoices.Articulation, ("tremolo", 1));
        var (sections, strokes, rates) = (0, 0, new List<double>());
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: picking))))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Tremolo))
        {
            sections++;
            var definition = (PitchInstrumentTrack)song.Song.TrackDefinitions[entry.Track];
            var (index, _) = ((int, double))entry.Value!;
            // but in stop time, its phrases, and in a bar of ensemble hits, whose hits a tremolo leaves struck (Articulations.PicksOutOf)
            var stopped = song.Trace.Where(x => x.Point == TracePoints.StopTime).SelectMany(x => (ImmutableArray<double>)x.Value!).ToArray();
            var hits = song.Trace.Where(x => x.Point == TracePoints.EnsembleHits).SelectMany(x => (ImmutableArray<double>)x.Value!).ToArray();
            bool InStopTime(double at) => song.Map.Sections.Any(s => stopped.Any(x => Math.Abs(x - s.Start) < 1e-6) && at >= s.Start - 1e-9 && at < s.Start + s.Meter.PhraseDuration - 1e-9)
                                          || song.Map.Sections.Any(s => hits.Any(x => at >= x - 1e-9 && at < x + s.Meter.BarDuration - 1e-9 && s.IsWithin(x)));
            var notes = song.Song.Notes!.GetValueOrDefault(entry.Track)?.Where(x => song.Map.PlayingAt(x.Position)?.Index == index).ToArray() ?? [];
            // (but its last stroke where it holds on past its appearance's end, and a solo's)
            foreach (var note in notes.Where(x => !InStopTime(x.Position) && InstrumentTraits.Has(InstrumentsOverTime.ProgramOf(x.Value.State, definition.InstrumentCode), InstrumentTrait.Strums)
                                                  && x.Value.State.GetStateValue(CompositionStateKinds.PalmMute) == 0 && x.Value.State.GetStateValue(CompositionStateKinds.Bend) == 0
                                                  && x.Value.State.GetStateValue(CompositionStateKinds.Ornament).IsDefaultOrEmpty && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0
                                                  && !(x.Value.State.GetStateValue(CompositionStateKinds.Picked) > 0 && x.Position + x.Value.Duration > song.Map.PlayingAt(x.Position)!.Value.Span.End - 1e-9)))
                // as long as it sounds before the part's next note, which the hand moves to
                await Assert.That(Math.Min(note.Value.Duration, notes.Where(x => x.Position > note.Position + 1e-9).Select(x => x.Position - note.Position).DefaultIfEmpty(double.MaxValue).Min()))
                    .IsLessThan(Articulations.TremoloStrokes * Articulations.TremoloSteps[0]).Because($"seed {song.Seed}, track {entry.Track} at {note.Position}");
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

    /// <summary>
    ///     A held line's note runs on to its next within its phrase; one stopping at a change of chord leaves the next note,
    ///     of the chord there, struck at the change; a phrase's end and a stretch held silent stop it, the next phrase after a
    ///     breath.
    /// </summary>
    [Test]
    public async Task AHeldLine_RunsOnToItsNextNote_StrikingItAtAChangeItWaitsAfter()
    {
        var held = Rmg.Core.Events.StateMap.FromStates([CompositionStateKinds.Articulation.CreateState(new Rmg.Core.Events.LayerValue<ArticulationChoice>(Rmg.Core.Events.StateDepths.Note, new(PartArticulation.Holds, 0.25)))]);
        Rmg.Core.Events.TimelineItem<RealizedNote> Note(double at) => new(at, new RealizedNote([40], 0.8, 0.5, held));
        // phrases of four beats, a change of chord at 3, the part held silent from 13
        var played = Articulations.Held(Rmg.Core.Events.EventTimeline.Create(16, [Note(0), Note(1), Note(2), Note(3.5), Note(5), Note(8), Note(12.5), Note(14)]), x => x.State.GetStateValue(CompositionStateKinds.Articulation).Value.Articulates.HasFlag(PartArticulation.Holds), [3],
            position => Math.Floor(position / 4) * 4 + 4, position => position < 13 ? 13 : 16).Select(x => (x.Position, x.Value.Duration)).ToArray();
        await Assert.That(played).IsEquivalentTo(new[] { (0.0, 1.0), (1.0, 1.0), (2.0, 1.0), (3.0, 1.0), (5.0, 3.0), (8.0, 4.0), (12.5, 0.5), (14.0, 2.0) });
    }
}
