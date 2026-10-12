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
        // a Techno song whose pad carries a riser, the first seed's that does, whichever it is as the songs are now
        string? report = null;
        string? seed = null;
        // the songs the route has answered, which the page's own requests, routed past, do not show
        var answered = 0;
        await page.Page.RouteAsync("**/api/songs/generate", async route =>
        {
            var body = JsonNode.Parse(route.Request.PostData ?? "{}")!.AsObject();
            IAPIResponse? response = null;
            foreach (var tried in seed is null ? Enumerable.Range(0, 64).Select(x => x.ToString()) : [seed])
            {
                body["seed"] = tried;
                response = await route.FetchAsync(new RouteFetchOptions { PostData = System.Text.Encoding.UTF8.GetBytes(body.ToJsonString()) });
                report = response.Headers["x-song-settings"];
                if (JsonDocument.Parse(report).RootElement.GetProperty("parts").GetProperty("pad").TryGetProperty("riser", out var carried) && carried.ValueKind == JsonValueKind.Number)
                {
                    seed = tried;
                    break;
                }
            }

            await route.FulfillAsync(new RouteFulfillOptions { Response = response });
            Interlocked.Increment(ref answered);
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
            await Assert.That(muted.Any(x => x.Contains($"\"channelNumber\":{channel},") && x.Contains("\"parameter\":\"isMuted\",\"value\":true"))).IsTrue().Because($"channel {channel} in {string.Join(" ", muted)}");

        // the pad's instrument given: its own channel takes it, the riser keeps the reverse cymbal
        before = await page.Page.EvaluateAsync<int>("() => window.posted.length");
        var answeredBefore = Volatile.Read(ref answered);
        var chosen = await page.Page.EvaluateAsync<int>("""
            () => {
                const row = document.querySelector("#mixer tr.part button.listen[aria-label='Mute the pad']").closest("tr");
                const given = row.querySelector("input[type=checkbox]");
                if (!given.checked) given.click();
                const select = row.querySelector("select");
                select.value = [...select.options].find(x => x.value !== select.value).value;
                select.dispatchEvent(new Event("change"));
                return Number(select.value);
            }
            """);
        // a program change to the instrument chosen, as the synthesizer takes it, a MIDI message of its channel's status
        // byte and the program; the riser's own, which a song made again sends it, no change to it
        bool ChangedTo(IEnumerable<string> posted, int channel) => posted.Any(x => x.Contains($"\"messageData\":{{\"0\":{0xC0 + channel},\"1\":{chosen}}}"));
        await page.Until(async () => ChangedTo(await page.Page.EvaluateAsync<string[]>("before => window.posted.slice(before)", before), own), "the pad's channel given the instrument");
        // and the song made again, answered and loaded: its own program change of the pad's channel, after the one the change
        // sent at once
        await page.Until(() => Task.FromResult(Volatile.Read(ref answered) > answeredBefore), "the song made again");
        await page.Until(async () => (await page.Page.EvaluateAsync<string[]>("before => window.posted.slice(before)", before))
            .Count(x => x.Contains($"\"messageData\":{{\"0\":{0xC0 + own},\"1\":{chosen}}}")) >= 2, "the song made again loaded");
        var posted = await page.Page.EvaluateAsync<string[]>("before => window.posted.slice(before)", before);
        await Assert.That(ChangedTo(posted, riser)).IsFalse().Because(string.Join(" ", posted));
        await Assert.That(page.Errors).IsEmpty();
    }
}
