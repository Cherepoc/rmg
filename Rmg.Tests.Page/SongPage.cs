using System.Diagnostics;
using System.Text.Json;
using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>A song asked of the server by the page: what it was asked with, and how it ended, null while on its way.</summary>
public sealed class SongRequest(JsonElement body)
{
    public JsonElement Body { get; } = body;

    /// <summary>The seed asked for, null for a random one.</summary>
    public string? Seed => Body.TryGetProperty("seed", out var seed) && seed.ValueKind == JsonValueKind.String ? seed.GetString() : null;

    /// <summary>A facet it was asked with, null for one left to the song to draw.</summary>
    public int? Facet(string facet) => Body.GetProperty("facets").TryGetProperty(facet, out var value) ? value.GetInt32() : null;

    /// <summary>The song's volume it was asked with, which is of the mix.</summary>
    public int Volume => Body.GetProperty("volume").GetInt32();

    /// <summary>When it was sent, and when it ended, by <see cref="Stopwatch.GetTimestamp" />.</summary>
    public long SentAt { get; } = Stopwatch.GetTimestamp();

    public long? EndedAt { get; private set; }

    /// <summary>The status it was answered with, "aborted" for one let go of, null while on its way.</summary>
    public string? Ended { get; private set; }

    public void End(string ended, long at)
    {
        if (Ended is not null) return;
        Ended = ended;
        EndedAt = at;
    }

    public override string ToString() => $"{Seed ?? "random"}: {Ended ?? "on its way"}";
}

/// <summary>
///     The player's page in a browser context of its own, with what it asks the server for songs watched, and slowed or
///     answered busy as a test asks: the song itself comes from the server, so only its timing is the test's.
/// </summary>
public sealed class SongPage : IAsyncDisposable
{
    private readonly IBrowserContext _context;
    private readonly List<SongRequest> _requests = [];
    private readonly Dictionary<IRequest, SongRequest> _byRequest = [];
    private readonly List<string> _errors = [];

    private SongPage(IBrowserContext context, IPage page)
    {
        _context = context;
        Page = page;
    }

    public IPage Page { get; }

    /// <summary>
    ///     Longer than the page waits after a change before it remakes the song (200 ms), so that a test showing no song
    ///     was asked for waits past it.
    /// </summary>
    public static readonly TimeSpan PastRemake = TimeSpan.FromMilliseconds(500);

    /// <summary>How long every song asked for is held back before it goes to the server.</summary>
    public TimeSpan Slow { get; set; } = TimeSpan.Zero;

    /// <summary>How many songs asked for are still to be answered busy, as the server answers one it has no room for.</summary>
    public int Busy { get; set; }

    /// <summary>Every song asked for so far, in order.</summary>
    public IReadOnlyList<SongRequest> Requests
    {
        get
        {
            lock (_requests) return [.._requests];
        }
    }

    /// <summary>What the page has written to its console as an error.</summary>
    public IReadOnlyList<string> Errors
    {
        get
        {
            lock (_errors) return [.._errors];
        }
    }

    /// <summary>
    ///     The page at <paramref name="path" />, on a phone's narrow touch screen when <paramref name="phone" />, and with
    ///     the admin area's credentials when <paramref name="admin" />.
    /// </summary>
    public static async Task<SongPage> Open(Site site, string path = "", bool phone = false, bool admin = false)
    {
        var options = new BrowserNewContextOptions { BaseURL = site.Address.ToString() };
        // the admin area's credentials, which the browser gives when the area asks for them
        if (admin)
            options.HttpCredentials = new HttpCredentials { Username = "admin", Password = Site.Token };
        if (phone)
        {
            options.ViewportSize = new ViewportSize { Width = 360, Height = 780 };
            options.IsMobile = true;
            options.HasTouch = true;
        }

        var context = await site.Browser.NewContextAsync(options);
        // the tests' own listening questions, whatever the page's are now
        await context.RouteAsync("**/listening.json", route => route.FulfillAsync(new RouteFulfillOptions { Path = Path.Combine(AppContext.BaseDirectory, "listening.json") }));
        var page = await context.NewPageAsync();
        var songPage = new SongPage(context, page);

        page.Console += (_, message) =>
        {
            // a resource that failed to load named by where it is, which its message leaves out
            if (message.Type == "error") lock (songPage._errors) songPage._errors.Add(message.Location is { Length: > 0 } where && message.Text.StartsWith("Failed to load resource") ? $"{message.Text} ({where})" : message.Text);
        };
        page.PageError += (_, error) =>
        {
            lock (songPage._errors) songPage._errors.Add(error);
        };
        page.RequestFailed += (_, request) => songPage.End(request, "aborted", Stopwatch.GetTimestamp());
        page.RequestFinished += async (_, request) =>
        {
            // when it finished, before the round trip that reads its status, which a request sent after it could pass
            var at = Stopwatch.GetTimestamp();
            // a request that finishes as its test closes the page has no one left to read it, and an event handler's
            // exception would end every test's run rather than this one
            try
            {
                var response = await request.ResponseAsync();
                songPage.End(request, response is null ? "aborted" : response.Status.ToString(), at);
            }
            catch (PlaywrightException) when (page.IsClosed)
            {
            }
        };

        await page.RouteAsync(Cdn.Pattern, Cdn.Serve);
        await page.RouteAsync("**/api/songs/generate", songPage.Answer);
        await page.GotoAsync(path);
        return songPage;
    }

