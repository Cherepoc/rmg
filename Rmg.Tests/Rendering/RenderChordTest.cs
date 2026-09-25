using System.Collections.Immutable;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Rendering;

public sealed class RenderChordTest
{
    private static readonly ImmutableArray<int> NaturalMinor = [0, 2, 3, 5, 7, 8, 10];
    private static readonly ImmutableArray<int> MajorPentatonic = [0, 2, 4, 7, 9];
    private static readonly ImmutableArray<int> WholeHalfOctatonic = [0, 2, 3, 5, 6, 8, 9, 11];
    private static readonly ImmutableArray<int> Chromatic = [..Enumerable.Range(0, 12)];

    // a triad whose third lies between a minor and a major one, for the scale to decide
    private static readonly double[] Triad = [0, 12.0 * 2 / 7, 7];

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    public async Task Triad_InNaturalMinor_IsEveryOtherScaleStep_OnEveryDegree(int rootIndex)
    {
        // Cm, D°, Eb, Fm, Gm, Ab, Bb: the scale's own triads
        var result = Render.SnapChordToScale(NaturalMinor, rootIndex, Triad);

        await Assert.That(result).IsEquivalentTo([0, 2, 4]);
    }

    [Test]
    public async Task Triad_InMajorPentatonic_IsAMajorTriad()
    {
        // C, E, G, where rounding the scale's note count would give C, D, G
        var result = Render.SnapChordToScale(MajorPentatonic, 0, Triad);

        await Assert.That(result).IsEquivalentTo([0, 2, 3]);
    }

    [Test]
    public async Task Triad_InOctatonic_IsDiminished()
    {
        // C, Eb, Gb: a fifth is as far from Gb as from Ab, and the lower note wins a tie
        var result = Render.SnapChordToScale(WholeHalfOctatonic, 0, Triad);

        await Assert.That(result).IsEquivalentTo([0, 2, 4]);
    }

    [Test]
    public async Task Triad_InChromaticScale_IsMinor()
    {
        var result = Render.SnapChordToScale(Chromatic, 0, Triad);

        await Assert.That(result).IsEquivalentTo([0, 3, 7]);
    }

    [Test]
    public async Task NotesAboveAnOctave_LandInTheOctavesAbove()
    {
        // a ninth and a note below the root, as voicings give
        var result = Render.SnapChordToScale(NaturalMinor, 0, [-5, 0, 14]);

        await Assert.That(result).IsEquivalentTo([-3, 0, 8]);
    }

    [Test]
    public async Task Cluster_InMajorPentatonic_TakesFreeNeighbours_AndDropsTheRest()
    {
        // C, Db, D, Eb in a scale of C, D, E, G, A: C, then D and E for the two next, and nothing left for the last
        var result = Render.SnapChordToScale(MajorPentatonic, 0, [0, 1, 2, 3]);

        await Assert.That(result).IsEquivalentTo([0, 1, 2]);
    }

    [Test]
    public async Task ChordFittingTheRange_KeepsItsVoicing()
    {
        // octaves 5 and 6; the chord comes up an octave, as its lowest note would
        var result = Render.FitChordIntoRange(5, 2, [50, 55, 62]);

        await Assert.That(result).IsEquivalentTo([62, 67, 74]);
    }

    [Test]
    public async Task ChordWiderThanTheRange_FoldsItsTopNotesDown()
    {
        // A, C, E in a single octave from C: C and E come down under A
        var result = Render.FitChordIntoRange(5, 1, [69, 72, 76]);

        await Assert.That(result).IsEquivalentTo([60, 64, 69]);
    }

    [Test]
    public async Task NoteFoldingOntoAnother_IsDropped()
    {
        var result = Render.FitChordIntoRange(5, 1, [60, 72]);

        await Assert.That(result).IsEquivalentTo([60]);
    }
}
