using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Playwright;
using TUnit.Core.Interfaces;

namespace Rmg.Tests.Page;

/// <summary>
///     The server as it is published, started once for every test on a port of its own, with its analytics in a
///     directory of their own and the small soundfont, and a headless Chromium to drive its pages: Playwright's own,
///     its headless shell installed on the first run, so that a test does not depend on what browser the machine has,
///     and its sans-serif font Liberation Sans, so that it does not depend on the machine's fonts either.
/// </summary>
public sealed class Site : IAsyncInitializer, IAsyncDisposable
{
    /// <summary>The admin area's password on this server.</summary>
    public const string Token = "page-tests-admin-token";

    private readonly StringBuilder _output = new();
    private readonly string _state = Path.Combine(Path.GetTempPath(), $"rmg-page-{Path.GetRandomFileName()}");
    private Process? _server;
    private IPlaywright? _playwright;

    public Uri Address { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (Microsoft.Playwright.Program.Main(["install", "--only-shell", "chromium"]) != 0)
            throw new InvalidOperationException("Playwright could not install its Chromium.");

        Address = new Uri($"http://127.0.0.1:{FreePort()}/");
        _server = StartServer(Address.Port);
        await WaitForServer();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true, Env = Fonts() });
    }

    /// <summary>
    ///     The browser's environment, the tests' own with fonts.conf as its fonts: the page's sans-serif and system-ui
    ///     Liberation Sans, as wide as Arial, rather than the machine's default, which on Ubuntu is DejaVu Sans, wide
    ///     enough to wrap the phone's lines that a phone's Roboto keeps whole.
    /// </summary>
    private static Dictionary<string, string> Fonts()
    {
        var config = Path.Combine(AppContext.BaseDirectory, "fonts.conf");
        var check = Process.Start(new ProcessStartInfo("fc-list", ["Liberation Sans"]) { RedirectStandardOutput = true, Environment = { ["FONTCONFIG_FILE"] = config } })!;
        var found = check.StandardOutput.ReadToEnd();
        check.WaitForExit();
        if (string.IsNullOrWhiteSpace(found))
            throw new InvalidOperationException("The page tests want Liberation Sans (fonts-liberation on Debian and Ubuntu), the browser's sans-serif font.");
        var environment = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().ToDictionary(x => (string)x.Key, x => (string)x.Value!);
        environment["FONTCONFIG_FILE"] = config;
        return environment;
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();

        if (_server is { HasExited: false })
        {
            _server.Kill(true);
            await _server.WaitForExitAsync();
        }

        if (Directory.Exists(_state)) Directory.Delete(_state, true);
    }

    /// <summary>What the server has written, which a server that would not start is told by.</summary>
    private string Output
    {
        get
        {
            lock (_output) return _output.ToString();
        }
    }

    private Process StartServer(int port)
    {
        // the test project's build has built the server's beside it, in the same configuration
        var testOutput = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        var configuration = testOutput.Parent!.Name;
        var project = Path.Combine(Root().FullName, "Rmg.WebApi");
        var assembly = Path.Combine(project, "bin", configuration, testOutput.Name, "Rmg.WebApi.dll");
        if (!File.Exists(assembly)) throw new FileNotFoundException($"The server is not built: {assembly}", assembly);

        // the dotnet this runs on, three folders above its runtime
        var dotnet = Path.Combine(new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()).Parent!.Parent!.Parent!.FullName, "dotnet");

        var start = new ProcessStartInfo(dotnet, [assembly])
        {
            WorkingDirectory = project,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment =
            {
                ["RMG_PORT"] = port.ToString(),
                ["RMG_STATEDIRECTORY"] = _state,
                ["RMG_ANALYTICSENABLED"] = "true",
                // as the test instance, whose songs the listening mode asks for with stages left out
                ["RMG_LISTENING"] = "true",
                // every page of the tests one visitor, whose events a day of them would run past the day's few
                ["RMG_EVENTSPERVISITORPERDAY"] = "100000",
                ["RMG_DASHBOARDTOKEN"] = Token,
                // a sixth of the size of the one the page offers first, as every test's page fetches it
                ["RMG_DEFAULTSOUNDFONT"] = "TimGM6mb.sf2"
            }
        };

        var server = Process.Start(start) ?? throw new InvalidOperationException("The server did not start.");
        server.OutputDataReceived += (_, line) => Keep(line.Data);
        server.ErrorDataReceived += (_, line) => Keep(line.Data);
        server.BeginOutputReadLine();
        server.BeginErrorReadLine();
        return server;
    }

    private void Keep(string? line)
    {
        if (line is null) return;
        lock (_output) _output.AppendLine(line);
    }

    private async Task WaitForServer()
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (_server!.HasExited) throw new InvalidOperationException($"The server stopped:\n{Output}");

            try
            {
                if ((await client.GetAsync(new Uri(Address, "api/version"))).IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
                // not listening yet
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"The server did not answer within 30 s:\n{Output}");
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>The repository the tests are built in.</summary>
    public static DirectoryInfo Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Rmg.slnx")))
                return directory;

        throw new DirectoryNotFoundException("No Rmg.slnx above the tests.");
    }
}
