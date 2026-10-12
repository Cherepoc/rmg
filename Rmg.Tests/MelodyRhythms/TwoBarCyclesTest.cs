using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.MelodyRhythms;

/// <summary>
///     A bar pattern whose cycle is longer than a bar, two bars, which a bar plays the first half of: the reports that
///     measured playing it whole over a pair of bars, and put it off (*Long cycles* in the roadmap).
/// </summary>
public sealed class TwoBarCyclesTest
{
    /// <summary>A report of the bar patterns whose period is over a bar, by the part playing them.</summary>
    [Test]
    [Explicit]
    public async Task ReportSlowPatterns()
    {
        var counts = new Dictionary<string, (int All, int Slow)>();
        foreach (var song in TestCorpus.Range(64))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.BarPattern && song.Song.TrackDefinitions.ContainsKey(x.Track)))
        {
            var part = song.Song.TrackDefinitions[entry.Track] is PitchInstrumentTrack pitched ? pitched.Role.ToString() : "Drum";
            var (all, slow) = counts.GetValueOrDefault(part);
            counts[part] = (all + 1, slow + (ResolvedRhythm.Of(entry.StateMap).Period > Meter.ReferenceBar + 1e-9 ? 1 : 0));
        }

        foreach (var (part, (all, slow)) in counts.OrderBy(x => x.Key))
            Console.WriteLine($"{part}: {slow} of {all} bar patterns over a bar, {slow / (double)all:P1}");
        await Task.CompletedTask;
    }

    /// <summary>
    ///     A report of the pitched parts' bars by their place in a pair, the first or the second of a phrase's halves, over
    ///     the sections a part plays in: how often one strikes a note in it, how often a note sounds in it, struck there or
    ///     held from before, and how often it strikes its downbeat.
    /// </summary>
    [Test]
    [Explicit]
    public async Task ReportBarsByPlace()
    {
        var counts = new Dictionary<(TrackRole, int), (int Bars, int Struck, int Sounding, int Downbeats)>();
        foreach (var song in TestCorpus.Range(64))
        foreach (var span in song.Map.Sections)
        foreach (var (track, definition) in song.Song.TrackDefinitions.Where(x => x.Value is PitchInstrumentTrack))
        {
            var role = ((PitchInstrumentTrack)definition).Role;
            if (role is not (TrackRole.Bass or TrackRole.Chords or TrackRole.CounterMelody or TrackRole.Melody or TrackRole.Riff or TrackRole.Rhythm or TrackRole.Pad))
                continue;
            var notes = song.Song.Notes!.GetValueOrDefault(track)?.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9).ToArray() ?? [];
            if (notes.Length == 0)
                continue;
            foreach (var (bar, start, end) in span.Bars)
            {
                var key = (role, bar % 2);
                var (b, struck, sounding, downbeats) = counts.GetValueOrDefault(key);
                counts[key] = (b + 1, struck + (notes.Any(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9) ? 1 : 0),
                    sounding + (notes.Any(x => x.Position < end - 1e-9 && x.Position + x.Value.Duration > start + 1e-9) ? 1 : 0),
                    downbeats + (notes.Any(x => Math.Abs(x.Position - start) < 0.13) ? 1 : 0));
            }
        }

        foreach (var ((role, place), (bars, struck, sounding, downbeats)) in counts.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2))
            Console.WriteLine($"{role} {(place == 0 ? "first" : "second")} bars: {bars}, struck {struck / (double)bars:P1}, sounding {sounding / (double)bars:P1}, downbeat {downbeats / (double)bars:P1}");
        await Task.CompletedTask;
    }
}
