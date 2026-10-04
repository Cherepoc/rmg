using System.Text.Json;
using Rmg.Core.Composition;

namespace Rmg.Tests.Genres;

/// <summary>The page's presets (<c>Rmg.WebApi/Client/genres.json</c>), as the page sends them, every amount a whole percent, or three of them.</summary>
internal static class GenrePresets
{
    internal sealed record Preset(string Id, string Name, string Description, Dictionary<string, Dictionary<string, JsonElement>> Choices);

    public static IReadOnlyList<Preset> All { get; } = Read();

    private static IReadOnlyList<Preset> Read()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Rmg.WebApi", "Client", "genres.json")))
            directory = directory.Parent;
        if (directory is null)
            throw new FileNotFoundException("The page's genres.json is not above the tests.");

        var text = File.ReadAllText(Path.Combine(directory.FullName, "Rmg.WebApi", "Client", "genres.json"));
        return JsonSerializer.Deserialize<List<Preset>>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    /// <summary>A preset's genre, as a request reads it (<see cref="Rmg.WebApi.Songs.GenerateSongRequest.Genre" />); what is wrong with it fails.</summary>
    public static Genre Genre(Preset preset) =>
        new Rmg.WebApi.Songs.GenerateSongRequest(Genre: preset.Choices.ToDictionary(x => x.Key, x => (IReadOnlyDictionary<string, JsonElement>)x.Value))
            .TryRead(out _, out var overrides, out _, out var error)
            ? overrides.Genre!
            : throw new InvalidOperationException($"{preset.Name}: {error}");

    public static Genre Genre(string id) => Genre(All.Single(x => x.Id == id));
}
