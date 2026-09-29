using Rmg.Core.Composition;

namespace Rmg.Tests.DrumKits;

/// <summary>
///     How often a percussion drum that colours a kit's groove plays in another feel than the lead of its main role in the
///     same bar: its tuplet (<see cref="ResolvedRhythm.PrimeIndex" />), or whether its cycle is grouped, as a dotted 8th's
///     is (<see cref="ResolvedRhythm.IsGrouped" />), where the lead's is not, or the other way.
/// </summary>
public sealed class PercussionFeelReportTest
{
    private const int SongCount = 100;

    [Test]
    [Explicit]
    public async Task Report()
    {
        int bars = 0, otherTuplet = 0, otherGrouping = 0;
        foreach (var song in TestCorpus.Range(SongCount))
        {
            var patterns = song.Trace.Where(x => x.Point == TracePoints.BarPattern)
                .GroupBy(x => (x.Section, x.Track, x.Bar))
                .ToDictionary(x => x.Key, x => ResolvedRhythm.Of(x.First().StateMap));
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Kit))
            {
                var kit = (SectionKit)entry.Value!;
                if (!kit.Drums.Contains(DrumDefinitions.Kick) && !kit.Drums.Any(x => x.Family == DrumFamily.Kit))
                    continue;

                foreach (var drum in kit.Drums.Where(x => x.Family.HasFlag(DrumFamily.Percussion) && !kit.Leads.Contains(x) && !kit.Doubles.ContainsKey(x)))
                {
                    var lead = kit.Leads.FirstOrDefault(x => x.MainRole == drum.MainRole);
                    if (lead is null)
                        continue;

                    for (var bar = 0; bar < Rmg.Core.Composition.Progressions.BarCount; bar++)
                    {
                        if (!patterns.TryGetValue((entry.Section, DrumGroups.GetTrackNumber(drum), bar), out var own)
                            || !patterns.TryGetValue((entry.Section, DrumGroups.GetTrackNumber(lead), bar), out var led))
                            continue;

                        bars++;
                        otherTuplet += own.PrimeIndex != led.PrimeIndex ? 1 : 0;
                        otherGrouping += ResolvedRhythm.IsGrouped(own.Period) != ResolvedRhythm.IsGrouped(led.Period) ? 1 : 0;
                    }
                }
            }
        }

        Console.WriteLine($"{bars} bars of percussion colouring a kit's groove: another tuplet than its lead's {otherTuplet / (double)Math.Max(1, bars):P0}, " +
                          $"another grouping {otherGrouping / (double)Math.Max(1, bars):P0}");
        await Task.CompletedTask;
    }
}
