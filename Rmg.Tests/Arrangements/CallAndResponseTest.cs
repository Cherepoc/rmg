using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Arrangements;

/// <summary>Call and response (<see cref="CallAndResponse" />): a verse's melody singing its phrases' first halves, its riff answering in their second.</summary>
public sealed class CallAndResponseTest
{
    [Test]
    public async Task ACallingVerse_SingsEachPhrasesFirstHalf_TheRiffAnsweringInItsSecond()
    {
        var (phrases, melodyLate, riffEarly, riffAnswers) = (0, 0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("blues")))))
        {
            var calling = song.Trace.Where(t => t.Point == TracePoints.CallAndResponse && (bool)t.Value!).Select(t => t.Section).ToHashSet();
            var melody = song.Song.Notes![song.Track(TrackRole.Melody)];
            var riff = song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Riff));
            foreach (var span in song.Map.Sections.Where(x => calling.Contains(x.SectionId) && !song.IsSolo(x)))
            for (var start = span.Start; start < span.End - 1e-9; start += span.Meter.PhraseDuration)
            {
                phrases++;
                var half = start + span.Meter.PhraseBars / 2 * span.Meter.BarDuration;
                var end = Math.Min(start + span.Meter.PhraseDuration, span.End);
                // the melody's notes struck in the second half, but its walk into the next section
                melodyLate += melody.Count(x => x.IsWithin(half, end) && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0
                                                && x.Value.State.GetStateValue(CompositionStateKinds.Walk) == 0);
                // but a song's first note, which a song otherwise silent at its start brings in on its first beat (SilentStart)
                riffEarly += riff?.Count(x => x.IsWithin(start, half) && x.Position > 1e-9) ?? 0;
                riffAnswers += riff?.Any(x => x.IsWithin(half, end)) == true ? 1 : 0;
            }
        }

        Console.WriteLine($"{phrases} phrases calling: the melody's notes in their second halves {melodyLate}, the riff's in their first {riffEarly}, the riff answering {riffAnswers}");
        await Assert.That(phrases).IsGreaterThan(20);
        await Assert.That(melodyLate).IsEqualTo(0);
        await Assert.That(riffEarly).IsEqualTo(0);
    }
}
