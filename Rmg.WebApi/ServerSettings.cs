using System.Collections.Immutable;
using System.Net;

namespace Rmg.WebApi;

/// <summary>What to listen on.</summary>
public enum Interfaces
{
    /// <summary>Every interface, which is what a machine on your own network wants.</summary>
    Every,

    /// <summary>127.0.0.1 and ::1 only, which is what anything behind a reverse proxy wants.</summary>
    Loopback,

    /// <summary>One address of your own.</summary>
    One
}

/// <summary>
///     Everything this server is told before it starts, read from one place and checked once.
///     <para>
///         The values live in <c>appsettings.json</c>, where they are visible and have sensible defaults.
///         The environment beats that, as <c>RMG_PORT</c> and the like, which is how a deploy sets the
///         ones that differ per machine and how a secret stays out of a file that is in the repository.
///         The command line beats both, as <c>--port 8080</c>, for the once-off.
///     </para>
/// </summary>
/// <param name="Port">The port to listen on.</param>
/// <param name="Interfaces">Which interfaces to listen on.</param>
/// <param name="Address">The one address, when <paramref name="Interfaces" /> says so, and null otherwise.</param>
/// <param name="KnownProxies">Reverse proxies to believe about a request, beyond loopback, which always is.</param>
/// <param name="SoundFontDirectory">Where the soundfonts are, or null for the one under the web root.</param>
/// <param name="DefaultSoundFont">Which one the page takes unasked, or null for the first it is offered.</param>
/// <param name="StateDirectory">Where the analytics are kept, which is the only thing this writes.</param>
/// <param name="IsCounting">Whether anything is counted at all.</param>
/// <param name="DashboardToken">What the analytics summary asks for, or null to not serve it at all.</param>
/// <param name="RetentionDays">How long an event is kept before it is thrown away.</param>
/// <param name="EventsPerVisitorPerDay">How much one visitor may write in a day before being ignored.</param>
public sealed record ServerSettings(
    int Port,
    Interfaces Interfaces,
    IPAddress? Address,
    ImmutableArray<IPAddress> KnownProxies,
    string? SoundFontDirectory,
    string? DefaultSoundFont,
    string StateDirectory,
    bool IsCounting,
    string? DashboardToken,
    int RetentionDays,
    int EventsPerVisitorPerDay
)
{
    public const int DefaultPort = 5000;
    public const int DefaultRetentionDays = 180;
    public const int DefaultEventsPerVisitorPerDay = 300;

    /// <summary>
    ///     Reads the settings, answering false and saying why when one of them is not a setting at all.
    ///     Nothing here throws: a typo in a config file should be a line on stderr, not a stack trace.
    /// </summary>
    /// <param name="defaultStateDirectory">Where the analytics go when nothing says otherwise.</param>
    public static bool TryRead(
        IConfiguration configuration,
        string defaultStateDirectory,
        out ServerSettings? settings,
        out string? error)
    {
        settings = null;

        if (!TryReadPort(Value(configuration, "Port"), out var port, out error)) return false;
        if (!TryReadInterfaces(Value(configuration, "Address"), out var interfaces, out var address, out error)) return false;
        if (!TryReadProxies(Value(configuration, "KnownProxies"), out var proxies, out error)) return false;
        if (!TryReadWhole(Value(configuration, "RetentionDays"), "RetentionDays", "days to keep events",
                DefaultRetentionDays, out var retention, out error)) return false;
        if (!TryReadWhole(Value(configuration, "EventsPerVisitorPerDay"), "EventsPerVisitorPerDay",
                "events for one visitor in a day", DefaultEventsPerVisitorPerDay, out var perVisitor, out error)) return false;
        if (!TryReadCounting(Value(configuration, "AnalyticsEnabled"), out var isCounting, out error)) return false;

        settings = new ServerSettings(
            port,
            interfaces,
            address,
            proxies,
            Value(configuration, "SoundFontDirectory"),
            Value(configuration, "DefaultSoundFont"),
            Value(configuration, "StateDirectory") ?? defaultStateDirectory,
            isCounting,
            Value(configuration, "DashboardToken"),
            retention,
            perVisitor
        );

        return true;
    }

    /// <summary>A setting nobody filled in reads as one nobody set, so a blank in a file means the default.</summary>
    private static string? Value(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool TryReadPort(string? value, out int port, out string? error)
    {
        port = DefaultPort;
        error = null;

        if (value is null) return true;
        if (int.TryParse(value, out port) && port is >= 1 and <= 65535) return true;

        error = $"\"{value}\" is not a port. Ports are 1 to 65535.";
        return false;
    }

    private static bool TryReadInterfaces(string? value, out Interfaces interfaces, out IPAddress? address, out string? error)
    {
        interfaces = Interfaces.Every;
        address = null;
        error = null;

        if (value is null || value.Equals("any", StringComparison.OrdinalIgnoreCase)) return true;

        if (value.Equals("loopback", StringComparison.OrdinalIgnoreCase))
        {
            interfaces = Interfaces.Loopback;
            return true;
        }

        if (IPAddress.TryParse(value, out address))
        {
            interfaces = Interfaces.One;
            return true;
        }

        error = $"\"{value}\" is not an address. Use \"any\", \"loopback\" or an IP address.";
        return false;
    }

    private static bool TryReadProxies(string? value, out ImmutableArray<IPAddress> proxies, out string? error)
    {
        proxies = [];
        error = null;

        if (value is null) return true;

        var read = ImmutableArray.CreateBuilder<IPAddress>();
        foreach (var proxy in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!IPAddress.TryParse(proxy, out var known))
            {
                error = $"\"{proxy}\" is not an IP address. KnownProxies is a comma separated list of them.";
                return false;
            }

            read.Add(known);
        }

        proxies = read.ToImmutable();
        return true;
    }

    private static bool TryReadWhole(string? value, string key, string what, int fallback, out int read, out string? error)
    {
        read = fallback;
        error = null;

        if (value is null) return true;
        if (int.TryParse(value, out read) && read >= 1) return true;

        error = $"\"{value}\" is not a number of {what}. {key} is a whole number of at least 1.";
        return false;
    }

    private static bool TryReadCounting(string? value, out bool isCounting, out string? error)
    {
        isCounting = true;
        error = null;

        if (value is null || bool.TryParse(value, out isCounting)) return true;

        error = $"\"{value}\" is not a yes or a no. AnalyticsEnabled is true or false.";
        return false;
    }
}
