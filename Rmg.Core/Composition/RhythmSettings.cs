namespace Rmg.Core.Composition;

/// <summary>Where the rhythm settings that are not steps start, before the layers move them.</summary>
internal static class RhythmSettings
{
    /// <summary>The fullness a song starts from: every rank from the rank offset halves a position's chance.</summary>
    public const double Fullness = 0.5;

    /// <summary>The least fullness, at which a pattern keeps little more than its offset rank.</summary>
    public const double MinFullness = 0.05;

    /// <summary>The variation a song starts from: half the cycles drawn afresh, half repeating the cycle before.</summary>
    public const double Variation = 0.5;
}
