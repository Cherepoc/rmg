using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.RhythmicUnconventionalities;

/// <summary>
///     Songs at both ends of the unconventionality, their base supplied as 0 and as 1, and fairly wild between, written to songs/conventionality to
///     listen to, each described: its meter, tempo, scales, feels, form, drums, key change, intro, ending and chords.
/// </summary>
public sealed class ConventionalityExamplesTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "songs", "conventionality");
        Directory.CreateDirectory(directory);
        foreach (var (name, @base) in new[] { ("plainest", 0.0), ("fairly-wild", 96 / 127.0), ("wildest", 1.0) })
        foreach (var seed in new[] { 1, 2, 3 })
        {
            var song = TestCorpus.Get(seed, new SongOverrides(Base: @base));
            var trace = song.Trace;
            string Text(string point) => trace.FirstOrDefault(x => x.Point == point)?.Phrase ?? "-";
            var map = song.Map;
            var (_, scale) = ((HarmonicUnconventionality, Scale))trace.Single(x => x.Point == TracePoints.SongHarmony).Value!;
            var feels = trace.Where(x => x.Point == TracePoints.Feel).Select(x => (int)x.Value!).ToArray();
            var chords = trace.Where(x => x.Point == TracePoints.Chord && x.Track == SongTracks.ChordsTrack)
                .Select(x => { var role = x.StateMap.GetStateValue(CompositionStateKinds.RoleChord); return (role.IsEmpty ? CompositionStateKinds.ChordPool.Pick(x.StateMap) : role[0]).Shape; })
                .ToArray();
            var shapes = string.Join(", ", chords.GroupBy(x => x.Name).OrderByDescending(x => x.Count()).Take(5).Select(x => $"{x.Key} {x.Count() * 100 / chords.Length}%"));
            var sectionScales = trace.Where(x => x.Point == TracePoints.SectionScale).Select(x => ((Scale)x.Value!).Name).Distinct();
            var tempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.Tempo) * Meter.BaseTempo;
            var file = Path.Combine(directory, $"{name}-{seed}.mid");
            await using (var stream = File.Create(file))
                song.Rendered.Write(stream, $"{name} {seed}");
            Console.WriteLine($"EXAMPLE {name}-{seed}.mid: {map.Meter.TimeSignature.Numerator}/{map.Meter.TimeSignature.Denominator} at {tempo:F0}, {scale.Name} (sections in {string.Join(", ", sectionScales)}), feel {string.Join(" then ", feels)}, " +
                              $"swing {Text(TracePoints.Swing)}, form {Text(TracePoints.SongForm)}, drums {Text(TracePoints.DrumSetup)}, key change {Text(TracePoints.KeyChange)}, {map.Sections.Length} sections, {map.Duration / map.Meter.BarDuration:F0} bars; " +
                              $"intro {map.Intro.Kind}, ending {map.Ending.Kind}; chords {shapes}");
        }
    }
}
