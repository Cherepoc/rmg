using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.Grooves;

/// <summary>
///     Where the backbeat's drum plays in four: its groove's notes by their place in the bar, their ranks and how loud,
///     and how many bars strike both 2 and 4, by the section's time feel; a pattern's last bar, where a fill may play,
///     left out.
/// </summary>
public sealed class BackbeatReportTest
{
    private static readonly Dictionary<string, int> Settings = [];

    private static void Count(string key) => Settings[key] = Settings.GetValueOrDefault(key) + 1;

    [Test]
    [Explicit]
    public async Task Report()
    {
        var stats = new SortedDictionary<int, (int Bars, int Both, SortedDictionary<double, (int Count, double Velocity)> Places, SortedDictionary<int, int> Ranks)>();
        foreach (var song in TestCorpus.Range(200).Where(x => x.Map.Meter == Meter.FourFour))
        {
            var meter = song.Map.Meter;
            var band = Math.Min(2, (int)(((RhythmicUnconventionality)song.Trace.Single(x => x.Point == TracePoints.SongRhythm).Value!).Value * 3));
            var feels = song.Trace.Where(x => x.Point == TracePoints.TimeFeel).ToDictionary(x => x.Section, x => (int)x.Value!);
            foreach (var span in song.Map.Sections)
            {
                var changed = (ImmutableDictionary<int, DrumRole>)song.Trace.First(e => e.Point == TracePoints.DrumRoles && e.Section == span.SectionId).Value!;
                var backbeats = song.Song.Notes!.Keys.Where(t => song.Song.TrackDefinitions[t].Role == Rmg.Core.Songs.TrackRole.Drum)
                    .Where(t => (changed.TryGetValue(t, out var r) ? r : (DrumRole)song.Song.TrackDefinitions[t].StateMap.GetStateValue(CompositionStateKinds.DrumRole).Value) == DrumRole.Backbeat)
                    .ToArray();
                if (!song.HasDrums(span))
                    continue;

                var feel = feels[span.SectionId];
                // the bars' rhythm settings as generated, in time, by the layers that make them
                if (feel == 0)
                    foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.BarPattern && x.Section == span.SectionId && backbeats.Contains(x.Track)))
                    {
                        var r = CompositionStateKinds.Rhythm;
                        var m = entry.StateMap;
                        var isBackbeat = m.GetStateValue(r.Period.Power) == -1 && m.GetStateValue(r.Period.PrimeIndex) == 0 && m.GetStateValue(r.Phase.Rank) == 1;
                        Count($"band {band}: {(isBackbeat ? "backbeat" : "other")}");
                        foreach (var c in m.Explain(r.Period.Power))
                            Count($"  power from {c.Layer} {c.Value}");
                        foreach (var c in m.Explain(r.Phase.Rank))
                            Count($"  phase rank from {c.Layer} {c.Value}");
                    }
                var s = stats.TryGetValue(feel, out var had) ? had : (Bars: 0, Both: 0, Places: new SortedDictionary<double, (int Count, double Velocity)>(), Ranks: new SortedDictionary<int, int>());
                for (var bar = 0; bar * meter.BarDuration < span.Duration - 1e-9; bar++)
                {
                    if (bar % Meter.PatternBarCount == Meter.PatternBarCount - 1)
                        continue;

                    var start = span.Start + bar * meter.BarDuration;
                    var notes = backbeats.SelectMany(t => song.Song.Notes[t].Where(x => x.Position >= start - 1e-9 && x.Position < start + meter.BarDuration - 1e-9
                                                                                     && x.Value.State.GetStateValue(StateKinds.ArticulationIndex) == 0)).ToArray();
                    s.Bars++;
                    var places = notes.Select(x => Math.Round((x.Position - start) * 4) / 4).ToHashSet();
                    if (places.Contains(1) && places.Contains(3))
                        s.Both++;
                    foreach (var note in notes)
                    {
                        var place = Math.Round((note.Position - start) * 4) / 4;
                        var (count, velocity) = s.Places.GetValueOrDefault(place);
                        s.Places[place] = (count + 1, velocity + note.Value.Velocity);
                        var rank = note.Value.State.GetStateValue(CompositionStateKinds.BeatRank);

                        s.Ranks[rank] = s.Ranks.GetValueOrDefault(rank) + 1;
                    }
                }

                stats[feel] = s;
            }
        }

        foreach (var (key, count) in Settings.OrderByDescending(x => x.Value).Take(40))
            Console.WriteLine($"{key}: {count}");
        foreach (var (feel, s) in stats)
        {
            Console.WriteLine($"feel {feel}: {s.Bars} bars, both 2 and 4 in {s.Both / (double)s.Bars:P0}; ranks {string.Join(" ", s.Ranks.Select(x => $"{x.Key}:{x.Value}"))}");
            Console.WriteLine($"  per bar  {string.Join(" ", s.Places.Where(x => x.Value.Count / (double)s.Bars >= 0.03).Select(x => $"{x.Key}:{x.Value.Count / (double)s.Bars:F2}"))}");
            Console.WriteLine($"  velocity {string.Join(" ", s.Places.Where(x => x.Value.Count / (double)s.Bars >= 0.03).Select(x => $"{x.Key}:{x.Value.Velocity / x.Value.Count:F2}"))}");
        }

        await Task.CompletedTask;
    }
}
