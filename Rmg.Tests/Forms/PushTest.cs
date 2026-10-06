using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>A push: a pushing section's chord changes on the bar played an eighth early by the band's harmony, the drums keeping time.</summary>
public sealed class PushTest
{
    [Test]
    public async Task APushedChange_IsPlayedAnEighthEarly_ByTheChords_TheDrumsOnTheBar()
    {
        int pushed = 0, early = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("funk")))))
        {
            var pushing = song.Trace.Where(x => x.Point == TracePoints.Push && (bool)x.Value!).Select(x => x.Section).ToHashSet();
            var changes = song.ChordChanges;
            var chords = song.Song.Notes!.GetValueOrDefault(SongTracks.ChordsTrack)?.ToArray() ?? [];
            foreach (var (span, index) in song.Song.Map!.Sections.Select((x, i) => (x, i)).Where(x => pushing.Contains(x.x.SectionId) && !song.Solos.ContainsKey(x.i)))
            foreach (var change in changes.Where(x => x > span.Start + 1e-9 && x < span.End - 1e-9 && Math.Abs(Math.IEEERemainder(x - span.Start, span.Meter.BarDuration)) < 1e-6))
            {
                pushed++;
                if (chords.Any(x => Math.Abs(x.Position - (change - Pushes.Early)) < 1e-6) && !chords.Any(x => Math.Abs(x.Position - change) < 1e-6))
                    early++;
            }
        }

        await Assert.That(pushed).IsGreaterThan(20);
        // the chords play most pushed changes early, but where they came in, a stop time, or rested the bar before
        await Assert.That(early / (double)pushed).IsGreaterThan(0.5);
    }
}
