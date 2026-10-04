using System.Text.Json;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.WebApi.Genres;

namespace Rmg.Tests.WebApi;

public sealed class GenrePreviewTest
{
    private static GenrePreviewRequest Request(string choice, string options) =>
        new(choice, JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(options)!);

    [Test]
    public async Task APoolsShares_AreThoseTheSongsDraw_FromThePlainestToTheWildest()
    {
        var preview = GenrePreview.Of(Request("meter", """{ "4/4": [100, 80, 0], "7/8": [0, 20, 100] }"""), out var error)!;

        await Assert.That(error).IsNull();
        await Assert.That(preview.Conventionality).Count().IsEqualTo(GenrePreview.Points);
        await Assert.That(preview.Shares!["4/4"][0]).IsEqualTo(100).Within(1e-9);
        await Assert.That(preview.Shares!["7/8"][GenrePreview.Points / 2]).IsEqualTo(20).Within(1e-9);
        await Assert.That(preview.Shares!["7/8"][^1]).IsEqualTo(100).Within(1e-9);
        // and its weights eased between its three
        await Assert.That(preview.Curves["4/4"][GenrePreview.Points / 2]).IsEqualTo(80).Within(1e-9);
        // as the songs draw it: the engine's own weighing at every point
        var meters = Meter.Options.Select(x => (Meter.NameOf(x.Meter), x.Weight));
        Func<string, ByConvention?> pool = x => x == "4/4" ? new ByConvention(1, 0.8, 0) : x == "7/8" ? new ByConvention(0, 0.2, 1) : null;
        var weights = ByConvention.Weigh(meters, 0.7, pool);
        await Assert.That(preview.Shares!["7/8"][14]).IsEqualTo(100 * weights.Single(x => x.Value == "7/8").Weight / weights.Sum(x => x.Weight)).Within(1e-9);
    }

    [Test]
    public async Task AChancesAndATemposAmounts_AreTheirOwnAlong_AndWhatIsNotAChoice_SaysSo()
    {
        await Assert.That(GenrePreview.Of(Request("parts", """{ "pad": [0, 40, 90] }"""), out _)!.Curves["pad"][^1]).IsEqualTo(90).Within(1e-9);
        await Assert.That(GenrePreview.Of(Request("tempo", """{ "from": [80, 90, 100], "to": 140 }"""), out _)!.Curves["from"][0]).IsEqualTo(80).Within(1e-6);
        await Assert.That(GenrePreview.Of(Request("kazoo", """{ "a": 1 }"""), out var error)).IsNull();
        await Assert.That(error).IsNotNull();
    }
}
