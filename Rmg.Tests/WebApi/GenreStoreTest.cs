using System.Text.Json;
using Rmg.WebApi.Genres;

namespace Rmg.Tests.WebApi;

public sealed class GenreStoreTest
{
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonElement>> Choices(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json)!
            .ToDictionary(x => x.Key, x => (IReadOnlyDictionary<string, JsonElement>)x.Value);

    [Test]
    public async Task AGenre_IsWrittenOneWayWhateverItsOrder_AndKeptByItsKey()
    {
        var first = GenreStore.Canonical("Dub", Choices("""{ "scale": { "major": 40, "dorian": [0, 60, 100] }, "meter": { "4/4": [80, 80, 80] } }"""));
        var second = GenreStore.Canonical("Dub", Choices("""{ "meter": { "4/4": 80 }, "scale": { "dorian": [0, 60, 100], "major": 40 } }"""));

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first).IsEqualTo("""{"format":1,"name":"Dub","choices":{"meter":{"4/4":80},"scale":{"dorian":[0,60,100],"major":40}}}""");
        await Assert.That(GenreStore.KeyOf(first)).Length().IsEqualTo(GenreStore.KeyLength);
        // another name, another key
        await Assert.That(GenreStore.KeyOf(GenreStore.Canonical("Dubs", Choices("""{ "meter": { "4/4": 80 } }""")))).IsNotEqualTo(GenreStore.KeyOf(first));

        var directory = Directory.CreateTempSubdirectory("rmg-genres");
        try
        {
            var store = GenreStore.Create(directory.FullName);
            var key = store.Keep(first);
            await Assert.That(key).IsEqualTo(GenreStore.KeyOf(first));
            await Assert.That(store.Keep(second)).IsEqualTo(key);
            await Assert.That(store.Find(key!)).IsEqualTo(first);
            await Assert.That(store.Find("0000000000A")).IsNull();
            await Assert.That(store.Find("not a key")).IsNull();
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
