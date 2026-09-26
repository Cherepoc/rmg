using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Progressions;

public sealed class ProgressionsTest
{
    private const int PhraseCount = 20_000;

    private static ImmutableArray<int>[] GeneratePhrases(Scale scale, int home, double strictness)
    {
        var context = new GenerationContext(1);
        return [..Enumerable.Range(0, PhraseCount).Select(_ => Rmg.Core.Composition.Progressions.Generate(context, scale, home, strictness))];
    }

    private static int Mod7(int x) => ((x % 7) + 7) % 7;

    public static IEnumerable<Func<(string Scale, int[] Cadences)>> StrongCadences()
    {
        // the roots, in steps above the tonic, that end a phrase at least 0.8 as well as the best
        yield return () => ("Major", [4]);
        yield return () => ("Harmonic minor", [4]);
        yield return () => ("Natural minor", [6]);
        yield return () => ("Dorian", [3, 6]);
        yield return () => ("Mixolydian", [6]);
        yield return () => ("Phrygian", [1]);
        yield return () => ("Lydian", [4]);
    }

    [Test]
    [MethodDataSource(nameof(StrongCadences))]
    public async Task Cadences_SuitTheMode((string Scale, int[] Cadences) expected)
    {
        var scale = Rmg.Core.Composition.Scales.All.Single(x => x.Name == expected.Scale);

        var weights = Rmg.Core.Composition.Progressions.GetCadenceWeights(scale.Offsets, 0);
        var strong = Enumerable.Range(0, 7).Where(x => weights[x] >= 0.8).ToArray();

        await Assert.That(strong).IsEquivalentTo(expected.Cadences);
        await Assert.That(weights[0]).IsEqualTo(0);
    }

    [Test]
    public async Task Cadences_OfARelativeHome_AreThoseOfItsMode()
    {
        // natural minor seen from its third step is major, so a section at home there cadences like major
        var fromRelativeMajor = Rmg.Core.Composition.Progressions.GetCadenceWeights(Rmg.Core.Composition.Scales.NaturalMinor.Offsets, 2);
        var major = Rmg.Core.Composition.Progressions.GetCadenceWeights(Rmg.Core.Composition.Scales.Major.Offsets, 0);

        await Assert.That(fromRelativeMajor).IsEquivalentTo(major);
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(0.5)]
    [Arguments(1.0)]
    public async Task Phrases_StartAtHome_AndStayWithinThreeSteps(double strictness)
    {
        var phrases = GeneratePhrases(Rmg.Core.Composition.Scales.Major, 0, strictness);

        await Assert.That(phrases.All(x => x.Length == Rmg.Core.Composition.Progressions.BarCount)).IsTrue();
        await Assert.That(phrases.All(x => x[0] == 0)).IsTrue();
        await Assert.That(phrases.All(x => x.All(root => root is >= -3 and <= 3))).IsTrue();
    }

    [Test]
    public async Task StrictPhrases_PrepareInBarThree_AndCadenceInBarFour()
    {
        var scale = Rmg.Core.Composition.Scales.Major;
        var cadences = Rmg.Core.Composition.Progressions.GetCadenceWeights(scale.Offsets, 0);
        var phrases = GeneratePhrases(scale, 0, 1);

        var preparing = phrases.Count(x => Mod7(x[2]) is 1 or 3) / (double)PhraseCount;
        var cadencing = phrases.Count(x => cadences[Mod7(x[3])] > 0) / (double)PhraseCount;

        await Assert.That(preparing).IsGreaterThan(0.75);
        await Assert.That(cadencing).IsGreaterThan(0.85);
    }

    [Test]
    public async Task StrictPhrases_FallAFifthMostOften_AndRarelyRepeatARoot()
    {
        var phrases = GeneratePhrases(Rmg.Core.Composition.Scales.NaturalMinor, 0, 1);

        var moves = phrases
            .SelectMany(x => x.Zip(x.Skip(1)))
            .Select(x => Mod7(x.Second - x.First))
            .CountBy(x => x)
            .ToDictionary();
        var total = moves.Values.Sum();

        // three steps up is a fifth down
        await Assert.That(moves.MaxBy(x => x.Value).Key).IsEqualTo(3);
        await Assert.That(moves.GetValueOrDefault(0) / (double)total).IsLessThan(0.1);
    }

    [Test]
    public async Task LooserPhrases_KeepLessToTheRules()
    {
        var scale = Rmg.Core.Composition.Scales.Major;
        var cadences = Rmg.Core.Composition.Progressions.GetCadenceWeights(scale.Offsets, 0);
        double CadenceShare(double strictness) =>
            GeneratePhrases(scale, 0, strictness).Count(x => cadences[Mod7(x[3])] > 0) / (double)PhraseCount;

        var strict = CadenceShare(1);
        var loose = CadenceShare(0);

        await Assert.That(loose).IsLessThan(strict - 0.2);
        // with no rules at all, bar 4 lands on each of the 7 steps about as often
        var cadenceStepCount = cadences.Count(x => x > 0);
        await Assert.That(loose).IsEqualTo(cadenceStepCount / 7.0).Within(0.02);
    }

    [Test]
    [Arguments("Natural minor", 2)]
    [Arguments("Dorian", 2)]
    [Arguments("Major", -2)]
    [Arguments("Mixolydian", -2)]
    public async Task Homes_AreTheTonicMostly_ThenTheRelativeKey(string scaleName, int relative)
    {
        var scale = Rmg.Core.Composition.Scales.All.Single(x => x.Name == scaleName);
        var context = new GenerationContext(1);
        var homes = Enumerable.Range(0, PhraseCount)
            .Select(_ => Rmg.Core.Composition.Progressions.GenerateHome(context, scale))
            .CountBy(x => x)
            .ToDictionary();

        await Assert.That(homes.Keys.Order()).IsEquivalentTo(new[] { relative, 0, 3, -3 }.Order());
        await Assert.That(homes[0] / (double)PhraseCount).IsEqualTo(0.6).Within(0.02);
        await Assert.That(homes[relative] / (double)PhraseCount).IsEqualTo(0.2).Within(0.02);
    }

    [Test]
    [Arguments(0.0, 1.0)]
    [Arguments(2.5, 0.5)]
    [Arguments(5.0, 0.0)]
    [Arguments(6.0, 0.0)]
    public async Task Strictness_FallsWithTheAnchor(double anchor, double expected)
    {
        var unconventionality = new HarmonicUnconventionality(anchor, 1, 1, 1);

        await Assert.That(unconventionality.ProgressionStrictness).IsEqualTo(expected).Within(1e-9);
    }

    [Test]
    public async Task SongChords_AreTheSectionsHomeAndTheProgressionsRoot()
    {
        using var trace = StateTrace.Start();
        SongGenerator.GenerateSong(1);

        var chordEntries = trace.Entries.Where(x => x.Point == "Chord").ToArray();

        await Assert.That(chordEntries.Length).IsGreaterThan(0);
        foreach (var entry in chordEntries)
        {
            var layers = entry.StateMap.Explain(StateKinds.ChordRootNoteOffset).Select(x => x.Layer).Order().ToArray();
            await Assert.That(layers).IsEquivalentTo(new[] { "Progression", "Section" });
        }
    }
}
