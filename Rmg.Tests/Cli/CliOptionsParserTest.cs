using Rmg;

namespace Rmg.Tests.Cli;

public sealed class CliOptionsParserTest
{
    private const int DefaultSeed = 4242;

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

        var withoutSeed = CliOptionsParser.Parse([], () => { calls++; return 7; });
        var withSeed = CliOptionsParser.Parse(["-s", "9"], () => { calls++; return 7; });

        await Assert.That(withoutSeed.Options!.Seed).IsEqualTo(7);
        await Assert.That(withSeed.Options!.Seed).IsEqualTo(9);
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    public async Task NoSeed_WithoutFactory_ResultsIn_NonNegativeSeed()
    {
        var result = CliOptionsParser.Parse([]);

        await Assert.That(result.Options!.Seed).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task LongOptions_AreParsed()
    {
        var result = Parse("--output", "out", "--count", "5", "--seed", "12");

        await Assert.That(result.Options).IsEqualTo(new CliOptions("out", 5, 12));
    }

    [Test]
    public async Task ShortOptions_AreParsed()
    {
        var result = Parse("-o", "out", "-n", "5", "-s", "12");

        await Assert.That(result.Options).IsEqualTo(new CliOptions("out", 5, 12));
    }

    [Test]
    public async Task EqualsSyntax_IsParsed()
    {
        var result = Parse("--output=some dir/x", "--count=3", "--seed=-8");

        await Assert.That(result.Options).IsEqualTo(new CliOptions("some dir/x", 3, -8));
    }

    [Test]
    public async Task NegativeSeed_AsSeparateArgument_IsParsed()
    {
        var result = Parse("--seed", "-5");

        await Assert.That(result.Options!.Seed).IsEqualTo(-5);
    }

    [Test]
    public async Task SeedBoundaries_AreParsed()
    {
        await Assert.That(Parse("-s", int.MaxValue.ToString()).Options!.Seed).IsEqualTo(int.MaxValue);
        await Assert.That(Parse("-s", int.MinValue.ToString()).Options!.Seed).IsEqualTo(int.MinValue);
    }

    [Test]
    public async Task RepeatedOption_LastOneWins()
    {
        var result = Parse("-n", "2", "-n", "4");

        await Assert.That(result.Options!.Count).IsEqualTo(4);
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
    [Arguments("--count", "0")]
    [Arguments("--count", "-1")]
    [Arguments("--count", "abc")]
    [Arguments("--count", "1.5")]
    [Arguments("--count", "")]
    [Arguments("--seed", "abc")]
    [Arguments("--seed", "99999999999")]
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
