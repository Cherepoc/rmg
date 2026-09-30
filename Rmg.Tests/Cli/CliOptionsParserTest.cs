using Rmg;

namespace Rmg.Tests.Cli;

public sealed class CliOptionsParserTest
{
    private const ulong DefaultSeed = 4242;

    private static CliParseResult Parse(params string[] args) => CliOptionsParser.Parse(args, () => DefaultSeed);

    [Test]
    public async Task NoArguments_ResultsIn_Defaults()
    {
        var result = Parse();

        await Assert.That(result.Error).IsNull();
        await Assert.That(result.ShowHelp).IsFalse();
        await Assert.That(result.Options).IsEqualTo(new CliOptions(CliOptionsParser.DefaultOutputDirectory, 1, DefaultSeed));
    }

    [Test]
    public async Task NoSeed_ResultsIn_SeedFromFactory_AndFactoryIsNotCalledWhenSeedIsPassed()
    {
        var calls = 0;

        var withoutSeed = CliOptionsParser.Parse([], () => { calls++; return 7UL; });
        var withSeed = CliOptionsParser.Parse(["-s", "9"], () => { calls++; return 7UL; });

        await Assert.That(withoutSeed.Options!.Seed).IsEqualTo(7UL);
        await Assert.That(withSeed.Options!.Seed).IsEqualTo(9UL);
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    public async Task NoSeed_WithoutFactory_ResultsIn_ARandomSeed_OfAll64Bits()
    {
        var seeds = Enumerable.Range(0, 64).Select(_ => CliOptionsParser.Parse([]).Options!.Seed).ToArray();

        await Assert.That(seeds.Distinct().Count()).IsEqualTo(64);
        await Assert.That(seeds.Any(x => x > uint.MaxValue)).IsTrue();
    }

    [Test]
    public async Task LongOptions_AreParsed()
    {
        var result = Parse("--output", "out", "--count", "5", "--seed", "12");

        await Assert.That(result.Options).IsEqualTo(new CliOptions("out", 5, 66));
    }

    [Test]
    public async Task ShortOptions_AreParsed()
    {
        var result = Parse("-o", "out", "-n", "5", "-s", "12");

        await Assert.That(result.Options).IsEqualTo(new CliOptions("out", 5, 66));
    }

    [Test]
    public async Task EqualsSyntax_IsParsed()
    {
        var result = Parse("--output=some dir/x", "--count=3", "--seed=a8");

        await Assert.That(result.Options).IsEqualTo(new CliOptions("some dir/x", 3, 2312));
    }

    [Test]
    public async Task SeedBoundaries_AreParsed()
    {
        await Assert.That(Parse("-s", "0").Options!.Seed).IsEqualTo(0UL);
        await Assert.That(Parse("-s", Rmg.Core.Base64.FromSeed(ulong.MaxValue)).Options!.Seed).IsEqualTo(ulong.MaxValue);
    }

    [Test]
    [Arguments("-h")]
    [Arguments("--help")]
    [Arguments("-?")]
    public async Task HelpOption_ResultsIn_ShowHelp(string option)
    {
        var result = Parse("-n", "3", option);

        await Assert.That(result.ShowHelp).IsTrue();
        await Assert.That(result.Options).IsNull();
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    [Arguments("--version", CliCommand.Version)]
    [Arguments("--fingerprint", CliCommand.Fingerprint)]
    public async Task CommandOption_ResultsIn_ItsCommand(string option, CliCommand expected)
    {
        var result = Parse("-n", "3", option);

        await Assert.That(result.Command).IsEqualTo(expected);
        await Assert.That(result.Options).IsNull();
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    [Arguments("--count", "0")]
    [Arguments("--count", "-1")]
    [Arguments("--count", "abc")]
    [Arguments("--count", "1.5")]
    [Arguments("--count", "")]
    [Arguments("--seed", "a.c")]
    [Arguments("--seed", "zzzzzzzzzzzz")]
    [Arguments("--output", "")]
    [Arguments("--output", "   ")]
    public async Task InvalidValue_ResultsIn_Error(string option, string value)
    {
        var result = Parse(option, value);

        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Options).IsNull();
        await Assert.That(result.ShowHelp).IsFalse();
    }

    [Test]
    [Arguments("--output")]
    [Arguments("--count")]
    [Arguments("--seed")]
    [Arguments("-o")]
    [Arguments("-n")]
    [Arguments("-s")]
    public async Task MissingValue_ResultsIn_Error_NamingTheOption(string option)
    {
        var result = Parse(option);

        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Contains(option)).IsTrue();
    }

    [Test]
    public async Task UnknownOption_ResultsIn_Error_NamingTheOption()
    {
        var result = Parse("--nope");

        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Contains("--nope")).IsTrue();
    }

    [Test]
    public async Task PositionalArgument_ResultsIn_Error()
    {
        var result = Parse("songs");

        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Contains("songs")).IsTrue();
    }
}
