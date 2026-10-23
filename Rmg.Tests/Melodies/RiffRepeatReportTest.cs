using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Melodies;

/// <summary>How much a section's riff, as it comes back, plays what it played the first time.</summary>
public sealed class RiffRepeatReportTest
{
    /// <summary>
    ///     Every bar of a section's first pattern as a later appearance plays it, but a solo's, against the first
    ///     appearance's bar, each of two notes or more: whether it starts its notes where the first does, and how many of
    ///     its notes play the first's pitch at the same place, and its pitch class, an octave apart, of how many.
    /// </summary>
    internal static (bool SameHarmony, bool Rhythm, int Kept, int Classes, int Notes)[] Repeats(CorpusSong song)
    {
        if (!song.Song.Notes!.TryGetValue(song.Track(TrackRole.Riff), out var notes))
            return [];
        return
        [
            ..song.PlayedSpans.Where(x => !song.IsSolo(x)).GroupBy(x => x.SectionId).Where(x => x.Count() > 1).SelectMany(group =>
            {
                var first = group.First();
                var bar = first.Meter.BarDuration;
                var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
                // the chords' roots and the key's over a bar, as its state has them
                string Harmony(SectionSpan span, int k) => string.Join(",", Enumerable.Range(0, 8).Select(i => common.GetEffectiveStateMapAt(span.Start + k * bar + i * bar / 8) is var state
                    ? $"{state.GetStateValue(StateKinds.ChordRoot)}:{state.GetStateValue(StateKinds.KeyOffset)}:{state.GetStateValue(StateKinds.ChordShift)}" : ""));
                return group.Skip(1).Where(x => x.Meter.Equals(first.Meter)).SelectMany(later => Enumerable.Range(0, first.Meter.PatternBarCount).Select(k => (BarNotes.Of(notes, first, k), BarNotes.Of(notes, later, k), Harmony(first, k) == Harmony(later, k))))
                    .Where(x => x.Item1.Length >= 2 && x.Item2.Length >= 2)
                    .Select(x => BarNotes.Compare(x.Item1, x.Item2) is var (rhythm, kept, count)
                        ? (x.Item3, rhythm, kept, x.Item2.Count(n => x.Item1.Any(m => m.Offset == n.Offset && (m.Pitch - n.Pitch) % 12 == 0)), count)
                        : default);
            })
        ];
    }

    private static string Line(string name, (bool SameHarmony, bool Rhythm, int Kept, int Classes, int Notes)[] bars) =>
        $"  {name}: {bars.Length} bars come back, the first's rhythm in {bars.Count(x => x.Rhythm) / (double)Math.Max(1, bars.Length):P0}, its pitches in {bars.Sum(x => x.Kept) / (double)Math.Max(1, bars.Sum(x => x.Notes)):P0} of notes, their pitch classes in {bars.Sum(x => x.Classes) / (double)Math.Max(1, bars.Sum(x => x.Notes)):P0}";

    [Test]
    [Explicit]
    public async Task Report()
    {
        var own = TestCorpus.Measure(256, Repeats).SelectMany(x => x).ToArray();
        Console.WriteLine(Line("the songs' own", own));
        Console.WriteLine(Line("the songs' own, over the same chords and key", [..own.Where(x => x.SameHarmony)]));
        foreach (var genre in new[] { "rock", "metal", "thrash", "funkRock", "blues", "punk" })
        {
            var overrides = new SongOverrides(Genre: GenrePresets.Genre(GenrePresets.All.Single(x => x.Id == genre)));
            Console.WriteLine(Line(genre, TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Repeats(TestCorpus.Get(seed, overrides))).SelectMany(x => x).ToArray()));
        }
        await Task.CompletedTask;
    }
}
