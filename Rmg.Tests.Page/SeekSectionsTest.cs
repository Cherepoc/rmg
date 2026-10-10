namespace Rmg.Tests.Page;

/// <summary>The sections on the seek bar: a mark where each starts, named on hover, and the song's length shown before it plays.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class SeekSectionsTest(Site site)
{
    [Test]
    public async Task ASongsSections_AreMarkedOnTheSeekBar_NamedOnHover_ItsLengthShownBeforePlay()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        var marks = page.Page.Locator("#seek-sections span");

        await page.Until(async () => await marks.CountAsync() > 0, "the sections marked");
        var lefts = await marks.EvaluateAllAsync<double[]>("xs => xs.map(x => parseFloat(x.style.left))");
        await Assert.That(lefts.All(x => x is > 0 and < 100)).IsTrue();
        await Assert.That(lefts.Zip(lefts.Skip(1)).All(x => x.Second > x.First)).IsTrue();
        await Assert.That(await page.Page.Locator("#total").TextContentAsync()).IsNotEqualTo("0:00");

        // the bar names the section under the pointer: past the first mark, the second section
        var box = (await page.Page.Locator("#seek").BoundingBoxAsync())!;
        // the marks set in as far as the thumb's middle travels, 8 pixels; halfway to the next mark, however short the second
        // section is
        var between = (lefts[0] + (lefts.Length > 1 ? lefts[1] : 100)) / 2;
        await page.Page.Mouse.MoveAsync((float)(box.X + 8 + (box.Width - 16) * between / 100), box.Y + box.Height / 2);
        var name = await marks.First.GetAttributeAsync("data-name");
        await page.Until(async () => await page.Page.Locator("#seek").GetAttributeAsync("title") == name, "the section under the pointer named");
        await Assert.That(page.Errors).IsEmpty();
    }
}
