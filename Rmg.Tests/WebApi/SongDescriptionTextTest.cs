using Rmg.Core.Composition;
using Rmg.WebApi.Admin;

namespace Rmg.Tests.WebApi;

public sealed class SongDescriptionTextTest
{
    private static (string Text, Rmg.Core.Songs.SongDescription Description) Describe(int seed)
    {
        var song = TestCorpus.Get(seed);
        var description = SongDescriptions.Of(song.Song, song.Rendered, song.Trace);
        return (SongDescriptionText.ToMarkdown(description, "abc", "6xyz", "Dub", "0.7.003"), description);
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
        await Assert.That(text).Contains("Genre: Dub");
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
                await Assert.That(row.StartsWith($"| {section.Place} | {section.Name} | {section.Bars} | {section.Energy} | {section.Density} | ")).IsTrue();
            // a progression spelled out once, and pointed back to after
            var shown = rows.Select(x => x.Split(" | ")[5]).ToArray();
            var spelled = shown.Where(x => x != "—" && !x.StartsWith("as in ")).ToArray();
            await Assert.That(spelled.Distinct().Count()).IsEqualTo(spelled.Length);
            // each pointing back to an earlier row that spells the chords it plays, as a section of the same chords does
            var chords = description.Sections.Select(SongDescriptionText.Chords).ToArray();
            for (var i = 0; i < shown.Length; i++)
                if (shown[i].StartsWith("as in ") && int.Parse(shown[i]["as in ".Length..]) is var place)
                {
                    var earlier = Array.FindIndex(description.Sections.ToArray(), x => x.Place == place);
                    await Assert.That(earlier).IsBetween(0, i - 1).Because($"seed {seed}, row {i + 1}");
                    await Assert.That(chords[earlier]).IsEqualTo(chords[i]).Because($"seed {seed}, row {i + 1}");
                    await Assert.That(shown[earlier].StartsWith("as in ")).IsFalse().Because($"seed {seed}, row {i + 1}");
                }
        }
    }
}
