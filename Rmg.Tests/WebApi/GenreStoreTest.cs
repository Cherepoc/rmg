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
            var key = store.Keep(first, null);
            await Assert.That(key).IsEqualTo(GenreStore.KeyOf(first));
            await Assert.That(store.Keep(second, null)).IsEqualTo(key);
            await Assert.That(store.Find(key!)).IsEqualTo(first);
            await Assert.That(store.Find("0000000000A")).IsNull();
            await Assert.That(store.Find("not a key")).IsNull();
        }
        finally
        {
            directory.Delete(true);
        }
    }

    /// <summary>A day keeps no more bytes of new genres than its budget, and a network no more new genres than its share, another network's kept.</summary>
    [Test]
    public async Task ADay_KeepsNoMoreBytesThanItsBudget_NorANetworkMoreThanItsShare()
    {
        var directory = Directory.CreateTempSubdirectory("rmg-genres");
        try
        {
            string Genre(int i) => GenreStore.Canonical($"Genre {i}", Choices("""{ "meter": { "4/4": 80 } }"""));
            var bytes = System.Text.Encoding.UTF8.GetByteCount(Genre(1));
            var store = GenreStore.Create(directory.FullName, dailyBytes: 2 * bytes + 1);
            await Assert.That(store.Keep(Genre(1), null)).IsNotNull();
            await Assert.That(store.Keep(Genre(2), null)).IsNotNull();
            await Assert.That(store.Keep(Genre(3), null)).IsNull();

            var networks = GenreStore.Create(directory.FullName + "-networks", networkNew: 2);
            var (one, other) = (System.Net.IPAddress.Parse("203.0.113.1"), System.Net.IPAddress.Parse("198.51.100.1"));
            await Assert.That(networks.Keep(Genre(1), one)).IsNotNull();
            await Assert.That(networks.Keep(Genre(2), one)).IsNotNull();
            await Assert.That(networks.Keep(Genre(3), one)).IsNull();
            await Assert.That(networks.Keep(Genre(3), other)).IsNotNull();
            // one kept already found whatever its network's share
            await Assert.That(networks.Keep(Genre(1), one)).IsEqualTo(GenreStore.KeyOf(Genre(1)));
            Directory.Delete(directory.FullName + "-networks", true);
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
            await Assert.That(store.Keep(Genre(1), null)).IsNotNull();
            await Assert.That(store.Keep(Genre(2), null)).IsNotNull();
            await Assert.That(store.Keep(Genre(3), null)).IsNull();
            // one kept already is found whatever the cap
            await Assert.That(store.Keep(Genre(1), null)).IsEqualTo(GenreStore.KeyOf(Genre(1)));
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

    /// <summary>A pool left with options that all weigh nothing is left out too, as no song could draw from it, so that its link still plays.</summary>
    [Test]
    public async Task APoolLeftWeighingNothing_IsLeftOut()
    {
        var body = """{"format":1,"name":"Mine","choices":{"meter":{"5/3":100,"4/4":0,"3/4":[0,0,0]},"push":{"sections":50}}}""";
        await Assert.That(Rmg.WebApi.Genres.GenreEndpoints.Known(body)).IsEqualTo("""{"format":1,"name":"Mine","choices":{"push":{"sections":50}}}""");
    }
}
