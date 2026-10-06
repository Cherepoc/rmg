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
            // but a first section an intro of entries builds, which plays as the band comes in
            var intro = song.Map.Intro;
            var built = intro.Kind == IntroKind.Entries && !intro.Window.IsBefore ? song.Map.Sections[0] : null;
            foreach (var span in song.Song.Map!.Sections.Where(x => stopping.Contains(x.SectionId) && x != built))
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

                    // but the drums' fill out of it, in the section's last bar, which goes on as a drummer fills into the next
                    var fill = song.Song.TrackDefinitions[track].Role == TrackRole.Drum ? span.End - bar : double.PositiveInfinity;
                    await Assert.That(inPhrase.Where(x => x.Position < fill - 1e-9).All(x => Math.Abs(Math.IEEERemainder(x.Position - start, bar)) < 1e-6 && x.Value.Duration <= StopTime.Hit + 1e-9)).IsTrue();
                }
            }
        }

        await Assert.That(phrases).IsGreaterThan(5);
        await Assert.That(melodyNotes).IsGreaterThan(phrases * 4);
    }
}
