using Rmg;
using Rmg.Core.Composition;

var parseResult = CliOptionsParser.Parse(args);

if (parseResult.ShowHelp)
{
    Console.Out.WriteLine(CliOptionsParser.Usage);
    return 0;
}

if (parseResult.Options is null)
{
    Console.Error.WriteLine(parseResult.Error);
    Console.Error.WriteLine();
    Console.Error.WriteLine(CliOptionsParser.Usage);
    return 2;
}

return SongBatch.Run(parseResult.Options, SongGenerator.GenerateSong, Console.Out, Console.Error);
