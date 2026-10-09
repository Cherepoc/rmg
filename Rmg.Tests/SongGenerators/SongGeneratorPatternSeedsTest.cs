using Rmg.Core.Composition;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     A pattern draws several random sequences: its state, its rhythm, how its offsets change and its note values.
///     Sequences started from the same seed repeat the same numbers, which ties together what should be independent,
///     such as whether a note plays and how loud it is.
/// </summary>
public sealed class SongGeneratorPatternSeedsTest
{
    [Test]
    [Arguments(0UL)]
    [Arguments(1UL)]
    [Arguments(ulong.MaxValue)]
    [Arguments(1UL << 63)]
    public async Task CreatePatternSeeds_GivesEachSequenceItsOwnSeed(ulong seed)
    {
        var result = PatternGenerator.CreatePatternSeeds(seed);

        ulong[] seeds = [result.TrackState, result.Rhythm, result.StateChanges, result.NoteValues];
        await Assert.That(seeds.Distinct().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task CreatePatternSeeds_SameSeed_ResultsInSameSeeds()
    {
        await Assert.That(PatternGenerator.CreatePatternSeeds(42)).IsEqualTo(PatternGenerator.CreatePatternSeeds(42));
    }

    [Test]
    public async Task CreatePatternSeeds_DifferentSeeds_ResultInDifferentSeeds()
    {
        var result1 = PatternGenerator.CreatePatternSeeds(1);
        var result2 = PatternGenerator.CreatePatternSeeds(2);

        await Assert.That(result1.Rhythm).IsNotEqualTo(result2.Rhythm);
        await Assert.That(result1.NoteValues).IsNotEqualTo(result2.NoteValues);
    }
}
