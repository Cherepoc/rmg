using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Progressions;

public sealed class ProgressionsTest
{
    private const int PhraseCount = 20_000;

    private static ImmutableArray<int>[] GeneratePhrases(Scale scale, int home, double strictness) => GeneratePhrases(scale, home, strictness, _ => 0);

    private static ImmutableArray<int>[] GeneratePhrases(Scale scale, int home, double strictness, Func<int, double> motionLean)
    {
        var context = new GenerationContext(1);
        return [..Enumerable.Range(0, PhraseCount).Select(_ => Rmg.Core.Composition.Progressions.Generate(context, scale, home, strictness, 4, 1, motionLean, _ => 0))];
    }

    // how often the roots move by a motion, by its place in the motions, of every move between chords
    private static double ShareOf(ImmutableArray<int>[] phrases, int motion) =>
        (double)phrases.Sum(x => x.Zip(x.Skip(1)).Count(m => Mod7(m.Second - m.First) == motion)) / phrases.Sum(x => x.Length - 1);

    [Test]
    public async Task ADegreeLeanedTo_IsLikelierAmongTheChordsDrawn_AndOneLeanedFrom_Rarer()
    {
        var minor = Rmg.Core.Composition.Scales.All.Single(x => x.Name == "Natural minor");
        double Share(Func<int, double> degreeLean, int degree)
        {
            var context = new GenerationContext(1);
            var phrases = Enumerable.Range(0, 4000).Select(_ => Rmg.Core.Composition.Progressions.Generate(context, minor, 0, 0.85, 4, 1, _ => 0, degreeLean)).ToArray();
            // the chords drawn, the first, home, not among them
            return (double)phrases.Sum(x => x.Skip(1).Count(r => Mod7(r) == degree)) / phrases.Sum(x => x.Length - 1);
        }

        var seventh = Share(_ => 0, 6);
        Console.WriteLine($"the seventh {seventh:P0}, leaned to {Share(x => x == 6 ? 1 : 0, 6):P0}, from {Share(x => x == 6 ? -1 : 0, 6):P0}");
        await Assert.That(Share(x => x == 6 ? 1 : 0, 6)).IsGreaterThan(seventh * 1.3);
        await Assert.That(Share(x => x == 6 ? -1 : 0, 6)).IsLessThan(seventh / 1.3);
    }

    [Test]
    public async Task ThreePhrases_StartFromHomeThenIVThenV_TheFirstTwoEndingHome_TheLastTurningAround_AsTheBluesDoes()
    {
        // twelve chords, a chord a bar, in three phrases of four, the blues' root motions leaned to (down a fourth, up a step)
        var major = Rmg.Core.Composition.Scales.All.Single(x => x.Name == "Major");
        var motions = Rmg.Core.Composition.Progressions.Motions.Select(x => x.Name).ToList();
        var context = new GenerationContext(1);
        var phrases = Enumerable.Range(0, 2000)
            .Select(_ => Rmg.Core.Composition.Progressions.Generate(context, major, 0, 1, 12, 3, x => x == motions.IndexOf("fourthDown") ? 0.8 : x == motions.IndexOf("stepUp") ? 0.7 : 0, _ => 0))
            .ToArray();
        double Share(int chord, int root) => (double)phrases.Count(x => Mod7(x[chord]) == root) / phrases.Length;

        Console.WriteLine($"bar 5 IV {Share(4, 3):P0}, bar 9 V {Share(8, 4):P0}, bar 4 I {Share(3, 0):P0}, bar 8 I {Share(7, 0):P0}, bar 10 IV {Share(9, 3):P0}, bar 11 I {Share(10, 0):P0}, bar 12 V {Share(11, 4):P0}");
        foreach (var strictness in new[] { 0.95, 0.9, 0.85 })
        {
            var some = Enumerable.Range(0, 2000).Select(_ => Rmg.Core.Composition.Progressions.Generate(context, major, 0, strictness, 12, 3,
                x => x == motions.IndexOf("fourthDown") ? 0.6 : x == motions.IndexOf("stepUp") ? 0.4 : 0, _ => 0)).ToArray();
            double At(int chord, int root) => (double)some.Count(x => Mod7(x[chord]) == root) / some.Length;
            Console.WriteLine($"at {strictness}: bar 5 IV {At(4, 3):P0}, bar 9 V {At(8, 4):P0}, bar 4 I {At(3, 0):P0}, bar 8 I {At(7, 0):P0}, bar 10 IV {At(9, 3):P0}, bar 11 I {At(10, 0):P0}, bar 12 V {At(11, 4):P0}");
        }
        // the second phrase from IV, the last from V and turning around through IV and I to the cadence, V, most of the
        // time; the first two ending home more often than not, the chord before an ending free
        await Assert.That(Share(4, 3)).IsGreaterThan(0.6);
        await Assert.That(Share(8, 4)).IsGreaterThan(0.6);
        await Assert.That(Share(3, 0)).IsGreaterThan(0.55);
        await Assert.That(Share(7, 0)).IsGreaterThan(0.55);
        await Assert.That(Share(9, 3)).IsGreaterThan(0.5);
        await Assert.That(Share(10, 0)).IsGreaterThan(0.5);
        await Assert.That(Share(11, 4)).IsGreaterThan(0.5);
    }

