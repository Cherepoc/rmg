using System.Collections.Immutable;
using System.Text.Json;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Genres;

/// <summary>The page's presets (<c>Rmg.WebApi/Client/genres.json</c>), as the page sends them, every amount a whole percent, or three of them.</summary>
internal static class GenrePresets
{
    internal sealed record Preset(string Id, string Name, string Description, Dictionary<string, Dictionary<string, JsonElement>> Choices);

    public static IReadOnlyList<Preset> All { get; } = Read();

    private static IReadOnlyList<Preset> Read() => [..Rmg.WebApi.Genres.GenrePresetFile.All.Select(x => new Preset(x.Id, x.Name, x.Description, x.Choices))];

    /// <summary>A preset's genre, as a request reads it (<see cref="Rmg.WebApi.Songs.GenerateSongRequest.Genre" />); what is wrong with it fails.</summary>
    public static Genre Genre(Preset preset) =>
        Rmg.WebApi.Genres.GenrePresetFile.Genre(new Rmg.WebApi.Genres.GenrePreset(preset.Id, preset.Name, preset.Description, preset.Choices));

    public static Genre Genre(string id) => Genre(All.Single(x => x.Id == id));

    /// <summary>A preset with one of its choices set to the options given, each at its chance at every end, the choice's others not played.</summary>
    public static Genre With(string id, GenreChoice choice, params (string Option, double Chance)[] options)
    {
        var genre = Genre(id);
        return genre with { Choices = genre.Choices.SetItem(choice.Name, options.ToImmutableDictionary(x => x.Option, x => new ByConvention(x.Chance, x.Chance, x.Chance))) };
    }

    /// <summary>A preset without one of its choices, which its songs then take at the songs' own amounts: the same songs with its device as the songs have it.</summary>
    public static Genre Without(string id, string choice)
    {
        var preset = All.Single(x => x.Id == id);
        return Genre(preset with { Choices = preset.Choices.Where(x => x.Key != choice).ToDictionary() });
    }
}
