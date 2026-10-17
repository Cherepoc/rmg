using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>
///     The song as the system shows it, on a lock screen and in the media keys' panel (<c>media-session.js</c>), and each
///     part's pulse in the mixer (<c>pulse.js</c>).
/// </summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class MediaSessionTest(Site site)
{
    // every action the page hands the system kept where a test can press it, as a media key would
    private const string KeepHandlers = """
        window.mediaHandlers = {};
        const set = navigator.mediaSession.setActionHandler.bind(navigator.mediaSession);
        navigator.mediaSession.setActionHandler = (action, handler) => { window.mediaHandlers[action] = handler; set(action, handler); };
        """;

    private static Task<string> State(SongPage page) => page.Page.EvaluateAsync<string>("() => navigator.mediaSession.playbackState");

    [Test]
    public async Task ThePlayingSong_IsShownByItsSeed_AndTheMediaKeysPlayPauseAndGoOn()
    {
        await using var page = await SongPage.Open(site, script: KeepHandlers);
        await page.FirstSong();
        await page.Playable();
        var seed = await page.Seed();

        await page.Page.Locator("#play").ClickAsync();
        await page.Until(async () => await State(page) == "playing", "the system told the song plays");
        await Assert.That(await page.Page.EvaluateAsync<string>("() => navigator.mediaSession.metadata.title")).IsEqualTo($"Song {seed}");

        await page.Page.EvaluateAsync("() => window.mediaHandlers.pause()");
        await page.Until(async () => await State(page) == "paused", "the song paused by its key");
        await page.Page.EvaluateAsync("() => window.mediaHandlers.play()");
        await page.Until(async () => await State(page) == "playing", "the song played again by its key");

        // the next song, asked for as Next asks for it, and shown once it plays
        var asked = page.Requests.Count;
        await page.Page.EvaluateAsync("() => window.mediaHandlers.nexttrack()");
        await page.Until(async () => await page.Seed() != seed && await page.IsGenerateEnabled(), "the next song", 15);
        await Assert.That(page.Requests.Count).IsGreaterThan(asked);
        await page.Until(async () => await page.Page.EvaluateAsync<string>("() => navigator.mediaSession.metadata.title") == $"Song {await page.Seed()}", "the next song shown");
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>A part's pulse is a small glow while nothing plays, and grows as its part plays.</summary>
    [Test]
    public async Task APartsPulse_GrowsAsItPlays()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Playable();
        // how much of each pulse is drawn, by its pixels that are not clear
        const string drawn = "() => [...document.querySelectorAll('#mixer canvas.pulse')].map(c => { const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data; let n = 0; for (let i = 3; i < d.length; i += 4) if (d[i] > 8) n++; return n; })";
        var pulses = page.Page.Locator("#mixer canvas.pulse");
        await Assert.That(await pulses.CountAsync()).IsGreaterThan(0);
        await Assert.That(await pulses.First.GetAttributeAsync("aria-hidden")).IsEqualTo("true");
        var idle = await page.Page.EvaluateAsync<int[]>(drawn);
        // every one a glow, small, while nothing plays
        await Assert.That(idle.All(x => x > 0)).IsTrue();

        await page.Page.Locator("#play").ClickAsync();
        await page.Until(async () => (await page.Page.EvaluateAsync<int[]>(drawn)).Zip(idle).Any(x => x.First > x.Second * 2), "a pulse grown as its part plays", 15);
        await Assert.That(page.Errors).IsEmpty();
    }
}
