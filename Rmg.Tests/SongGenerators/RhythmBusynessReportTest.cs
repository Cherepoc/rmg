using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     How busy the rhythm is by how far a section strays from convention, told by its melody's answer amount, which rises
///     with it (<see cref="MelodyLayers.AnswerAmount" />): the notes a bar of the drums and of the pitched tracks play in
///     the groove, the pattern's last bar, where fills play, left out; how many of the drums' fall off the 8ths, as
///     syncopation, and off the 16ths, as tuplets; and how many notes a beat the fills play.
/// </summary>
public sealed class RhythmBusynessReportTest
{
    private const int SongCount = 300;

    private static readonly (string Name, double From, double To)[] Bands = [("plain", 0, 0.35), ("middle", 0.35, 0.65), ("wild", 0.65, 0.8), ("wildest", 0.8, 1.01)];

    /// <summary>A section's rhythmic unconventionality, from its answer amount, a chance of 0.5 leaned by it.</summary>
    private static double Unconventionality(double amount) => 0.5 + Math.Log(amount / (1 - amount)) / Math.Log(16);

    [Test]
    [Explicit]
    public async Task Report()
    {
        var bands = Bands.ToDictionary(x => x.Name, _ => (Bars: 0, Drums: 0, Pitched: 0, OffEighths: 0, OffSixteenths: 0, FillNotes: 0, FillBeats: 0.0));
        foreach (var song in TestCorpus.Range(SongCount))
        {
            var amounts = song.Trace.Where(x => x.Point == TracePoints.MelodyAnswer).ToDictionary(x => x.Section, x => (double)x.Value!);
            string Band(int sectionId) => Bands.First(x => Unconventionality(amounts[sectionId]) >= x.From && Unconventionality(amounts[sectionId]) < x.To).Name;
            var notes = song.Song.Notes!;
            var drums = notes.Where(x => x.Key >= DrumGroups.FirstTrackNumber).SelectMany(x => x.Value).Select(x => x.Position).Order().ToArray();
            var pitched = notes.Where(x => x.Key < DrumGroups.FirstTrackNumber).SelectMany(x => x.Value).Select(x => x.Position).Order().ToArray();
            foreach (var span in song.Map.Sections)
            {
                var band = Band(span.SectionId);
                for (var bar = 0; bar < span.Duration / song.Map.Meter.BarDuration; bar++)
                {
                    if (bar % Meter.PatternBarCount == Meter.PatternBarCount - 1)
                        continue;

                    var start = span.Start + bar * song.Map.Meter.BarDuration;
                    var inBar = drums.Where(x => x >= start && x < start + song.Map.Meter.BarDuration).ToArray();
                    var b = bands[band];
                    bands[band] = b with
                    {
                        Bars = b.Bars + 1,
                        Drums = b.Drums + inBar.Length,
                        Pitched = b.Pitched + pitched.Count(x => x >= start && x < start + song.Map.Meter.BarDuration),
                        OffEighths = b.OffEighths + inBar.Count(x => !IsOn(x - start, 0.5)),
                        OffSixteenths = b.OffSixteenths + inBar.Count(x => !IsOn(x - start, 0.25))
                    };
                }
            }

            // the fills that play, at the lines the drums mark, as the fill test finds them
            var map = song.Map;
            var decisions = song.Trace.Where(x => x.Point == TracePoints.FillDecision).ToArray();
            var first = map.Intro.Duration > 0 ? 0 : 1;
            var last = (int)Math.Round((map.Sections[^1].End - song.Origin) / song.Map.Meter.PatternDuration) - (FormLayers.HasFinalChord(map.Ending.Kind) ? 0 : 1);
            var lines = Enumerable.Range(first, last - first + 1).Select(x => song.Origin + x * song.Map.Meter.PatternDuration).ToArray();
            for (var i = 0; i < Math.Min(lines.Length, decisions.Length); i++)
            {
                var decision = (FillDecision)decisions[i].Value!;
                if (decision.Span <= 0 || decision.Rests || map.SectionAt(lines[i] - 1e-3) is not { } section)
                    continue;

                var band = Band(section.SectionId);
                var b = bands[band];
                bands[band] = b with
                {
                    FillNotes = b.FillNotes + drums.Count(x => x >= lines[i] - decision.Span - 1e-6 && x < lines[i] - 1e-6),
                    FillBeats = b.FillBeats + decision.Span
                };
            }
        }

        foreach (var (name, b) in bands)
            Console.WriteLine($"{name,-8} {b.Bars,5} bars: drums {b.Drums / (double)b.Bars:F2} notes a bar, pitched {b.Pitched / (double)b.Bars:F2}; " +
                              $"drums off the 8ths {b.OffEighths / (double)b.Drums:P1}, off the 16ths {b.OffSixteenths / (double)b.Drums:P1}; " +
                              $"fills {b.FillNotes / Math.Max(1e-9, b.FillBeats):F2} notes a beat over {b.FillBeats:F0} beats");
        await Task.CompletedTask;
    }

    private static bool IsOn(double position, double grid) => Math.Abs(position / grid - Math.Round(position / grid)) < 1e-6;
}
