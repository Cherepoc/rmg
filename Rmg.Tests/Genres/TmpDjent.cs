using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;
namespace Rmg.Tests.Genres;
public sealed class TmpDjent
{
    [Test]
    [Explicit]
    public async Task Dump()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("djent"))));
        int all = 0, outOfKey = 0, root = 0, chromaticSteps = 0, moves = 0, chords = 0, muted = 0;
        double beats = 0;
        var stops = 0.0; var sections = 0;
        foreach (var song in songs)
        {
            var riff = song.Song.Notes!.GetValueOrDefault(SongTracks.RiffTrack);
            if (riff is null) continue;
            beats += song.Song.Duration;
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            int? before = null;
            foreach (var n in riff)
            {
                all++;
                var state = common.GetEffectiveStateMapAt(n.Position);
                var (offsets, rootStep, chord) = Realizer.GetChord(state);
                var pitch = n.Value.Pitches.Min();
                var key = state.GetStateValue(StateKinds.KeyOffset);
                var classes = offsets.Select(o => (o + key) % 12).ToHashSet();
                if (!classes.Contains(((pitch % 12) + 12) % 12)) outOfKey++;
                if (((pitch - chord.Root) % 12 + 12) % 12 == 0) root++;
                if (n.Value.Pitches.Length > 1) chords++;
                if (n.Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0) muted++;
                if (before is { } b && b != pitch) { moves++; if (Math.Abs(b - pitch) == 1) chromaticSteps++; }
                before = pitch;
            }
            var stopTimes = song.Trace.Where(x => x.Point == TracePoints.StopTime).Select(x => (bool)x.Value!).ToArray();
            stops += stopTimes.Count(x => x); sections += stopTimes.Length;
        }
        Console.WriteLine($"riff notes {all}, a beat {all / beats:F2}, out of the key {outOfKey / (double)all:P0}, on the chord's root {root / (double)all:P0}, half-step moves {chromaticSteps / (double)moves:P0} of moves, chords {chords / (double)all:P0}, palm-muted {muted / (double)all:P0}; sections in stop time {stops / sections:P0}");
        await Task.CompletedTask;
    }
}
