using Rmg;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Core.Versions;
using Rmg.WebApi.Genres;

var parseResult = CliOptionsParser.Parse(args);

if (parseResult.ShowHelp)
{
    Console.Out.WriteLine(CliOptionsParser.Usage);
    return 0;
}

if (parseResult.Command == CliCommand.Version)
{
    Console.Out.WriteLine($"RMG {SongsVersion.Number}{(SongsVersion.Commit is { } commit ? $" ({commit})" : "")}");
    return 0;
}

if (parseResult.Command == CliCommand.Fingerprint)
{
    Console.Out.WriteLine(SongFingerprint.Compute(SongFingerprint.CorpusSize, GenrePresetFile.Genres()));
    return 0;
}

if (parseResult.Command == CliCommand.FingerprintSongs)
{
    foreach (var line in SongFingerprint.ComputeBySong(SongFingerprint.CorpusSize, GenrePresetFile.Genres()))
        Console.Out.WriteLine(line);
    return 0;
}

if (parseResult.Options is null)
{
    Console.Error.WriteLine(parseResult.Error);
    Console.Error.WriteLine();
    Console.Error.WriteLine(CliOptionsParser.Usage);
    return 2;
}

// the songs in the preset asked for, or their own
var genre = parseResult.Options.Genre is { } id ? GenrePresetFile.Genre(GenrePresetFile.All.Single(x => x.Id == id)) : null;
return SongBatch.Run(parseResult.Options, seed => SongGenerator.GenerateSong(seed, new SongOverrides(Genre: genre)), Console.Out, Console.Error);
