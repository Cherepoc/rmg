using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Chords;

/// <summary>A chord's own mode (<see cref="ChordScales.OwnModes" />), as a planed chord draws it: by the progression facet, and a genre's pool.</summary>
public sealed class OwnModesTest
{
    /// <summary>How often each mode is drawn for a planed chord over C major's home, by its name, the section's scale named so.</summary>
    private static Dictionary<string, double> Shares(double unconventionality, Genre genre)
    {
        var scale = Rmg.Core.Composition.Scales.Major;
        var named = ChordScales.OwnModes.ToDictionary(x => ChordScales.Toward(scale.Offsets, 0, x.Mode.Offsets, ChordScaleKind.Own) ?? ChordScale.Section, x => x.Mode.Name);
        var context = new GenerationContext(0);
        var drawn = Enumerable.Range(0, 1000).SelectMany(_ => ChordScales.Planed(context, scale, 0, 4, false, unconventionality, genre).Skip(1)).ToArray();
        return drawn.GroupBy(x => named.GetValueOrDefault(x, "Major")).ToDictionary(x => x.Key, x => x.Count() / (double)drawn.Length);
    }

    [Test]
    public async Task AtThePlainEnd_APlanedChordTakesLydianMixolydianOrDorian_AndAtTheWildAnyAlike()
    {
        var plain = Shares(0, Genre.None);
        var wild = Shares(1, Genre.None);
        Console.WriteLine($"plain: {string.Join(", ", plain.Select(x => $"{x.Key} {x.Value:P0}"))}; wild: {string.Join(", ", wild.Select(x => $"{x.Key} {x.Value:P0}"))}");
        await Assert.That(plain.Keys.Order()).IsEquivalentTo(new[] { "Dorian", "Lydian", "Mixolydian" }, CollectionOrdering.Matching);
        await Assert.That(wild.Count).IsEqualTo(5);
        await Assert.That(wild.Values.All(x => x is > 0.15 and < 0.25)).IsTrue();
    }

    [Test]
    public async Task Fusions_PlanedChords_LeanToLydianDorianAndMelodicMinor()
    {
        var shares = Shares(0.5, GenrePresets.Genre("fusion"));
        Console.WriteLine(string.Join(", ", shares.Select(x => $"{x.Key} {x.Value:P0}")));
        await Assert.That(shares["Lydian"] + shares["Dorian"] + shares["Melodic minor"]).IsGreaterThan(0.75);
        await Assert.That(shares.GetValueOrDefault("Phrygian")).IsLessThan(0.1);
    }
}
