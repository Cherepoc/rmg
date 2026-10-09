using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>A riser into a drop, on a channel of its own, mixed on the page as the part that carries it, its sound its own.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class RiserTest(Site site)
{
    [Test]
    public async Task ARiser_MutedWithItsPart_KeepsItsSound_WhenThePartsInstrumentChanges()
    {
        await using var page = await SongPage.Open(site, $"?song=1&settings=B{new string('0', 70)}&genre=techno");
        // every message the page sends its synthesizer, as it can be read
        await page.Page.AddInitScriptAsync("""
            window.posted = [];
            const post = MessagePort.prototype.postMessage;
            MessagePort.prototype.postMessage = function (message, ...rest) {
                try { window.posted.push(JSON.stringify(message)); } catch { }
                return post.call(this, message, ...rest);
            };
            """);
        // a Techno song whose pad carries a riser
        string? report = null;
        await page.Page.RouteAsync("**/api/songs/generate", async route =>
        {
            var body = JsonNode.Parse(route.Request.PostData ?? "{}")!.AsObject();
            body["seed"] = "0";
            var response = await route.FetchAsync(new RouteFetchOptions { PostData = System.Text.Encoding.UTF8.GetBytes(body.ToJsonString()) });
            report = response.Headers["x-song-settings"];
            await route.FulfillAsync(new RouteFulfillOptions { Response = response });
        });
        await page.Page.ReloadAsync();
        await page.FirstSong();
        await page.Until(() => Task.FromResult(report is not null), "the song's report");

        var pad = JsonDocument.Parse(report!).RootElement.GetProperty("parts").GetProperty("pad");
        await Assert.That(pad.GetProperty("riser").ValueKind).IsEqualTo(JsonValueKind.Number).Because(report!);
        var (own, riser) = (pad.GetProperty("channel").GetInt32() - 1, pad.GetProperty("riser").GetInt32() - 1);
        await Assert.That(riser).IsNotEqualTo(own);

        // the pad muted: its riser with it
        var before = await page.Page.EvaluateAsync<int>("() => window.posted.length");
        await page.Page.EvaluateAsync("() => document.querySelector(\"#mixer tr.part button.listen[aria-label='Mute the pad']\").click()");
        await page.Until(async () => await page.Page.EvaluateAsync<int>("() => window.posted.length") > before, "the synthesizer told");
        var muted = await page.Page.EvaluateAsync<string[]>("before => window.posted.slice(before).filter(x => x.includes('isMuted'))", before);
        foreach (var channel in new[] { own, riser })
            await Assert.That(muted.Any(x => x.Contains($"\"channelNumber\":{channel},") && x.Contains("true"))).IsTrue().Because($"channel {channel} in {string.Join(" ", muted)}");

        // the pad's instrument given: its own channel takes it, the riser keeps the reverse cymbal
        before = await page.Page.EvaluateAsync<int>("() => window.posted.length");
        await page.Page.EvaluateAsync("""
            () => {
                const row = document.querySelector("#mixer tr.part button.listen[aria-label='Mute the pad']").closest("tr");
                const given = row.querySelector("input[type=checkbox]");
                if (!given.checked) given.click();
                const select = row.querySelector("select");
                select.value = [...select.options].find(x => x.value !== select.value).value;
                select.dispatchEvent(new Event("change"));
            }
            """);
        await page.Until(async () => await page.Page.EvaluateAsync<int>("() => window.posted.length") > before, "the synthesizer told");
        // a program change as the synthesizer takes it, a MIDI message of its channel's status byte
        var posted = await page.Page.EvaluateAsync<string[]>("before => window.posted.slice(before)", before);
        bool Changed(int channel) => posted.Any(x => x.Contains($"\"messageData\":{{\"0\":{0xC0 + channel},"));
        await Assert.That(Changed(own)).IsTrue().Because(string.Join(" ", posted));
        await Assert.That(Changed(riser)).IsFalse().Because(string.Join(" ", posted));
        await Assert.That(page.Errors).IsEmpty();
    }
}
