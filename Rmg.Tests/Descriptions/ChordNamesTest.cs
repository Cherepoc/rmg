using Rmg.Core.Songs;

namespace Rmg.Tests.Descriptions;

public sealed class ChordNamesTest
{
    private static readonly int[] Major = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] Minor = [0, 2, 3, 5, 7, 8, 10];
    private static readonly int[] Mixolydian = [0, 2, 4, 5, 7, 9, 10];

    [Test]
    [Arguments(new[] { 0, 4, 7 }, "")]
    [Arguments(new[] { 0, 3, 7 }, "m")]
    [Arguments(new[] { 0, 3, 6 }, "dim")]
    [Arguments(new[] { 0, 4, 8 }, "aug")]
    [Arguments(new[] { 0, 7 }, "5")]
    [Arguments(new[] { 0, 2, 7 }, "sus2")]
    [Arguments(new[] { 0, 5, 7 }, "sus4")]
    [Arguments(new[] { 0, 5, 7, 10 }, "7sus4")]
    [Arguments(new[] { 0, 4, 7, 10 }, "7")]
    [Arguments(new[] { 0, 4, 7, 11 }, "maj7")]
    [Arguments(new[] { 0, 3, 7, 10 }, "m7")]
    [Arguments(new[] { 0, 3, 7, 11 }, "m(maj7)")]
    [Arguments(new[] { 0, 3, 6, 10 }, "m7♭5")]
    [Arguments(new[] { 0, 3, 6, 9 }, "dim7")]
    [Arguments(new[] { 0, 4, 7, 9 }, "6")]
    [Arguments(new[] { 0, 3, 7, 9 }, "m6")]
    [Arguments(new[] { 0, 4, 7, 9, 2 }, "6/9")]
    [Arguments(new[] { 0, 4, 7, 10, 2 }, "9")]
    [Arguments(new[] { 0, 3, 7, 10, 2 }, "m9")]
    [Arguments(new[] { 0, 4, 7, 11, 2 }, "maj9")]
    [Arguments(new[] { 0, 4, 7, 2 }, "add9")]
    [Arguments(new[] { 4, 7, 10 }, "7")]
    public async Task AChord_IsNamedByTheNotesItPlaysAboveItsRoot(int[] intervals, string suffix)
    {
        await Assert.That(ChordNames.Of(intervals.ToHashSet()).Suffix).IsEqualTo(suffix);
    }

    [Test]
    public async Task ANumeral_IsTheDegrees_InTheCaseOfItsThird_FlatOrSharpOffTheMajorScale()
    {
        string Numeral(int degree, int[] scale, params int[] intervals) => ChordNames.Numeral(degree, scale, ChordNames.Of(intervals.ToHashSet()));

        await Assert.That(Numeral(4, Major, 0, 4, 7, 10)).IsEqualTo("V7");
        await Assert.That(Numeral(1, Major, 0, 3, 7, 10)).IsEqualTo("ii7");
        await Assert.That(Numeral(6, Major, 0, 3, 6)).IsEqualTo("vii°");
        await Assert.That(Numeral(6, Major, 0, 3, 6, 10)).IsEqualTo("viiø7");
        await Assert.That(Numeral(0, Major, 0, 4, 7, 11)).IsEqualTo("Imaj7");
        // the flats of minor and Mixolydian, and a cadence's raised fifth chord in minor
        await Assert.That(Numeral(5, Minor, 0, 4, 7)).IsEqualTo("♭VI");
        await Assert.That(Numeral(6, Mixolydian, 0, 4, 7)).IsEqualTo("♭VII");
        await Assert.That(Numeral(4, Minor, 0, 4, 7)).IsEqualTo("V");
        // a chord of no third in its scale's case
        await Assert.That(Numeral(0, Minor, 0, 7)).IsEqualTo("i5");
        await Assert.That(Numeral(4, Major, 0, 5, 7, 10)).IsEqualTo("V7sus4");
    }
}
