using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>Stop time: a section's first phrase, its band hitting the bar's first beat alone under the lines, which go on.</summary>
public sealed class StopTimeTest
{
    [Test]
    public async Task InStopTime_TheBandHitsOnceABar_AndTheMelodyGoesOn()
    {
        int phrases = 0, melodyNotes = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("blues")))))
        {
            var stopping = song.Trace.Where(x => x.Point == TracePoints.StopTime && (bool)x.Value!).Select(x => x.Section).ToHashSet();
            foreach (var span in song.Song.Map!.Sections.Where(x => stopping.Contains(x.SectionId)))
            {
                phrases++;
                var (start, end, bar) = (span.Start, Math.Min(span.End, span.Start + Meter.PhraseBarCount * span.Meter.BarDuration), span.Meter.BarDuration);
                foreach (var (track, notes) in song.Song.Notes!)
                {
                    var inPhrase = notes.Where(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9).ToArray();
                    if (song.Song.TrackDefinitions[track].Role is TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Solo)
                    {
                        melodyNotes += inPhrase.Length;
                        continue;
                    }

                    await Assert.That(inPhrase.All(x => Math.Abs(Math.IEEERemainder(x.Position - start, bar)) < 1e-6 && x.Value.Duration <= SongStages.StopTimeHit + 1e-9)).IsTrue();
                }
            }
        }

        await Assert.That(phrases).IsGreaterThan(5);
        await Assert.That(melodyNotes).IsGreaterThan(phrases * 4);
    }
}
