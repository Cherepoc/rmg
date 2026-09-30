using System.Collections.Immutable;
using Rmg.Core.Composition;

namespace Rmg.Tests.DrumKits;

/// <summary>
///     How often a drum that does not lead plays in another feel than the lead of its main role in the same bar: its
///     tuplet (<see cref="ResolvedRhythm.PrimeIndex" />), or whether its cycle is grouped, as a dotted 8th's is
///     (<see cref="ResolvedRhythm.IsGrouped" />), where the lead's is not, or the other way. A drum that colours, by
///     whether it follows its lead's feel, and one bound to its lead by a figure of its own, apart.
/// </summary>
public sealed class PercussionFeelReportTest
{
    private const int SongCount = 128;

    private sealed class Tally
    {
        public int Bars, OtherTuplet, OtherGrouping;

        public override string ToString() =>
            $"{Bars} bars, another tuplet than its lead's {OtherTuplet / (double)Math.Max(1, Bars):P0}, another grouping {OtherGrouping / (double)Math.Max(1, Bars):P0}";
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var following = new Tally();
        var crossing = new Tally();
        var figures = new Tally();
        int colours = 0, follows = 0;
        foreach (var song in TestCorpus.Range(SongCount))
        {
            var patterns = song.Trace.Where(x => x.Point == TracePoints.BarPattern)
                .GroupBy(x => (x.Section, x.Track, x.Bar))
                .ToDictionary(x => x.Key, x => ResolvedRhythm.Of(x.First().StateMap));
            var feelLeads = song.Trace.Where(x => x.Point == TracePoints.FeelLeads)
                .ToDictionary(x => x.Section, x => (ImmutableDictionary<int, int>)x.Value!);
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Kit))
            {
                var kit = (SectionKit)entry.Value!;
                foreach (var drum in kit.Drums.Where(x => !kit.Leads.Contains(x)))
                {
                    var isFigure = kit.Doubles.ContainsKey(drum);
                    var lead = isFigure ? kit.Doubles[drum] : kit.Leads.FirstOrDefault(x => x.MainRole == drum.MainRole);
                    if (lead is null || (isFigure && song.Trace.Any(e => e.Point == TracePoints.Doubles && e.Section == entry.Section
                                                                     && ((ImmutableDictionary<int, Doubling>)e.Value!)[DrumGroups.GetTrackNumber(drum)].Binding != DrumBinding.Figure)))
                        continue;

                    var follows_ = feelLeads.TryGetValue(entry.Section, out var leads) && leads.ContainsKey(DrumGroups.GetTrackNumber(drum));
                    if (!isFigure)
                    {
                        colours++;
                        follows += follows_ ? 1 : 0;
                    }

                    var tally = isFigure ? figures : follows_ ? following : crossing;
                    for (var bar = 0; bar < Rmg.Core.Composition.Progressions.BarCount; bar++)
                    {
                        if (!patterns.TryGetValue((entry.Section, DrumGroups.GetTrackNumber(drum), bar), out var own)
                            || !patterns.TryGetValue((entry.Section, DrumGroups.GetTrackNumber(lead), bar), out var led))
                            continue;

                        tally.Bars++;
                        tally.OtherTuplet += own.PrimeIndex != led.PrimeIndex ? 1 : 0;
                        tally.OtherGrouping += ResolvedRhythm.IsGrouped(own.Period) != ResolvedRhythm.IsGrouped(led.Period) ? 1 : 0;
                    }
                }
            }
        }

        Console.WriteLine($"Drums colouring a section with a lead of their main role: {colours}, following its feel {follows / (double)Math.Max(1, colours):P0}");
        Console.WriteLine($"Following: {following}");
        Console.WriteLine($"Crossing: {crossing}");
        Console.WriteLine($"Bound by a figure: {figures}");
        await Task.CompletedTask;
    }
}
