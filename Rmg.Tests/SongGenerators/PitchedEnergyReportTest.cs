using Rmg.Core.Composition;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     How far the pitched tracks follow their sections' energy within a song: the correlation of a section's energy
///     against its song's with the notes a bar its melody, chords and bass play and its melody's mean pitch, each against
///     its song's, over the sections that play, in plain sections and in wild ones.
/// </summary>
public sealed class PitchedEnergyReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var rows = new List<(double Energy, double Rhythm, double Melody, double Chords, double Bass, double Pitch)>();
        foreach (var song in TestCorpus.Range(200))
        {
            var energy = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            var answers = song.Trace.Where(x => x.Point == TracePoints.MelodyAnswer).ToDictionary(x => x.Section, x => (double)x.Value!);
            var notes = song.Song.Notes!;
            var melody = notes.GetValueOrDefault(SongTracks.MelodyTrack)?.ToArray() ?? [];
            if (melody.Length == 0)
                continue;
            var songPitch = melody.Average(x => x.Value.Pitches[0]);
            var songRows = new List<(double Energy, double Rhythm, double Melody, double Chords, double Bass, double Pitch)>();
            foreach (var span in song.Map.Sections)
            {
                double PerBar(int track) => notes.GetValueOrDefault(track)?.Count(x => x.Position >= span.Start && x.Position < span.End) / (span.Duration / song.Map.Meter.BarDuration) ?? 0;
                var inSpan = melody.Where(x => x.Position >= span.Start && x.Position < span.End).ToArray();
                if (inSpan.Length == 0)
                    continue;
                var rhythm = 0.5 + Math.Log(answers[span.SectionId] / (1 - answers[span.SectionId])) / Math.Log(16);
                songRows.Add((energy[span.SectionId], rhythm, PerBar(SongTracks.MelodyTrack), PerBar(SongTracks.ChordsTrack), PerBar(SongTracks.BassTrack),
                    inSpan.Average(x => x.Value.Pitches[0]) - songPitch));
            }

            // every measure against its song's, so that what differs between songs leaves what differs between sections
            if (songRows.Count < 2)
                continue;
            var (e, m, c, b) = (songRows.Average(x => x.Energy), songRows.Average(x => x.Melody), songRows.Average(x => x.Chords), songRows.Average(x => x.Bass));
            rows.AddRange(songRows.Select(x => (x.Energy - e, x.Rhythm, x.Melody - m, x.Chords - c, x.Bass - b, x.Pitch)));
        }

        foreach (var (name, band) in new[] { ("plain", rows.Where(x => x.Rhythm < 0.5)), ("wild", rows.Where(x => x.Rhythm >= 0.5)) })
        {
            var b = band.ToArray();
            Console.WriteLine($"{name}, {b.Length} sections: energy with the melody's notes {Correlation(b, x => x.Melody):F2}, the chords' {Correlation(b, x => x.Chords):F2}, " +
                              $"the bass's {Correlation(b, x => x.Bass):F2}, the melody's pitch {Correlation(b, x => x.Pitch):F2}");
        }

        await Task.CompletedTask;
    }

    private static double Correlation(IReadOnlyList<(double Energy, double Rhythm, double Melody, double Chords, double Bass, double Pitch)> rows,
        Func<(double Energy, double Rhythm, double Melody, double Chords, double Bass, double Pitch), double> value)
    {
        var energies = rows.Select(x => x.Energy).ToArray();
        var values = rows.Select(value).ToArray();
        var (me, mv) = (energies.Average(), values.Average());
        var covariance = energies.Zip(values, (e, v) => (e - me) * (v - mv)).Sum();
        return covariance / Math.Sqrt(energies.Sum(e => (e - me) * (e - me)) * values.Sum(v => (v - mv) * (v - mv)));
    }
}
