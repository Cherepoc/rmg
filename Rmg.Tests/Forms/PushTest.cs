using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>
///     A push: a pushing section's chord changes on the bar played an eighth early by the band's harmony and the kick, the
///     other drums keeping time.
/// </summary>
public sealed class PushTest
{
    [Test]
    public async Task APushedChange_IsPlayedAnEighthEarly_ByTheChordsAndTheKick()
    {
        int pushed = 0, early = 0, kicked = 0;
        var kicks = DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("funk")))))
        {
            var pushing = song.Trace.Where(x => x.Point == TracePoints.Push && (bool)x.Value!).Select(x => x.Section).ToHashSet();
            var changes = song.ChordChanges;
            var chords = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Chords))?.ToArray() ?? [];
            var kick = song.Song.Notes!.Where(x => kicks.Contains(x.Key)).SelectMany(x => x.Value).Select(x => x.Position).ToArray();
            // but in and at the end of a phrase in stop time, which a push leaves be, its fill landing there
            var stopped = song.Trace.Where(x => x.Point == TracePoints.StopTime).SelectMany(x => (ImmutableArray<double>)x.Value!).ToArray();
            // and in and at the end of a bar of ensemble hits, which a push leaves be as it does stop time
            var hits = song.Trace.Where(x => x.Point == TracePoints.EnsembleHits).SelectMany(x => (ImmutableArray<double>)x.Value!).ToArray();
            bool InStopTime(double at) => song.Song.Map!.Sections.Any(s => stopped.Any(x => Math.Abs(x - s.Start) < 1e-6) && at >= s.Start - 1e-9 && at <= s.Start + s.Meter.PhraseDuration + 1e-9)
                                          || song.Song.Map!.Sections.Any(s => hits.Any(x => at >= x - 1e-9 && at <= x + s.Meter.BarDuration + 1e-9 && s.IsWithin(x)));
            // but in a polymeter, whose cycle runs across the changes (Pushes)
            var polymetric = song.PolymeterPlans.Keys.ToHashSet();
            foreach (var (span, index) in song.Song.Map!.Sections.Select((x, i) => (x, i)).Where(x => pushing.Contains(x.x.SectionId) && !song.Solos.ContainsKey(x.i) && !polymetric.Contains(x.x.SectionId)))
            foreach (var change in changes.Where(x => x > span.Start + 1e-9 && x < span.End - 1e-9 && Math.Abs(Math.IEEERemainder(x - span.Start, span.Meter.BarDuration)) < 1e-6 && !InStopTime(x)))
            {
                pushed++;
                if (chords.Any(x => Math.Abs(x.Position - (change - Pushes.Early)) < 1e-6) && !chords.Any(x => Math.Abs(x.Position - change) < 1e-6))
                    early++;
                // the kick an eighth early, never on the bar as well
                var (before, on) = (kick.Any(x => Math.Abs(x - (change - Pushes.Early)) < 1e-6), kick.Any(x => Math.Abs(x - change) < 1e-6));
                await Assert.That(before && on).IsFalse().Because($"seed {song.Seed} at {change}");
                kicked += before ? 1 : 0;
            }
        }

        await Assert.That(pushed).IsGreaterThan(80);
        Console.WriteLine($"{pushed} changes pushed: the chords early on {early / (double)pushed:P0}, the kick on {kicked / (double)pushed:P0}");
        // measured: the chords 86%, the kick 73%, at offset 0
        // the chords play most pushed changes early, but where they came in, a stop time, or rested the bar before
        await Assert.That(early / (double)pushed).IsGreaterThan(0.5);
        await Assert.That(kicked / (double)pushed).IsGreaterThan(0.3);
    }

    /// <summary>The plainest songs never push, of no genre, whose chance is the songs' own.</summary>
    [Test]
    public async Task ThePlainestSongs_NeverPush()
    {
        var sections = 0;
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0))))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Push))
        {
            sections++;
            await Assert.That((bool)entry.Value!).IsFalse().Because($"seed {song.Seed}");
        }

        await Assert.That(sections).IsGreaterThan(16);
    }
}
