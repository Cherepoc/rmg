using System.Collections.Immutable;

namespace Rmg.Core.Composition;

/// <summary>
///     The meter a song is in: a bar as its groups of 16ths, from which its hierarchy follows, the bar splitting into
///     its groups and every group into its beats and on down to the 16ths; 4/4 as two halves of two beats. A section is
///     made of 4-bar patterns in every meter, one bar for every chord of a progression of a chord a bar.
/// </summary>
/// <param name="Groups">The bar's groups, each in 16ths, in order.</param>
public sealed record Meter(ImmutableArray<int> Groups)
{
    /// <summary>Four beats, as two halves of two.</summary>
    public static Meter FourFour { get; } = new([8, 8]);

    /// <summary>How many bars a section's pattern has.</summary>
    public const int PatternBarCount = Progressions.BarCount;

    /// <summary>The tempo, in beats a minute, that a song's tempo state is a multiple of; a beat is a quarter note.</summary>
    public const double BaseTempo = 120;

    /// <summary>
    ///     The bar a rhythm's period and phase are counted in, in beats: 4/4's, whatever the song's meter, so that a
    ///     setting means a length, which the song's meter plays at the level of its own nearest it.
    /// </summary>
    public const double ReferenceBar = 4;

    /// <summary>How many 16ths a bar has.</summary>
    public int Sixteenths => Groups.Sum();

    /// <summary>How long a bar is, in beats.</summary>
    public double BarDuration => Sixteenths / 4.0;

    /// <summary>How long a section's pattern is, in beats.</summary>
    public double PatternDuration => PatternBarCount * BarDuration;

    /// <summary>
    ///     The time signature a score writes the bar in: in quarters where every group is whole beats, as 4/4 and 3/4, in
    ///     8ths where every group is whole 8ths, as 6/8 and 7/8, and in 16ths otherwise, as 15/16 of a group of three.
    /// </summary>
    public (int Numerator, int Denominator) TimeSignature =>
        Groups.All(x => x % 4 == 0) ? (Sixteenths / 4, 4) : Groups.All(x => x % 2 == 0) ? (Sixteenths / 2, 8) : (Sixteenths, 16);

    public bool Equals(Meter? other) => other is not null && Groups.SequenceEqual(other.Groups);

    public override int GetHashCode() => Groups.Aggregate(17, (hash, group) => hash * 31 + group);

    public override string ToString() => $"{Sixteenths}/16 ({string.Join("+", Groups)})";
}
