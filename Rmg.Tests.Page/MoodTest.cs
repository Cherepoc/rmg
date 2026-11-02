namespace Rmg.Tests.Page;

/// <summary>A song's mood among the settings, its darkness and its intensity: drawn, shown as the song drew them, or given and asked for.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class MoodTest(Site site)
{
    [Test]
    [Arguments("darkness")]
    [Arguments("intensity")]
    public async Task AMood_ShowsAsTheSongDrewIt_AndGiven_IsAskedFor(string key)
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Page.EvaluateAsync("() => document.getElementById('composition-panel').open = true");

        // drawn, the song's own, as its settings answered it, and not asked for
        await page.Until(() => page.Requests[^1].Answered is not null, "the song's settings answered");
        var drawn = page.Requests[^1].Answered!.Value.GetProperty(key).GetProperty("value").GetInt32();
        var slider = page.Page.Locator($"input[data-setting='{key}']");
        await page.Until(async () => await slider.InputValueAsync() == drawn.ToString(), "the drawn mood shown");
        await Assert.That(page.Requests[^1].Body.TryGetProperty(key, out _)).IsFalse();

        // given at its darkest or fiercest, and asked for, the song's answer saying it was given
        await page.SetSetting(key, 61);
        await page.Until(() => page.Requests[^1].Body.TryGetProperty(key, out var given) && given.GetInt32() == 61, "the song asked for with the mood given");
        await page.Until(() => page.Requests[^1].Answered is not null, "the given song's settings answered");
        await Assert.That(page.Requests[^1].Answered!.Value.GetProperty(key).GetProperty("isGiven").GetBoolean()).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }
}
