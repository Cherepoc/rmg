using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>A doubled driven guitar mixed on the page as one part, every take of it with it.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class DoubledPartTest(Site site)
{
    private const string Seed = "1";

    [Test]
    public async Task ADoubledPart_MutedHere_MutesEveryTakeOfIt()
    {
        await using var page = await SongPage.Open(site);
        // every message the page sends its synthesizer, as it can be read
        await page.Page.AddInitScriptAsync("""
            window.posted = [];
            const post = MessagePort.prototype.postMessage;
            MessagePort.prototype.postMessage = function (message, ...rest) {
                try { window.posted.push(JSON.stringify(message)); } catch { }
                return post.call(this, message, ...rest);
            };
            """);
        // the songs asked for with the riff, its twin and the chords on a distortion guitar, which a mix doubles
        string? report = null;
        await page.Page.RouteAsync("**/api/songs/generate", async route =>
        {
            var body = JsonNode.Parse(route.Request.PostData ?? "{}")!.AsObject();
            // a song whose twin plays, which a riff in power chords throughout would leave out
            body["seed"] = Seed;
            var parts = body["parts"]!.AsObject();
            foreach (var part in new[] { "riff", "chords" })
            {
                parts[part]!["plays"] = true;
                parts[part]!["instrument"] = 30;
            }

            parts["riffTwin"] = new JsonObject { ["plays"] = true, ["instrument"] = 30, ["volume"] = parts["riff"]!["volume"]!.DeepClone() };
            var response = await route.FetchAsync(new RouteFetchOptions { PostData = System.Text.Encoding.UTF8.GetBytes(body.ToJsonString()) });
            report = response.Headers["x-song-settings"];
            await route.FulfillAsync(new RouteFulfillOptions { Response = response });
        });
        await page.Page.ReloadAsync();
        await page.FirstSong();
        await page.Until(() => Task.FromResult(report is not null), "the song's report");

        var parts = JsonDocument.Parse(report!).RootElement.GetProperty("parts");
        int[] Takes(string part) =>
        [
            ..new[] { "channel", "double" }.Select(x => parts.GetProperty(part).GetProperty(x)).Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32() - 1)
        ];
        var riff = Takes("riff").Concat(Takes("riffTwin")).ToArray();
        await Assert.That(riff.Length).IsEqualTo(4).Because(report!);

        // the riff muted: every one of its four channels, its own, its second take's, its twin's and the twin's second
        var before = await page.Page.EvaluateAsync<int>("() => window.posted.length");
        await page.Page.EvaluateAsync("() => document.querySelector(\"#mixer tr.part button.listen[aria-label='Mute the riff']\").click()");
        await page.Until(async () => await page.Page.EvaluateAsync<int>("() => window.posted.length") > before, "the synthesizer told");
        var muted = await page.Page.EvaluateAsync<string[]>("before => window.posted.slice(before).filter(x => x.includes('isMuted'))", before);
        foreach (var channel in riff)
            await Assert.That(muted.Any(x => x.Contains($"\"channelNumber\":{channel},") && x.Contains("true"))).IsTrue().Because($"channel {channel} in {string.Join(" ", muted)}");
        await Assert.That(page.Errors).IsEmpty();
    }
}
