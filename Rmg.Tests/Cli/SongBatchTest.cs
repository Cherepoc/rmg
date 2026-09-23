using Rmg;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Cli;

public sealed class SongBatchTest
{
    private static string CreateTempDirectory() =>
        Path.Combine(Path.GetTempPath(), "rmg-tests-" + Guid.NewGuid().ToString("N"));

    private static void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        catch (IOException)
        {
        }
    }

    [Test]
    public async Task GenerateSongSeeds_ResultsIn_RequestedCount()
    {
        await Assert.That(SongBatch.GenerateSongSeeds(1, 0).Length).IsEqualTo(0);
        await Assert.That(SongBatch.GenerateSongSeeds(1, 5).Length).IsEqualTo(5);
    }

    [Test]
    public async Task GenerateSongSeeds_SameSeed_ResultsIn_SameSeeds()
    {
        await Assert.That(SongBatch.GenerateSongSeeds(77, 10).AsEnumerable())
            .IsEquivalentTo(SongBatch.GenerateSongSeeds(77, 10).AsEnumerable());
    }

    [Test]
    public async Task GenerateSongSeeds_SmallerBatch_IsPrefixOfLargerBatch()
    {
        var small = SongBatch.GenerateSongSeeds(77, 3);
        var large = SongBatch.GenerateSongSeeds(77, 8);

        await Assert.That(large.Take(3)).IsEquivalentTo(small.AsEnumerable());
    }

    [Test]
    public async Task GenerateSongSeeds_DifferentSeeds_ResultsIn_DifferentSequences()
    {
        var first = SongBatch.GenerateSongSeeds(1, 5);
        var second = SongBatch.GenerateSongSeeds(2, 5);

        await Assert.That(first.SequenceEqual(second)).IsFalse();
    }

    [Test]
    public async Task GenerateSongSeeds_SeedsInABatch_AreDistinct_AndNotTheBatchSeed()
    {
        var seeds = SongBatch.GenerateSongSeeds(1234, 50);

        await Assert.That(seeds.Distinct().Count()).IsEqualTo(50);
        await Assert.That(seeds.Contains(1234)).IsFalse();
    }

    [Test]
    public async Task Run_WritesOneFilePerSong_NamedBySongSeed()
    {
        var directory = CreateTempDirectory();
        try
        {
            var options = new CliOptions(directory, 3, 99);
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = SongBatch.Run(options, SongGenerator.GenerateSong, output, error);

            var expectedNames = SongBatch.GenerateSongSeeds(99, 3)
                .Select(SongFile.GetName)
                .ToArray();
            var actualNames = Directory.GetFiles(directory).Select(Path.GetFileName).ToArray();
            await Assert.That(exitCode).IsEqualTo(SongBatch.Success);
            await Assert.That(actualNames).IsEquivalentTo(expectedNames);
            await Assert.That(error.ToString()).IsEqualTo(string.Empty);
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    [Test]
    public async Task Run_CreatesMissingOutputDirectory_IncludingParents()
    {
        var root = CreateTempDirectory();
        var directory = Path.Combine(root, "a", "b");
        try
        {
            var exitCode = SongBatch.Run(
                new CliOptions(directory, 1, 5),
                SongGenerator.GenerateSong,
                new StringWriter(),
                new StringWriter()
            );

            await Assert.That(exitCode).IsEqualTo(SongBatch.Success);
            await Assert.That(Directory.GetFiles(directory).Length).IsEqualTo(1);
        }
        finally
        {
            DeleteQuietly(root);
        }
    }

    [Test]
    public async Task Run_WritesValidMidiFiles()
    {
        var directory = CreateTempDirectory();
        try
        {
            SongBatch.Run(new CliOptions(directory, 2, 5), SongGenerator.GenerateSong, new StringWriter(), new StringWriter());

            foreach (var file in Directory.GetFiles(directory))
            {
                var header = File.ReadAllBytes(file).Take(4).ToArray();
                await Assert.That(header).IsEquivalentTo(new byte[] { 0x4D, 0x54, 0x68, 0x64 });
            }
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    [Test]
    public async Task Run_SameOptions_ResultsIn_IdenticalFiles()
    {
        var directory1 = CreateTempDirectory();
        var directory2 = CreateTempDirectory();
        try
        {
            SongBatch.Run(new CliOptions(directory1, 3, 2024), SongGenerator.GenerateSong, new StringWriter(), new StringWriter());
            SongBatch.Run(new CliOptions(directory2, 3, 2024), SongGenerator.GenerateSong, new StringWriter(), new StringWriter());

            var files1 = Directory.GetFiles(directory1).Order().ToArray();
            var files2 = Directory.GetFiles(directory2).Order().ToArray();
            await Assert.That(files1.Length).IsEqualTo(3);
            await Assert.That(files1.Select(Path.GetFileName)).IsEquivalentTo(files2.Select(Path.GetFileName));
            for (var i = 0; i < files1.Length; i++)
                await Assert.That(File.ReadAllBytes(files1[i])).IsEquivalentTo(File.ReadAllBytes(files2[i]));
        }
        finally
        {
            DeleteQuietly(directory1);
            DeleteQuietly(directory2);
        }
    }

    [Test]
    public async Task Run_PrintsBatchSeed()
    {
        var directory = CreateTempDirectory();
        try
        {
            var output = new StringWriter();

            SongBatch.Run(new CliOptions(directory, 1, 31337), SongGenerator.GenerateSong, output, new StringWriter());

            await Assert.That(output.ToString().Contains("31337")).IsTrue();
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    [Test]
    public async Task Run_FailingSong_IsReported_AndTheRestIsStillGenerated()
    {
        var directory = CreateTempDirectory();
        try
        {
            var songSeeds = SongBatch.GenerateSongSeeds(11, 3);
            var failingSeed = songSeeds[1];
            var error = new StringWriter();

            var exitCode = SongBatch.Run(
                new CliOptions(directory, 3, 11),
                seed => seed == failingSeed
                    ? throw new InvalidOperationException("boom")
                    : SongGenerator.GenerateSong(seed),
                new StringWriter(),
                error
            );

            var expectedNames = new[]
            {
                SongFile.GetName(songSeeds[0]),
                SongFile.GetName(songSeeds[2]),
            };
            await Assert.That(exitCode).IsEqualTo(SongBatch.SongsFailed);
            await Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName)).IsEquivalentTo(expectedNames);
            await Assert.That(error.ToString().Contains(failingSeed.ToString())).IsTrue();
            await Assert.That(error.ToString().Contains("boom")).IsTrue();
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    [Test]
    public async Task Run_OutputPathIsAFile_ResultsIn_InvalidOutput()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "not-a-directory");
        File.WriteAllText(filePath, "x");
        try
        {
            var error = new StringWriter();

            var exitCode = SongBatch.Run(
                new CliOptions(filePath, 1, 1),
                SongGenerator.GenerateSong,
                new StringWriter(),
                error
            );

            await Assert.That(exitCode).IsEqualTo(SongBatch.InvalidOutput);
            await Assert.That(error.ToString().Contains(filePath)).IsTrue();
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }
}
