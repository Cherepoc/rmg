using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.Meters;

/// <summary>
///     How songs play in other meters than four: the drums' notes a bar, where in the bar each drum role strikes in the
///     groove, and where the chords change.
/// </summary>
public sealed class MeterReportTest
{
    private const int SongCount = 32;

    [Test]
    [Explicit]
    [Arguments(new[] { 8, 8 })]
    [Arguments(new[] { 4, 4, 4 })]
    [Arguments(new[] { 6, 6 })]
    [Arguments(new[] { 4, 4, 4, 3 })]
    [Arguments(new[] { 4, 3, 3, 3 })]
    [Arguments(new[] { 4, 4, 6 })]
    [Arguments(new[] { 12, 8 })]
    public async Task Report(int[] groups)
    {
        var meter = new Meter([..groups]);
        int bars = 0, drums = 0;
        var roles = Enum.GetValues<DrumRole>().ToDictionary(x => x, _ => new SortedDictionary<double, int>());
        var chords = new SortedDictionary<double, int>();
        for (var seed = 0; seed < SongCount; seed++)
        {
            var song = TestCorpus.Get(seed, meter);
            var map = song.Map;
            await Assert.That(map.Meter).IsEqualTo(meter);
            var (start, end) = (map.Sections[0].Start, map.Sections[^1].End);
            bars += (int)Math.Round((end - start) / meter.BarDuration);
            drums += song.Drums.Count(x => x.Position >= start && x.Position < end);
            foreach (var track in DrumGroups.AllDrums.Select(DrumGroups.GetTrackNumber).Distinct())
            {
                if (!song.Song.Notes!.TryGetValue(track, out var notes))
                    continue;
                foreach (var note in notes.Where(x => x.Position >= start && x.Position < end && x.Value.State.GetStateValue(StateKinds.ArticulationIndex) == 0))
                {
                    // the section's role for the drum, or the song's where it keeps it
                    var span = map.Sections.Last(x => x.Start <= note.Position + 1e-9);
                    var changed = (System.Collections.Immutable.ImmutableDictionary<int, DrumRole>)song.Trace.First(e => e.Point == TracePoints.DrumRoles && e.Section == span.SectionId).Value!;
                    var role = changed.TryGetValue(track, out var r) ? r : (DrumRole)song.Song.TrackDefinitions[track].StateMap.GetStateValue(CompositionStateKinds.DrumRole).Value;
                    var place = Math.Round(map.BeatInBar(note.Position) * 4) / 4;
                    roles[role][place] = roles[role].GetValueOrDefault(place) + 1;
                }
            }

            foreach (var change in song.ChordChanges.Where(x => x >= start && x < end))
            {
                var place = Math.Round(map.BeatInBar(change) * 4) / 4;
                chords[place] = chords.GetValueOrDefault(place) + 1;
            }
        }

        string Places(SortedDictionary<double, int> counts) => string.Join(" ", counts.Where(x => x.Value / (double)bars >= 0.05).Select(x => $"{x.Key}:{x.Value / (double)bars:F2}"));
        Console.WriteLine($"{meter} ({meter.TimeSignature.Numerator}/{meter.TimeSignature.Denominator}): drums {drums / (double)bars:F1} a bar, {drums / (double)bars / meter.BarDuration:F2} a beat");
        foreach (var (role, counts) in roles)
            Console.WriteLine($"  {role,-9} {Places(counts)}");
        Console.WriteLine($"  chords    {Places(chords)}");
    }

    [Test]
    [Explicit]
    public async Task Meters()
    {
        // the meters drawn, by how unconventional the song's rhythm is
        var songs = TestCorpus.Range(512).Select(x => (Meter: x.Map.Meter, Rhythm: ((RhythmicUnconventionality)x.Trace.Single(e => e.Point == TracePoints.SongRhythm).Value!).Value)).ToArray();
        foreach (var band in songs.GroupBy(x => Math.Min(2, (int)(x.Rhythm * 3))).OrderBy(x => x.Key))
            Console.WriteLine($"rhythm {band.Key}/3: {string.Join(", ", band.GroupBy(x => x.Meter.TimeSignature).Select(x => $"{x.Key.Numerator}/{x.Key.Denominator} {x.Count() / (double)band.Count():P0}"))} of {band.Count()}");
        Console.WriteLine($"all: {string.Join(", ", songs.GroupBy(x => x.Meter.TimeSignature).Select(x => $"{x.Key.Numerator}/{x.Key.Denominator} {x.Count() / (double)songs.Length:P1}"))}");
        await Task.CompletedTask;
    }
}
