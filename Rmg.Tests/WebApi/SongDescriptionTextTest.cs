using Rmg.Core.Composition;
using Rmg.WebApi.Admin;

namespace Rmg.Tests.WebApi;

public sealed class SongDescriptionTextTest
{
    private static (string Text, Rmg.Core.Songs.SongDescription Description) Describe(int seed)
    {
        var song = TestCorpus.Get(seed);
        var description = SongDescriptions.Of(song.Song, song.Rendered, song.Trace);
        return (SongDescriptionText.ToMarkdown(description, "abc", "6xyz", "0.7.003"), description);
    }

    [Test]
    public async Task TheText_HasItsTitle_ItsGlance_ItsSound_ItsForm_AndItsMelody_InThatOrder()
    {
        var (text, description) = Describe(1);
        string[] headings = ["# Song abc (songs 0.7.003)", "At a glance:", "## Sound", "## Form:", "## Melody"];
        var places = headings.Select(x => text.IndexOf(x, StringComparison.Ordinal)).ToArray();

        await Assert.That(places.All(x => x >= 0)).IsTrue();
        await Assert.That(places).IsInOrder();
        await Assert.That(text).Contains("Settings: 6xyz");
        await Assert.That(text).Contains(description.Key);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var seed in new[] { 1, 28 })
            Console.WriteLine(Describe(seed).Text);
        await Task.CompletedTask;
    }

    [Test]
    public async Task EverySection_IsARow_ItsChordsOnceAndThenAsWhereTheyWereHeard()
    {
        foreach (var seed in Enumerable.Range(0, 8))
        {
            var (text, description) = Describe(seed);
            var rows = text.Split('\n').Where(x => x.StartsWith("| ") && !x.StartsWith("| # ")).ToArray();

            await Assert.That(rows.Length).IsEqualTo(description.Sections.Length);
            foreach (var (row, section) in rows.Zip(description.Sections))
                await Assert.That(row.StartsWith($"| {section.Place} | {section.Name} | {section.Bars} | {section.Energy} | ")).IsTrue();
            // a progression spelled out once, and pointed back to after
            var spelled = rows.Select(x => x.Split(" | ")[4]).Where(x => x != "—" && !x.StartsWith("as in ")).ToArray();
            await Assert.That(spelled.Distinct().Count()).IsEqualTo(spelled.Length);
        }
    }
}
