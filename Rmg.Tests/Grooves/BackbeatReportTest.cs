using System.Collections.Immutable;
using Rmg.Core.Composition;

namespace Rmg.Tests.Grooves;

/// <summary>
///     How often the backbeat's drum plays the backbeat, by how conventional the song's rhythm is and how much energy the
///     section has: every bar of its, but a pattern's last, where a fill may play, by the rhythm settings it was made
///     with against the section's time feel: on the backbeat, faster (double time), slower (half time), in a tuplet, or
///     shifted; and how often a bar of each strikes both of the bar's backbeats in four.
/// </summary>
public sealed class BackbeatReportTest
{
    private const int SongCount = 256;

    private static readonly string[] Outcomes = ["backbeat", "faster", "slower", "tuplet", "shifted"];

    private sealed record Bar(int Seed, int Rhythm, double Energy, string Outcome, bool? StrikesBoth);

    [Test]
    [Explicit]
    public async Task Report()
    {
        var bars = TestCorpus.Measure(SongCount, Measure).SelectMany(x => x).ToArray();
        // the sections' energy in thirds of the sections measured
        var energies = bars.Select(x => x.Energy).Order().ToArray();
        var (low, high) = (energies[energies.Length / 3], energies[energies.Length * 2 / 3]);
        int EnergyBand(double energy) => energy < low ? 0 : energy < high ? 1 : 2;

        // a share with its 90% interval, the songs drawn again with replacement, as a song's bars go together: the song's
        // and its sections' layers move all of them
        string Share(Bar[] of, string outcome)
        {
            var songs = of.GroupBy(x => x.Seed).Select(x => (Bars: x.Count(), Of: x.Count(b => b.Outcome == outcome))).ToArray();
            var random = new Random(1);
            var shares = Enumerable.Range(0, 1000).Select(_ =>
            {
                var drawn = Enumerable.Range(0, songs.Length).Select(_ => songs[random.Next(songs.Length)]).ToArray();
                return drawn.Sum(x => x.Of) / (double)drawn.Sum(x => x.Bars);
            }).Order().ToArray();
            return $"{outcome} {of.Count(x => x.Outcome == outcome) / (double)of.Length * 100:F0} ({shares[50] * 100:F0}-{shares[950] * 100:F0})";
        }

        void Print(string name, Bar[] of) =>
            Console.WriteLine($"{name,-24} {of.Select(x => x.Seed).Distinct().Count(),4} songs {of.Length,5} bars: {string.Join(", ", Outcomes.Select(o => Share(of, o)))}");

        Print("all", bars);
        foreach (var band in bars.GroupBy(x => x.Rhythm).OrderBy(x => x.Key))
            Print($"rhythm {band.Key}/3", [..band]);
        foreach (var band in bars.GroupBy(x => EnergyBand(x.Energy)).OrderBy(x => x.Key))
            Print($"energy {band.Key}/3", [..band]);
        foreach (var band in bars.GroupBy(x => (x.Rhythm, Energy: EnergyBand(x.Energy))).OrderBy(x => x.Key))
            Print($"rhythm {band.Key.Rhythm}/3, energy {band.Key.Energy}/3", [..band]);

        // in four, how often a bar of each outcome strikes both 2 and 4
        foreach (var outcome in Outcomes)
        {
            var inFour = bars.Where(x => x.Outcome == outcome && x.StrikesBoth is not null).ToArray();
            Console.WriteLine($"in four, {outcome}: both 2 and 4 in {inFour.Count(x => x.StrikesBoth == true) / (double)Math.Max(1, inFour.Length):P0} of {inFour.Length}");
        }

        await Task.CompletedTask;
    }

    private static Bar[] Measure(CorpusSong song)
    {
        var meter = song.Map.Meter;
        var rhythm = Math.Min(2, (int)(((RhythmicUnconventionality)song.Trace.Single(x => x.Point == TracePoints.SongRhythm).Value!).Value * 3));
        var feels = song.Trace.Where(x => x.Point == TracePoints.TimeFeel).ToDictionary(x => x.Section, x => (int)x.Value!);
        var energies = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
        var bars = new List<Bar>();
        // a section's bars as it first plays them, which its trace records
        foreach (var span in song.Map.Sections.GroupBy(x => x.SectionId).Select(x => x.First()))
        {
            if (!song.HasDrums(span))
                continue;

            var changed = (ImmutableDictionary<int, DrumRole>)song.Trace.First(e => e.Point == TracePoints.DrumRoles && e.Section == span.SectionId).Value!;
            DrumRole RoleOf(int track) =>
                changed.TryGetValue(track, out var role) ? role : (DrumRole)song.Song.TrackDefinitions[track].StateMap.GetStateValue(CompositionStateKinds.DrumRole).Value;

            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.BarPattern && x.Section == span.SectionId && x.Bar % Meter.PatternBarCount != Meter.PatternBarCount - 1))
            {
                var track = entry.Track;
                if (song.Song.TrackDefinitions[track].Role != Rmg.Core.Songs.TrackRole.Drum || !song.Song.Notes!.ContainsKey(track) || RoleOf(track) != DrumRole.Backbeat)
                    continue;

                // the backbeat's period is half a bar, a step longer in half time and shorter in double time
                var r = CompositionStateKinds.Rhythm;
                var (power, prime, phase) = (entry.StateMap.GetStateValue(r.Period.Power), entry.StateMap.GetStateValue(r.Period.PrimeIndex), entry.StateMap.GetStateValue(r.Phase.Rank));
                var expected = DrumRoles.Parts[DrumRole.Backbeat].PeriodPower + feels[span.SectionId];
                var outcome = prime != 0 ? "tuplet" : power < expected ? "faster" : power > expected ? "slower" : phase != 1 ? "shifted" : "backbeat";

                bool? strikesBoth = null;
                if (meter == Meter.FourFour && feels[span.SectionId] == 0)
                {
                    var start = span.Start + entry.Bar * meter.BarDuration;
                    var places = song.Song.Notes[track]
                        .Where(x => x.Position >= start - 1e-9 && x.Position < start + meter.BarDuration - 1e-9 && x.Value.State.GetStateValue(Rmg.Core.Events.StateKinds.ArticulationIndex) == 0)
                        .Select(x => Math.Round(x.Position - start, 3))
                        .ToHashSet();
                    strikesBoth = places.Contains(1) && places.Contains(3);
                }

                bars.Add(new Bar(song.Seed, rhythm, energies[span.SectionId], outcome, strikesBoth));
            }
        }

        return [..bars];
    }
}
