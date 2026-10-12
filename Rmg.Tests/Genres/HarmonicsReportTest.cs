using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     The driven guitars' pinch harmonics as the songs play them (<see cref="Expression.Pinches" />): which parts,
///     how long, how fast the playing around them, whether bent or wavering, how many a phrase.
/// </summary>
public sealed class HarmonicsReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "rock", "metal", "blackMetal", "djent", "nuMetal", "punk" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            var harmonics = songs.SelectMany(song => song.Song.Notes!.Where(n => song.Song.TrackDefinitions[n.Key] is PitchInstrumentTrack)
                .SelectMany(n => n.Value.Select((x, i) => (Song: song, Role: song.Song.TrackDefinitions[n.Key].Role, Notes: n.Value, Index: i, Note: x)))
                .Where(x => x.Note.Value.State.GetStateValue(CompositionStateKinds.Pinch) == 1)).ToArray();
            if (harmonics.Length == 0)
            {
                Console.WriteLine($"{name}: none");
                continue;
            }

            // seconds a beat at the note, and the part's notes a beat over the beat on either side of it
            double Seconds((CorpusSong Song, TrackRole Role, EventTimeline<RealizedNote> Notes, int Index, TimelineItem<RealizedNote> Note) x) =>
                Meter.SecondsPerBeat(x.Song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(x.Note.Position).GetStateValue(StateKinds.Tempo));
            double Around((CorpusSong Song, TrackRole Role, EventTimeline<RealizedNote> Notes, int Index, TimelineItem<RealizedNote> Note) x) =>
                x.Notes.Count(n => Math.Abs(n.Position - x.Note.Position) < 1) / 2.0;
            var seconds = harmonics.Select(x => x.Note.Value.Duration * Seconds(x)).ToArray();
            var phrases = harmonics.GroupBy(x => (x.Song.Seed, x.Role, Section: x.Song.Map.SectionAt(x.Note.Position)?.Start, Phrase: x.Song.Map.SectionAt(x.Note.Position)?.PhraseOf(x.Note.Position)))
                .Select(x => x.Count()).ToArray();
            Console.WriteLine($"{name}: {harmonics.Length} harmonics in {songs.Count(s => harmonics.Any(h => h.Song == s))} songs; parts {string.Join(", ", harmonics.GroupBy(x => x.Role).Select(x => $"{x.Key} {x.Count()}"))}; " +
                              $"chords {harmonics.Count(x => x.Note.Value.Pitches.Length > 1) / (double)harmonics.Length:P0}");
            Console.WriteLine($"{name}: shorter than 0.25 s {seconds.Count(x => x < 0.25) / (double)seconds.Length:P0}, than 0.5 s {seconds.Count(x => x < 0.5) / (double)seconds.Length:P0}; " +
                              $"in playing of 3 notes a beat or more {harmonics.Count(x => Around(x) >= 3) / (double)harmonics.Length:P0}; " +
                              $"palm-muted {harmonics.Count(x => x.Note.Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0) / (double)harmonics.Length:P0}; " +
                              $"bent or wavering {harmonics.Count(x => x.Note.Value.State.GetStateValue(CompositionStateKinds.Bend) != 0 || x.Note.Value.State.GetStateValue(CompositionStateKinds.Vibrato) > 0 || x.Note.Value.State.GetStateValue(CompositionStateKinds.WideVibrato) != 0) / (double)harmonics.Length:P0}; " +
                              $"a phrase that has any {phrases.Average():F1}, at most {phrases.Max()}");
        }

        await Task.CompletedTask;
    }
}
