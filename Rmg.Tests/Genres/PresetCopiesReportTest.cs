using Rmg.Core.Composition;

namespace Rmg.Tests.Genres;

/// <summary>
///     Every preset amount equal to the songs' own, a copy a chances choice forces where naming one option switches the
///     others off: a retune of the songs' amounts leaves these behind, so that it shows them to retune with it.
/// </summary>
public sealed class PresetCopiesReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var preset in GenrePresets.All)
        {
            var genre = GenrePresets.Genre(preset);
            var copies = GenreChoices.All.Where(x => genre.Choices.ContainsKey(x.Name))
                .SelectMany(choice => choice.Options.Where(option => genre.Choices[choice.Name].TryGetValue(option.Name, out var amount) && amount == option.Own).Select(option => $"{choice.Name}.{option.Name}"))
                .ToArray();
            if (copies.Length > 0)
                Console.WriteLine($"{preset.Id}: {string.Join(", ", copies)}");
        }

        await Task.CompletedTask;
    }
}
