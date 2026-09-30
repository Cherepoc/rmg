using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongStructures;

/// <summary>Which parts the sections leave out, by their energy and their role, and how often a section keeps its harmony.</summary>
public sealed class ArrangementReportTest
{
    [Test]
    public async Task ARestingPart_PlaysNoNotes_AndASectionKeepsItsHarmony()
    {
        foreach (var song in TestCorpus.Range(32))
        foreach (var span in song.Map.Sections)
        {
            var resting = song.Resting(span);
            await Assert.That(resting.Contains(TrackRole.Bass) && resting.Contains(TrackRole.Chords)).IsFalse();
            // but the drums' fill in its last bar, where they come back into the next section
            foreach (var (track, notes) in song.Song.Notes!.Where(x => resting.Contains(song.Song.TrackDefinitions[x.Key].Role)))
            {
                var end = song.Song.TrackDefinitions[track].Role == TrackRole.Drum ? span.End - song.Map.Meter.BarDuration : span.End - 1;
                await Assert.That(notes.Count(x => x.Position >= span.Start + 1e-9 && x.Position < end - 1e-9)).IsEqualTo(0)
                    .Because($"seed {song.Seed}, track {track} at {span.Start}");
            }
        }
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var rows = new List<(double Energy, SectionRole Role, ImmutableHashSet<TrackRole> Resting)>();
        foreach (var song in TestCorpus.Range(256))
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var energy = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            rows.AddRange(song.Trace.Where(x => x.Point == TracePoints.Arrangement)
                .Select(x => (energy[x.Section], structure.Roles[x.Section], (ImmutableHashSet<TrackRole>)x.Value!)));
        }

        foreach (var part in new[] { TrackRole.Drum, TrackRole.Bass, TrackRole.Chords, TrackRole.Melody, TrackRole.Pad, TrackRole.CounterMelody })
        {
            string Share(IEnumerable<(double Energy, SectionRole Role, ImmutableHashSet<TrackRole> Resting)> band)
            {
                var b = band.ToArray();
                return $"{b.Count(x => x.Resting.Contains(part)) / (double)Math.Max(1, b.Length):P0}";
            }

            Console.WriteLine($"{part} rests in {Share(rows)} of {rows.Count} sections: the quieter half {Share(rows.Where(x => x.Energy < 0))}, the louder {Share(rows.Where(x => x.Energy >= 0))}; " +
                              string.Join(", ", rows.GroupBy(x => x.Role).OrderBy(x => x.Key).Select(x => $"{x.Key} {Share(x)}")));
        }

        // the parts a recurring section plays its first time and its last, as parts only join
        var (first, last, recurring, grown) = (0.0, 0.0, 0, 0);
        foreach (var song in TestCorpus.Range(256))
        foreach (var group in song.Map.Sections.GroupBy(x => x.SectionId).Where(x => x.Count() > 1))
        {
            var (f, l) = (6 - song.Resting(group.First()).Count, 6 - song.Resting(group.Last()).Count);
            (first, last, recurring, grown) = (first + f, last + l, recurring + 1, grown + (l > f ? 1 : 0));
        }

        Console.WriteLine($"{recurring} recurring sections play {first / recurring:F2} parts the first time and {last / recurring:F2} the last; {grown} grow");
        await Task.CompletedTask;
    }
}