    private async Task Answer(IRoute route)
    {
        var asked = new SongRequest(JsonDocument.Parse(route.Request.PostData ?? "{}").RootElement.Clone());
        lock (_requests)
        {
            _requests.Add(asked);
            _byRequest[route.Request] = asked;
        }

        if (Busy > 0)
        {
            Busy--;
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 503,
                Headers = new Dictionary<string, string> { ["Retry-After"] = "1" },
                ContentType = "application/problem+json",
                Body = "{}"
            });
            return;
        }

        if (Slow > TimeSpan.Zero) await Task.Delay(Slow);

        try
        {
            await route.ContinueAsync();
        }
        catch (PlaywrightException)
        {
            // let go of by the page while it was held back
        }
    }

    private void End(IRequest request, string ended, long at)
    {
        lock (_requests)
            if (_byRequest.TryGetValue(request, out var asked))
                asked.End(ended, at);
    }

    /// <summary>Waits until the condition holds, and fails saying what was asked for if it does not in time.</summary>
    public async Task Until(Func<Task<bool>> condition, string what, int seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"{what} did not happen within {seconds} s; songs asked for: {string.Join(", ", Requests)}");

            await Task.Delay(25);
        }
    }

    public Task Until(Func<bool> condition, string what, int seconds = 10) => Until(() => Task.FromResult(condition()), what, seconds);

    /// <summary>Waits for every song asked for to have ended.</summary>
    public Task Settled(int seconds = 10) => Until(() => Requests.All(x => x.Ended is not null), "every song asked for ending", seconds);

    /// <summary>The song on the page, by its seed.</summary>
    public Task<string> Seed() => Page.Locator("#seed").InputValueAsync();

    public Task<string> Status() => Page.Locator("#status").InnerTextAsync();

    public Task<bool> IsGenerateEnabled() => Page.Locator("#generate").IsEnabledAsync();

    /// <summary>Waits for the first song, which the page asks for the moment it opens.</summary>
    public Task FirstSong() => Until(async () => await Seed() != "" && await IsGenerateEnabled(), "the first song arriving");

    /// <summary>Asks for a new random song, and waits for it while nothing is held back.</summary>
    public async Task Generate()
    {
        var before = await Seed();
        await Page.Locator("#generate").ClickAsync();
        if (Slow == TimeSpan.Zero && Busy == 0)
            await Until(async () => await Seed() != before && await IsGenerateEnabled(), "a new song arriving");
    }

    /// <summary>The songs on the list, by their seeds, the latest first.</summary>
    public async Task<IReadOnlyList<string>> History() => await Page.Locator("button[data-entry]").EvaluateAllAsync<string[]>("xs => xs.map(x => x.dataset.seed)");

    public ILocator HistoryItem(string seed) => Page.Locator($"button[data-entry][data-seed='{seed}']");

    /// <summary>The songs asked for since the given count of them.</summary>
    public IReadOnlyList<SongRequest> Since(int count) => [..Requests.Skip(count)];

    /// <summary>Waits for a song asked for to be on its way, held back.</summary>
    public Task OnItsWay() => Until(() => Requests.Any(x => x.Ended is null), "a song on its way");

    /// <summary>Moves a setting's slider and lets go of it, as a hand would, wherever its panel is folded away.</summary>
    public Task SetSetting(string key, int value) => Page.EvaluateAsync(
        "([key, value]) => { const range = document.querySelector(`input[data-setting='${key}']`); range.value = String(value); range.dispatchEvent(new Event('input')); range.dispatchEvent(new Event('change')); }",
        new object[] { key, value });

    /// <summary>Moves the song's volume, which is of the mix and heard at once.</summary>
    public Task SetSongVolume(int value) => Page.EvaluateAsync(
        "value => { const range = document.getElementById('song-volume'); range.value = String(value); range.dispatchEvent(new Event('input')); }",
        value);

    /// <summary>The window of every genre, opened by its button once the genres are listed.</summary>
    public async Task<ILocator> Manager()
    {
        var manage = Page.Locator("button.genre-manage");
        await Until(() => manage.IsEnabledAsync(), "the genres listed");
        await manage.ClickAsync();
        var manager = Page.Locator("dialog.genre-manager");
        await Until(() => manager.IsVisibleAsync(), "the window of every genre open");
        return manager;
    }

    /// <summary>The editor, opened from the window of every genre by a button of a genre's line in a group, such as Duplicate of the preset Rock.</summary>
    public async Task<ILocator> FromManager(string group, string genre, string action)
    {
        var manager = await Manager();
        var line = manager.Locator($"ul[aria-label=\"{group}\"] li").Filter(new() { Has = Page.Locator(".genre-line-name", new() { HasTextRegex = new System.Text.RegularExpressions.Regex($"^{System.Text.RegularExpressions.Regex.Escape(genre)}$") }) });
        await line.GetByRole(AriaRole.Button, new() { Name = action, Exact = true }).ClickAsync();
        return await Editor();
    }

    /// <summary>The editor, opened on a genre made new, from the window of every genre.</summary>
    public async Task<ILocator> NewGenre()
    {
        var manager = await Manager();
        await manager.GetByRole(AriaRole.Button, new() { Name = "New genre", Exact = true }).ClickAsync();
        return await Editor();
    }

    private async Task<ILocator> Editor()
    {
        var editor = Page.Locator("dialog.genre-dialog");
        await Until(() => editor.IsVisibleAsync(), "the editor open");
        return editor;
    }

    /// <summary>How many of the settings are given, as the settings panel says.</summary>
    public async Task<string> Given() => await Page.Locator("#settings-state").TextContentAsync() ?? "";

    /// <summary>Waits until the player can be stopped, which wants the soundfont as well as the song.</summary>
    public Task Playable() => Until(() => Page.Locator("#stop").IsEnabledAsync(), "the song and the soundfont being ready");

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
