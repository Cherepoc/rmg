using Rmg.Core.Composition;

namespace Rmg.Tests.Melodies;

/// <summary>How far a melody's later lines keep its first's rhythm, a sequence's, rather than draw their own.</summary>
public sealed class MelodySequenceReportTest
{
    /// <summary>
    ///     Every bar of a melody's later letter in its section's first appearance, of two notes or more, its first bar's
    ///     too: whether its letter borrows the first's rhythm, and whether it starts its notes where the first bar does.
    /// </summary>
    internal static (bool Borrows, bool Same)[] Sequences(CorpusSong song)
    {
        var melody = song.Track(TrackRole.Melody);
        if (!song.Song.Notes!.TryGetValue(melody, out var notes))
            return [];
        var schemes = song.Trace.Where(x => x.Point == TracePoints.BarPattern && x.Track == melody && x.Value is PhraseScheme)
            .GroupBy(x => x.Section).ToDictionary(x => x.Key, x => ((PhraseScheme)x.First().Value!).Across(song.MeterOf(x.Key).Phrases));
        return
        [
            ..song.PlayedSpans.GroupBy(x => x.SectionId).Select(x => x.First()).Where(x => schemes.ContainsKey(x.SectionId)).SelectMany(span =>
            {
                string Onsets(int bar)
                {
                    var start = span.Start + bar * span.Meter.BarDuration;
                    var onsets = notes.Where(x => x.Position >= start - 1e-9 && x.Position < start + span.Meter.BarDuration - 1e-9).Select(x => Math.Round(x.Position - start, 3)).ToArray();
                    return onsets.Length >= 2 ? string.Join(",", onsets) : "";
                }

                var scheme = schemes[span.SectionId];
                var first = Onsets(0);
                return first == ""
                    ? []
                    : Enumerable.Range(0, Math.Min(scheme.Letters.Length, (int)Math.Round(span.Duration / span.Meter.BarDuration)))
                        .Where(bar => scheme.Letters[bar] != 0 && Onsets(bar) != "").Select(bar => (scheme.RhythmOf(scheme.Letters[bar]) == 0, Onsets(bar) == first));
            })
        ];
    }

    /// <summary>
    ///     A melody's later letter that borrows its first's rhythm plays it far more often than one that does not, in the same
    ///     songs (seen 53% against 8% in 128 songs), short of every bar, as an answer's bars draw their rhythm afresh and a
    ///     phrase's last holds its note.
    /// </summary>
    [Test]
    public async Task ALetterBorrowingTheFirstsRhythm_PlaysIt()
    {
        var bars = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => Sequences(TestCorpus.Get(seed))).SelectMany(x => x).ToArray();
        double Same(bool borrows) => bars.Where(x => x.Borrows == borrows).Count(x => x.Same) / (double)Math.Max(1, bars.Count(x => x.Borrows == borrows));
        Console.WriteLine($"  {bars.Count(x => x.Borrows)} borrowing bars, the first's rhythm in {Same(true):P0}; {bars.Count(x => !x.Borrows)} others, in {Same(false):P0}");
        await Assert.That(bars.Count(x => x.Borrows)).IsGreaterThan(30);
        await Assert.That(Same(true)).IsGreaterThan(Same(false) + 0.25);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var bars = TestCorpus.Measure(256, Sequences).SelectMany(x => x).ToArray();
        Console.WriteLine($"  {bars.Length} bars of a melody's later letters: its first bar's rhythm in {bars.Count(x => x.Same) / (double)bars.Length:P0}, " +
                          $"{bars.Count(x => x.Borrows)} borrowing it in {bars.Where(x => x.Borrows).Count(x => x.Same) / (double)Math.Max(1, bars.Count(x => x.Borrows)):P0}");
        await Task.CompletedTask;
    }
}
