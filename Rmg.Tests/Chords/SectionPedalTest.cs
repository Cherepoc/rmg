using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A pedal under a section: the bass holding one note, its home or its dominant, under the changing chords of its first phrase, or of its last for the dominant, none under chords that never change.</summary>
public sealed class SectionPedalTest
{
    [Test]
    public async Task APedalledPhrase_HoldsOneNoteInTheBass_UnderChangingChords()
    {
        int phrases = 0, held = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed,
                     new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Progression, 0.9))))))
        {
            var pedalling = song.Trace.Where(x => x.Point == TracePoints.SectionPedal && x.Value is not null).ToDictionary(x => x.Section, x => (int)x.Value!);
            var bass = song.Song.Notes![song.Song.Track(TrackRole.Bass)];
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            foreach (var span in song.Song.Map!.Sections.Where(x => pedalling.ContainsKey(x.SectionId)))
            {
                var phrase = span.Meter.PhraseDuration;
                // the first phrase, or the last, counted from the section's start, as the stage takes them
                var (start, end) = pedalling[span.SectionId] == 0 ? (span.Start, Math.Min(span.End, span.Start + phrase)) : (span.Phrases.Last().Start, span.End);
                if (!ChordsChange(common, start, end))
                    continue;
                var classes = bass.Where(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9).Select(x => x.Value.Pitches[0].Mod(12)).ToArray();
                if (classes.Length == 0)
                    continue;
                phrases++;
                // one note held, but where the band pushes into the next phrase
                if (classes.GroupBy(x => x).Max(x => x.Count()) >= 0.9 * classes.Length)
                    held++;
            }
        }

        await Assert.That(phrases).IsGreaterThan(5);
        await Assert.That(held / (double)phrases).IsGreaterThan(0.9);
    }

    /// <summary>No pedal under chords that never change, a vamp's or a home held, which it would only flatten onto one note; in Djent's songs, which vamp and pedal both.</summary>
    [Test]
    public async Task NoPedal_IsHeldUnderOneChord()
    {
        var (pedalled, unchanging) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Rmg.Tests.Genres.GenrePresets.Genre("djent")))))
        {
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var pedalling = song.Trace.Where(x => x.Point == TracePoints.SectionPedal && x.Value is not null).ToDictionary(x => x.Section, x => (int)x.Value!);
            var bass = song.Song.Notes![song.Song.Track(TrackRole.Bass)];
            // but where a key change's pedal leads into the new key, a bar before it
            var keyChanges = ((ImmutableArray<KeyChange>)song.Trace.Single(x => x.Point == TracePoints.KeyChange).Value!).Select(x => x.Position).ToArray();
            foreach (var span in song.Song.Map!.Sections.Where(x => pedalling.ContainsKey(x.SectionId)))
            {
                pedalled++;
                var (start, end) = pedalling[span.SectionId] == 0 ? (span.Start, Math.Min(span.End, span.Start + span.Meter.PhraseDuration)) : (span.Phrases.Last().Start, span.End);
                if (ChordsChange(common, start, end))
                    continue;
                unchanging++;
                var held = bass.Where(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9 && x.Value.State.GetStateValue(CompositionStateKinds.BassPedal) > 0
                                           && !keyChanges.Any(k => x.Position >= k - span.Meter.BarDuration - 1e-9 && x.Position < k - 1e-9));
                await Assert.That(held.Any()).IsFalse().Because($"seed {song.Seed} at {start}");
            }
        }

        Console.WriteLine($"{pedalled} pedalled appearances, {unchanging} of them over one chord");
        await Assert.That(unchanging).IsGreaterThan(2);
    }

    /// <summary>Whether a chord changes, its root or its move by semitones, a planed chord's, after a position and before another.</summary>
    private static bool ChordsChange(StateTimelineMap common, double start, double end)
    {
        (int, int) Chord(double at) => (common.GetEffectiveStateMapAt(at).GetStateValue(StateKinds.ChordRoot), common.GetEffectiveStateMapAt(at).GetStateValue(StateKinds.ChordShift));
        return common.GetStateTimeline(StateKinds.ChordChange).Any(x => x.Position > start + 1e-9 && x.Position < end - 1e-9 && Chord(x.Position) != Chord(start));
    }
}
