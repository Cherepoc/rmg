using System.Text.Json;

namespace Rmg.WebApi.Genres;

/// <summary>A preset of the page's (<c>Client/genres.json</c>): its id, its name, its description and its choices as a request names them.</summary>
public sealed record GenrePreset(string Id, string Name, string Description, Dictionary<string, Dictionary<string, JsonElement>> Choices);

/// <summary>The page's presets, read as the page sends them (<c>genres.js</c>' flatChoices), every amount a whole percent, or three of them.</summary>
public static class GenrePresetFile
{
    /// <summary>The page's presets, as the server is built with them.</summary>
    public static IReadOnlyList<GenrePreset> All { get; } = Read();

    private static IReadOnlyList<GenrePreset> Read()
    {
        using var stream = typeof(GenrePresetFile).Assembly.GetManifestResourceStream("genres.json")
                           ?? throw new InvalidOperationException("The server is built without the page's genres.json.");
        var file = JsonSerializer.Deserialize<List<GenrePreset>>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return [..file.Select(x => x with { Choices = Flat(x.Choices) })];
    }

    /// <summary>
    ///     A preset's choices as the page sends them, from the file's families: an entry's member that is itself options a
    ///     family's choice, "rhythm": { "ground": ... } the choice "rhythm.ground", any other an option of the entry's own.
    /// </summary>
    public static Dictionary<string, Dictionary<string, JsonElement>> Flat(Dictionary<string, Dictionary<string, JsonElement>> choices)
    {
        var flat = new Dictionary<string, Dictionary<string, JsonElement>>();
        foreach (var (name, members) in choices)
        {
            if (members.Count == 0)
                flat[name] = [];
            foreach (var (member, value) in members)
                if (value.ValueKind == JsonValueKind.Object)
                    flat[$"{name}.{member}"] = value.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
                else
                    (flat.TryGetValue(name, out var own) ? own : flat[name] = [])[member] = value;
        }

        return flat;
    }
}
