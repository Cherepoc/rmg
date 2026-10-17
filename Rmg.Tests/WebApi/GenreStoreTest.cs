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

    [Test]
    public async Task ADay_KeepsNoMoreNewGenresThanItsCap_ButFindsTheKnownOnes()
    {
        var directory = Directory.CreateTempSubdirectory("rmg-genres");
        try
        {
            var store = GenreStore.Create(directory.FullName, dailyNew: 2);
            string Genre(int i) => GenreStore.Canonical($"Genre {i}", Choices("""{ "meter": { "4/4": 80 } }"""));
            await Assert.That(store.Keep(Genre(1))).IsNotNull();
            await Assert.That(store.Keep(Genre(2))).IsNotNull();
            await Assert.That(store.Keep(Genre(3))).IsNull();
            // one kept already is found whatever the cap
            await Assert.That(store.Keep(Genre(1))).IsEqualTo(GenreStore.KeyOf(Genre(1)));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Test]
    public async Task AKeptGenre_IsServedWithoutTheChoicesAndOptionsTakenOut()
    {
        var body = """{"format":1,"name":"Mine","choices":{"kickLock":{"sections":50},"push":{"never":10,"sections":50}}}""";
        await Assert.That(Rmg.WebApi.Genres.GenreEndpoints.Known(body)).IsEqualTo("""{"format":1,"name":"Mine","choices":{"push":{"sections":50}}}""");
    }

    /// <summary>A choice left with none of the options it named is left out, its songs playing it as the songs have it; one the genre names with none kept.</summary>
    [Test]
    public async Task AChoiceTakenOutWhole_IsLeftOut_OneNamedWithNoneKept()
    {
        var body = """{"format":1,"name":"Mine","choices":{"meter":{"5/3":100},"passingChords":{},"push":{"sections":50}}}""";
        await Assert.That(Rmg.WebApi.Genres.GenreEndpoints.Known(body)).IsEqualTo("""{"format":1,"name":"Mine","choices":{"passingChords":{},"push":{"sections":50}}}""");
    }
}
