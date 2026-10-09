using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Chords;

/// <summary>
///     A passing chord: the next chord's dominant, the next a semitone above or below, its tritone substitute or a
///     half-diminished chord a semitone below it, over the second half of a chord a bar long, never in a plain song.
/// </summary>
public sealed class PassingChordTest
{
    /// <summary>Every section's chords as it records them: where they start, their roots, and which of them pass.</summary>
    private static IEnumerable<(HarmonicRhythm Rhythm, ImmutableArray<int> Progression)> Sections(CorpusSong song)
    {
        // joined by their section, as each records its own
        var progressions = song.Trace.Where(x => x.Point == TracePoints.Progression).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => (ImmutableArray<int>)x.Last().Value!);
        return song.Trace.Where(x => x.Point == TracePoints.HarmonicRhythm).GroupBy(x => x.Section).Select(x => ((HarmonicRhythm)x.Last().Value!, progressions[x.Key]));
    }

    [Test]
    public async Task APassingChord_IsTheNextsDominant_OrTheNextASemitoneAbove_HalfwayThroughAChordABarLong()
    {
        var passing = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("jazz")))))
        foreach (var (rhythm, progression) in Sections(song))
        {
            await Assert.That(progression.Length).IsEqualTo(rhythm.Count);
            foreach (var at in rhythm.Passing)
            {
                passing++;
                var chord = rhythm.Changes.IndexOf(at);
                await Assert.That(((progression[chord] - progression[chord + 1] - 4) % 7 + 7) % 7 == 0 || progression[chord] == progression[chord + 1]).IsTrue();
                await Assert.That(rhythm.LengthOf(chord - 1) + rhythm.LengthOf(chord)).IsGreaterThanOrEqualTo(rhythm.Meter.BarDuration - 1e-9);
            }
        }

        await Assert.That(passing).IsGreaterThan(10);
    }

    [Test]
    public async Task AChromaticPassingChord_SoundsTheNextASemitoneAboveOrBelow_AHalfDiminishedOneOnlyBelow()
    {
        var wild = new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Progression, 0.9), Genre: GenrePresets.Genre("jazz"));
        var (above, below) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, wild)))
        {
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            // a chord as a pitched note struck where it starts has it, its section's home among its state; none where none is
            var struck = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack).SelectMany(x => x.Value)
                .GroupBy(x => Math.Round(x.Position, 6)).ToDictionary(x => x.Key, x => x.First().Value.State);
            ChordContext? Chord(double position) => struck.TryGetValue(Math.Round(position, 6), out var state) ? Rmg.Core.Composition.Realizer.GetChord(state).Chord : null;
            // but where a section planes, its chords moved apart by semitones of their own
            var planing = song.Trace.Where(x => x.Point == TracePoints.Planing && x.Value is not null).Select(x => x.Section).ToHashSet();
            var changes = song.ChordChanges;
            for (var i = 0; i + 1 < changes.Length; i++)
            {
                var shift = common.GetEffectiveStateMapAt(changes[i]).GetStateValue(Rmg.Core.Events.StateKinds.ChordShift);
                if (shift is not (1 or -1) || planing.Contains(song.Song.Map!.SectionAt(changes[i])?.SectionId ?? -1))
                    continue;
                if (Chord(changes[i]) is not { } chord || Chord(changes[i + 1]) is not { } next)
                    continue;
                (above, below) = shift == 1 ? (above + 1, below) : (above, below + 1);
                await Assert.That(((chord.GetPitch(0) - next.GetPitch(0)) % 12 + 12) % 12).IsEqualTo((shift % 12 + 12) % 12).Because($"seed {song.Seed} at {changes[i]}");
                // a diminished fifth over its root a semitone below alone
                if (((chord.GetPitch(4) - chord.GetPitch(0)) % 12 + 12) % 12 == 6)
                    await Assert.That(shift).IsEqualTo(-1).Because($"seed {song.Seed} at {changes[i]}");
            }
        }

        Console.WriteLine($"passing a semitone above {above}, below {below}");
        await Assert.That(above).IsGreaterThan(5);
        await Assert.That(below).IsGreaterThan(5);
    }

    [Test]
    public async Task ThePlainestSongs_PassNoChord()
    {
        var plain = new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Progression, 0), Genre: GenrePresets.Genre("jazz"));
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, plain)))
            await Assert.That(Sections(song).All(x => x.Rhythm.Passing.IsEmpty)).IsTrue();
    }
}
