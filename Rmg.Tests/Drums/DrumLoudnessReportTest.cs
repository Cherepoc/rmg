using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>How loud the drums play against the pitched parts, as rendered, in the calm genres and a band's.</summary>
public sealed class DrumLoudnessReportTest
{
    [Test]
    [Explicit]
    [Arguments("ambient")]
    [Arguments("medieval")]
    [Arguments("dungeon")]
    [Arguments("orchestral")]
    [Arguments("pop")]
    [Arguments("rock")]
    public async Task Report(string preset)
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))).ToArray();
        foreach (var job in Enum.GetValues<DrumJob>())
        {
            var of = songs.Where(x => (DrumJob)x.Trace.First(t => t.Point == TracePoints.DrumJob).Value! == job).ToArray();
            if (of.Length == 0)
                continue;
            var drums = of.SelectMany(x => x.Drums).Select(x => x.Value.Velocity).Order().ToArray();
            var pitched = of.SelectMany(x => x.Rendered.Tracks.Where(t => !t.IsPercussionInstrument && !t.IsDouble).SelectMany(t => t.NoteTimeline)).Select(x => x.Value.Velocity).Order().ToArray();
            double At(double[] x, double share) => x.Length == 0 ? 0 : 127 * x[(int)(share * (x.Length - 1))];
            Console.WriteLine($"{preset} {job} ({of.Length} songs): drums {drums.Length} notes, median {At(drums, 0.5):F0}, 90th {At(drums, 0.9):F0}, loudest {At(drums, 1):F0}; " +
                              $"pitched median {At(pitched, 0.5):F0}, 90th {At(pitched, 0.9):F0}");
        }

        await Task.CompletedTask;
    }

    /// <summary>The drums marking the form play under the parts they colour, as a calm piece's cymbal or timpani does (<see cref="DrumMarks" />).</summary>
    [Test]
    public async Task TheMarks_PlayUnderThePitchedParts()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("ambient"))))
            .Where(x => (DrumJob)x.Trace.First(t => t.Point == TracePoints.DrumJob).Value! == DrumJob.Marks).ToArray();
        double Median(IEnumerable<double> velocities) => velocities.Order().ToArray() is var x && x.Length > 0 ? 127 * x[x.Length / 2] : 0;
        var marks = Median(songs.SelectMany(x => x.Drums).Select(x => x.Value.Velocity));
        var pitched = Median(songs.SelectMany(x => x.Rendered.Tracks.Where(t => !t.IsPercussionInstrument && !t.IsDouble).SelectMany(t => t.NoteTimeline)).Select(x => x.Value.Velocity));
        // against the mechanism breaking (measured: 54 against 66 in Ambient, from 79 where they struck as the drum's loudest)
        Console.WriteLine($"{songs.Length} songs marking: the marks' median velocity {marks:F0}, the pitched parts' {pitched:F0}");
        await Assert.That(songs.Length).IsGreaterThan(16);
        await Assert.That(marks).IsLessThan(pitched);
    }
}
