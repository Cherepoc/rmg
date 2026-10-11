using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Arrangements;

/// <summary>Call and response (<see cref="CallAndResponse" />): a verse's melody singing its phrases' first halves, a part answering in their second.</summary>
public sealed class CallAndResponseTest
{
    [Test]
    public async Task ACalledPhrase_IsAlwaysAnswered_ByTheRiffOrTheCounterMelody_RestingInItsFirstHalf()
    {
        var (phrases, cut, byCounter) = (0, 0, 0);
        var blues = GenrePresets.Genre("blues");
        // the same songs never calling, whose melody sings where the calling song's was cut
        var never = new Genre(blues.Choices.SetItem(GenreChoices.CallAndResponse.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("sections", new ByConvention(0, 0, 0))));
        foreach (var (song, uncut) in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => (TestCorpus.Get(seed, new SongOverrides(Genre: blues)), TestCorpus.Get(seed, new SongOverrides(Genre: never)))))
        {
            var calling = song.Trace.Where(t => t.Point == TracePoints.CallAndResponse && (bool)t.Value!).Select(t => t.Section).ToHashSet();
            var notes = song.Song.Notes!;
            var melody = notes[song.Track(TrackRole.Melody)];
            var answerers = new[] { TrackRole.Riff, TrackRole.CounterMelody }.Select(x => notes.GetValueOrDefault(song.Track(x))).ToArray();
            foreach (var span in song.Map.Sections.Where(x => calling.Contains(x.SectionId) && !song.IsSolo(x)))
            for (var start = span.Start; start < span.End - 1e-9; start += span.Meter.PhraseDuration)
            {
                phrases++;
                var half = start + span.Meter.PhraseBars / 2 * span.Meter.BarDuration;
                var end = Math.Min(start + span.Meter.PhraseDuration, span.End);
                // a phrase cut, its melody singing in the second half in the song never calling and not in this one, but its
                // walk into the next section
                bool Late(Rmg.Core.Events.EventTimeline<RealizedNote> line) =>
                    line.Any(x => x.IsWithin(half, end) && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0 && x.Value.State.GetStateValue(CompositionStateKinds.Walk) == 0);
                if (Late(melody) || !Late(uncut.Song.Notes![uncut.Track(TrackRole.Melody)]))
                    continue;
                cut++;
                // answered by the first part playing in its second half, which rests in the first, but for a song's first
                // note, which a song otherwise silent brings in on its first beat (SilentStart)
                var answerer = answerers.FirstOrDefault(x => x?.Any(n => n.IsWithin(half, end)) == true);
                await Assert.That(answerer).IsNotNull().Because($"seed {song.Seed} at {start}: {string.Join(" ", notes.Where(x => x.Value.Any(n => n.IsWithin(half, end))).Select(x => song.Song.TrackDefinitions[x.Key].Role))}; intro {song.Map.Intro.Kind} origin {song.Origin}; riff {notes.GetValueOrDefault(song.Track(TrackRole.Riff))?.Count}, counter {notes.GetValueOrDefault(song.Track(TrackRole.CounterMelody))?.Count}");
                await Assert.That(answerer!.Count(x => x.IsWithin(start, half) && x.Position > 1e-9)).IsEqualTo(0).Because($"seed {song.Seed} at {start}");
                byCounter += answerer == answerers[1] ? 1 : 0;
            }
        }

        Console.WriteLine($"{phrases} phrases of calling verses: {cut} cut and answered, {byCounter} by the counter-melody");
        await Assert.That(cut).IsGreaterThan(10);
    }
}
