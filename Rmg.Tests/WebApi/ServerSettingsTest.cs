using Microsoft.Extensions.Configuration;
using Rmg.WebApi;

namespace Rmg.Tests.WebApi;

public sealed class ServerSettingsTest
{
    private const string Fallback = "/somewhere/else";

    private static ServerSettings Read(params (string Key, string? Value)[] values)
    {
        var read = TryRead(values, out var settings, out var error);

        if (!read) throw new InvalidOperationException($"expected these settings to be read, but: {error}");
        return settings!;
    }

    private static bool TryRead(
        (string Key, string? Value)[] values,
        out ServerSettings? settings,
        out string? error)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

        return ServerSettings.TryRead(configuration, Fallback, out settings, out error);
    }

    [Test]
    public async Task NothingSetIsEveryDefault()
    {
        var settings = Read();

        await Assert.That(settings.Port).IsEqualTo(ServerSettings.DefaultPort);
        await Assert.That(settings.Interfaces).IsEqualTo(Interfaces.Every);
        await Assert.That(settings.Address).IsNull();
        await Assert.That(settings.KnownProxies).IsEmpty();
        await Assert.That(settings.StateDirectory).IsEqualTo(Fallback);
        await Assert.That(settings.DashboardToken).IsNull();
        await Assert.That(settings.RetentionDays).IsEqualTo(ServerSettings.DefaultRetentionDays);
        await Assert.That(settings.SoundFontDirectory).IsNull();
        await Assert.That(settings.DefaultSoundFont).IsNull();
        await Assert.That(settings.IsCounting).IsTrue();
        await Assert.That(settings.EventsPerVisitorPerDay).IsEqualTo(ServerSettings.DefaultEventsPerVisitorPerDay);
    }

    [Test]
    public async Task TheSoundFontSettingsAreRead()
    {
        var settings = Read(("SoundFontDirectory", "/srv/soundfonts"), ("DefaultSoundFont", "TimGM6mb.sf2"));

        await Assert.That(settings.SoundFontDirectory).IsEqualTo("/srv/soundfonts");
        await Assert.That(settings.DefaultSoundFont).IsEqualTo("TimGM6mb.sf2");
    }

    [Test]
    [Arguments("false", false)]
    [Arguments("False", false)]
    [Arguments("true", true)]
    public async Task CountingCanBeTurnedOff(string value, bool expected)
    {
        await Assert.That(Read(("AnalyticsEnabled", value)).IsCounting).IsEqualTo(expected);
    }

    [Test]
    public async Task SomethingThatIsNotAYesOrNoSaysSo()
    {
        var read = TryRead([("AnalyticsEnabled", "sometimes")], out _, out var error);

        await Assert.That(read).IsFalse();
        await Assert.That(error).IsEqualTo("\"sometimes\" is not a yes or a no. AnalyticsEnabled is true or false.");
    }

    [Test]
    [Arguments("0")]
    [Arguments("nope")]
    public async Task AVisitorCeilingThatIsNotANumberSaysSo(string value)
    {
        var read = TryRead([("EventsPerVisitorPerDay", value)], out _, out var error);

        await Assert.That(read).IsFalse();
        await Assert.That(error).Contains("EventsPerVisitorPerDay is a whole number");
    }

    /// <summary>A blank in appsettings.json is a setting nobody filled in, not a setting of "".</summary>
    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task ABlankIsTheSameAsSayingNothing(string blank)
    {
        var settings = Read(("Port", blank), ("Address", blank), ("KnownProxies", blank),
            ("StateDirectory", blank), ("DashboardToken", blank), ("RetentionDays", blank),
            ("SoundFontDirectory", blank), ("DefaultSoundFont", blank), ("AnalyticsEnabled", blank),
            ("EventsPerVisitorPerDay", blank));

        await Assert.That(settings.Port).IsEqualTo(ServerSettings.DefaultPort);
        await Assert.That(settings.Interfaces).IsEqualTo(Interfaces.Every);
        await Assert.That(settings.StateDirectory).IsEqualTo(Fallback);
        await Assert.That(settings.DashboardToken).IsNull();
        await Assert.That(settings.RetentionDays).IsEqualTo(ServerSettings.DefaultRetentionDays);
        await Assert.That(settings.SoundFontDirectory).IsNull();
        await Assert.That(settings.DefaultSoundFont).IsNull();
        await Assert.That(settings.IsCounting).IsTrue();
    }

    [Test]
    public async Task EverythingSetIsEverythingRead()
    {
        var settings = Read(
            ("Port", "8080"),
            ("Address", "10.0.0.9"),
            ("KnownProxies", "10.0.0.5, 10.0.0.6"),
            ("StateDirectory", "/var/lib/rmg"),
            ("DashboardToken", "s3cret"),
            ("RetentionDays", "30"));

        await Assert.That(settings.Port).IsEqualTo(8080);
        await Assert.That(settings.Interfaces).IsEqualTo(Interfaces.One);
        // compared as text: IPAddress throws from ScopeId when an assertion reflects over its properties
        await Assert.That(settings.Address?.ToString()).IsEqualTo("10.0.0.9");
        await Check.That(settings.KnownProxies.Select(proxy => proxy.ToString())).IsEquivalentTo(new[] { "10.0.0.5", "10.0.0.6" });
        await Assert.That(settings.StateDirectory).IsEqualTo("/var/lib/rmg");
        await Assert.That(settings.DashboardToken).IsEqualTo("s3cret");
        await Assert.That(settings.RetentionDays).IsEqualTo(30);
    }

    [Test]
    [Arguments("any", Interfaces.Every)]
    [Arguments("ANY", Interfaces.Every)]
    [Arguments("loopback", Interfaces.Loopback)]
    [Arguments("Loopback", Interfaces.Loopback)]
    public async Task TheInterfacesAreNamedWhicheverWayRound(string value, Interfaces expected)
    {
        var settings = Read(("Address", value));

        await Assert.That(settings.Interfaces).IsEqualTo(expected);
        await Assert.That(settings.Address).IsNull();
    }

    [Test]
    [Arguments("0")]
    [Arguments("65536")]
    [Arguments("-1")]
    [Arguments("http")]
    [Arguments("80.5")]
    public async Task APortThatIsNotAPortSaysSo(string value)
    {
        var read = TryRead([("Port", value)], out var settings, out var error);

        await Assert.That(read).IsFalse();
        await Assert.That(settings).IsNull();
        await Assert.That(error).IsEqualTo($"\"{value}\" is not a port. Ports are 1 to 65535.");
    }

    [Test]
    public async Task AnAddressThatIsNotAnAddressSaysSo()
    {
        var read = TryRead([("Address", "nonsense")], out _, out var error);

        await Assert.That(read).IsFalse();
        await Assert.That(error).IsEqualTo("\"nonsense\" is not an address. Use \"any\", \"loopback\" or an IP address.");
    }

    [Test]
    public async Task AProxyThatIsNotAnAddressSaysWhichOne()
    {
        var read = TryRead([("KnownProxies", "10.0.0.5,nope")], out _, out var error);

        await Assert.That(read).IsFalse();
        await Assert.That(error).IsEqualTo("\"nope\" is not an IP address. KnownProxies is a comma separated list of them.");
    }

    [Test]
    [Arguments("0")]
    [Arguments("-3")]
    [Arguments("forever")]
    public async Task ARetentionThatIsNotDaysSaysSo(string value)
    {
        var read = TryRead([("RetentionDays", value)], out _, out var error);

        await Assert.That(read).IsFalse();
        await Assert.That(error).Contains("is not a number of days");
    }

    [Test]
    public async Task IPv6IsAnAddressToo()
    {
        var settings = Read(("Address", "::1"), ("KnownProxies", "::1"));

        await Assert.That(settings.Interfaces).IsEqualTo(Interfaces.One);
        await Assert.That(settings.Address?.ToString()).IsEqualTo("::1");
        await Check.That(settings.KnownProxies.Select(proxy => proxy.ToString())).IsEquivalentTo(new[] { "::1" });
    }

    /// <summary>What the layering is for: a file for the defaults, the environment for this machine.</summary>
    [Test]
    public async Task WhatComesLastWins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Port", "5000"),
                                    new KeyValuePair<string, string?>("DashboardToken", "")])
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Port", "8080"),
                                    new KeyValuePair<string, string?>("DashboardToken", "from the environment")])
            .Build();

        ServerSettings.TryRead(configuration, Fallback, out var settings, out _);

        await Assert.That(settings!.Port).IsEqualTo(8080);
        await Assert.That(settings.DashboardToken).IsEqualTo("from the environment");
    }
}