    [Test]
    public async Task AMotionLeanedTo_IsHeardMore_AtEveryStrictness_AndLeanedFrom_Less()
    {
        var major = Rmg.Core.Composition.Scales.All.Single(x => x.Name == "Major");
        var fourthDown = Rmg.Core.Composition.Progressions.Motions.Select(x => x.Name).ToList().IndexOf("fourthDown");
        foreach (var strictness in new[] { 1.0, 0.85, 0 })
        {
            var asIs = ShareOf(GeneratePhrases(major, 0, strictness), fourthDown);
            await Assert.That(ShareOf(GeneratePhrases(major, 0, strictness, x => x == fourthDown ? 1 : 0), fourthDown)).IsGreaterThan(asIs * 1.5);
            await Assert.That(ShareOf(GeneratePhrases(major, 0, strictness, x => x == fourthDown ? -1 : 0), fourthDown)).IsLessThan(asIs / 1.5);
        }
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

        await Assert.That(strong).IsEquivalentTo(expected.Cadences, CollectionOrdering.Matching);
        await Assert.That(weights[0]).IsEqualTo(0);
    }

    [Test]
    public async Task Cadences_OfARelativeHome_AreThoseOfItsMode()
    {
        // natural minor seen from its third step is major, so a section at home there cadences like major
        var fromRelativeMajor = Rmg.Core.Composition.Progressions.GetCadenceWeights(Rmg.Core.Composition.Scales.NaturalMinor.Offsets, 2);
        var major = Rmg.Core.Composition.Progressions.GetCadenceWeights(Rmg.Core.Composition.Scales.Major.Offsets, 0);

        await Assert.That(fromRelativeMajor).IsEquivalentTo(major, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(0.5)]
    [Arguments(1.0)]
    public async Task Phrases_StartAtHome_AndStayWithinThreeSteps(double strictness)
    {
        var phrases = GeneratePhrases(Rmg.Core.Composition.Scales.Major, 0, strictness);

        await Assert.That(phrases.All(x => x.Length == Meter.PhraseBarCount)).IsTrue();
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
            .Select(_ => Rmg.Core.Composition.Progressions.GenerateHome(context, scale, Rmg.Core.Composition.SectionRole.Free, Rmg.Core.Composition.Genre.None, 0.5))
            .CountBy(x => x)
            .ToDictionary();

        await Assert.That(homes.Keys.Order()).IsEquivalentTo(new[] { relative, 0, 3, -3 }.Order(), CollectionOrdering.Matching);
        await Assert.That(homes[0] / (double)PhraseCount).IsEqualTo(0.6).Within(0.02);
        await Assert.That(homes[relative] / (double)PhraseCount).IsEqualTo(0.2).Within(0.02);
    }

    [Test]
    public async Task Strictness_KeepsToEveryRuleAtThePlainEnd_ToNoneAtTheWild_AndAsTheCorpusDidBetween()
    {
        await Assert.That(Rmg.Core.Composition.Progressions.Strictness.At(0)).IsEqualTo(1);
        await Assert.That(Rmg.Core.Composition.Progressions.Strictness.At(0.5)).IsEqualTo(0.85).Within(1e-9);
        await Assert.That(Rmg.Core.Composition.Progressions.Strictness.At(1)).IsEqualTo(0);
    }

    [Test]
    public async Task SongChords_AreTheSectionsHomeAndTheProgressionsRoot()
    {
        // songs of their own, traced to explain their states, which the corpus's are not
        var chordEntries = TestCorpus.InParallel(Enumerable.Range(0, 4), seed =>
        {
            using var trace = StateTrace.Start();
            SongGenerator.GenerateSong((ulong)seed);
            return trace.Entries.Where(x => x.Point == TracePoints.Chord).ToArray();
        }).SelectMany(x => x).ToArray();

        await Assert.That(chordEntries.Length).IsGreaterThan(0);
        // a root of 0, the tonic, is left out as the default, so a chord may show one layer or none
        var seen = new HashSet<string>();
        foreach (var entry in chordEntries)
        {
            var layers = entry.StateMap.Explain(StateKinds.ChordRoot).Select(x => x.Layer).ToArray();
            await Assert.That(layers.All(x => x is "Progression" or "Section")).IsTrue();
            seen.UnionWith(layers);
        }

        await Assert.That(seen).IsEquivalentTo(new[] { "Progression", "Section" }, CollectionOrdering.Any);
    }
}
