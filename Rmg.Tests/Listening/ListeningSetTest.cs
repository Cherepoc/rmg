using System.Collections.Immutable;
using System.Text;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Tests.MelodyRhythms;

namespace Rmg.Tests.Listening;

/// <summary>
///     Songs to settle the questions listening is to answer, written to songs/listening with a checklist of what to
///     listen for in each and where: the melody's shape at the middle, the key changes of a fairly wild song, and the
///     chords of the slowest and the fastest songs that change them the least and the most often.
/// </summary>
public sealed class ListeningSetTest
{
    // a step of the unconventionality, from 0 to 127, as the page supplies it
    private static SongOverrides At(int step) => new(Base: step / 127.0);

    private static double Bpm(CorpusSong song) =>
        song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.Tempo) * Meter.BaseTempo;

    // a position as the time it is heard at, at the song's tempo, which only an ending's ritardando moves
    private static string Time(CorpusSong song, double position)
    {
        var seconds = (int)Math.Round(position * 60 / Bpm(song));
        return $"{seconds / 60}:{seconds % 60:D2}";
    }

    private static string Describe(CorpusSong song)
    {
        var meter = song.Map.Meter.TimeSignature;
        var (_, scale) = ((HarmonicUnconventionality, Scale))song.Trace.Single(x => x.Point == TracePoints.SongHarmony).Value!;
        return $"{meter.Numerator}/{meter.Denominator} at {Bpm(song):F0} BPM, {scale.Name}, {Time(song, song.Map.Duration)} long";
    }

    private static async Task Write(string directory, string name, CorpusSong song)
    {
        await using var stream = File.Create(Path.Combine(directory, $"{name}.mid"));
        song.Rendered.Write(stream, name);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "songs", "listening");
        Directory.CreateDirectory(directory);
        var checklist = new StringBuilder("# Listening set\n\n");

        checklist.AppendLine("## The melody's shape (step 64 of 127, the middle)\n");
        checklist.AppendLine("Do the phrases rise and fall like a tune going somewhere, a figure repeated higher where the phrase climbs, or do the figures drift? Does a section that comes back sound like itself? A song whose melody follows its aim little (melody-4) is there to compare.\n");
        foreach (var seed in new[] { 1, 2, 3, 4 })
        {
            var song = TestCorpus.Get(seed, At(64));
            var contour = MelodyContourTest.Measure([song]);
            var sections = string.Join(", ", song.Map.Sections.Select(x => $"{(char)('A' + x.SectionId)} {Time(song, x.Start)}"));
            await Write(directory, $"melody-{seed}", song);
            checklist.AppendLine($"- `melody-{seed}.mid`: {Describe(song)}; follows its phrases' aim {contour.Correlation:F2}; sections {sections}");
        }

        checklist.AppendLine("\n## Key changes (step 96 of 127, fairly wild)\n");
        checklist.AppendLine("A song this wild changes key about twice on average, but its scale facet strays from the base, so some change far more often. Is a change a lift, or too restless? Where is the count too many? Does each change land, or does it feel like the band lost its place?\n");
        foreach (var seed in new[] { 1, 2, 3, 4 })
        {
            var song = TestCorpus.Get(seed, At(96));
            var changes = (ImmutableArray<KeyChange>)song.Trace.Single(x => x.Point == TracePoints.KeyChange).Value!;
            var listed = changes.IsEmpty ? "none" : string.Join(", ", changes.Select(x => $"{Time(song, x.Position)} to {x.Semitones:+0;-0;0}"));
            await Write(directory, $"keys-{seed}", song);
            checklist.AppendLine($"- `keys-{seed}.mid`: {Describe(song)}; key changes (semitones from the song's key) {listed}");
        }

        checklist.AppendLine("\n## Chord length by tempo (drawn songs)\n");
        checklist.AppendLine("Does a chord every two bars in a slow song feel static, and two chords a bar in a fast one feel rushed? Each section is listed with how long its chords last.\n");
        var songs = TestCorpus.Range(256);
        var spans = songs.Select(song => (Song: song, Spans: song.Trace.Where(x => x.Point == TracePoints.HarmonicRhythm).ToDictionary(x => x.Section, x => (HarmonicRhythm)x.Value!))).ToArray();
        var slow = spans.Where(x => x.Spans.Values.Any(r => r.Bars == 2)).OrderBy(x => Bpm(x.Song)).Take(2);
        var fast = spans.Where(x => x.Spans.Values.Any(r => r.Bars == 0.5)).OrderByDescending(x => Bpm(x.Song)).Take(2);
        foreach (var (kind, (song, rhythms)) in slow.Select(x => ("slow", x)).Concat(fast.Select(x => ("fast", x))))
        {
            var seed = songs.ToList().IndexOf(song);
            var sections = string.Join(", ", song.Map.Sections.Select(x => $"{Time(song, x.Start)} {rhythms[x.SectionId].Span * 60 / Bpm(song):F1} s"));
            await Write(directory, $"chords-{kind}-{seed}", song);
            checklist.AppendLine($"- `chords-{kind}-{seed}.mid`: {Describe(song)}; each section's start and chord length: {sections}");
        }

        await File.WriteAllTextAsync(Path.Combine(directory, "CHECKLIST.md"), checklist.ToString());
        Console.WriteLine(checklist);
    }
}
