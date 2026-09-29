using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     What sets one line apart from another, placed by the same rules (<see cref="Line" />): how wide a range it keeps
///     to, how it moves, and what its strong beats take; the melody's is <see cref="MelodyLayers.Line" />.
/// </summary>
/// <param name="RangeWidth">
///     How wide the line's range is, in semitones, in the middle of the track's: for a melody an octave and a fourth, a
///     little more than a voice sings comfortably, so that a line that runs on turns at its phrase's aim more often than at
///     the range's edge.
/// </param>
/// <param name="RegisterPull">How far from where its phrase aims the line goes before it turns back towards it.</param>
/// <param name="LeapSize">A move this big or bigger is a leap, which the next note fills in by stepping back.</param>
/// <param name="StrongestWeakRank">The weakest beat, by its rank in the rhythm, that still takes a note of the chord.</param>
/// <param name="RepeatChance">The chance of a note meaning to stay on the note before.</param>
/// <param name="MaxLeapChance">The chance of a note meaning to leap, at no stepwiseness; less the more stepwise the line is.</param>
/// <param name="ContinueChance">The chance of a moving note going on the way the line goes, rather than turning back.</param>
/// <param name="RegisterFreedom">
///     The chance, before a song and a section spread it, that a phrase of the line starts afresh at where it aims, as a
///     new phrase that changes register, rather than going on from the note before.
/// </param>
/// <param name="AimOdds">
///     How much likelier a note is to go on towards where its phrase aims, and to turn back rather than go on away from
///     it, as the odds at <see cref="RegisterPull" /> semitones from it, less the nearer it is.
/// </param>
internal sealed record LineProfile(
    int RangeWidth,
    int RegisterPull,
    int LeapSize,
    int StrongestWeakRank,
    double RepeatChance,
    double MaxLeapChance,
    double ContinueChance,
    double AimOdds,
    double RegisterFreedom
)
{
    /// <summary>
    ///     Where a note means to go: staying (0), a step (1), or a leap (2), the less likely the more stepwise the line
    ///     is; and its draw of whether it goes on the way the line goes or turns back, from 0 to 1, which goes on below
    ///     <see cref="ContinueChance" /> as the aim leans it (<see cref="GetContinueChance" />), so that it runs up or
    ///     down a while before it turns.
    /// </summary>
    public (int Step, double Turn) GenerateStep(IGenerationContext context, double stepwiseness)
    {
        if (context.TestProbability(RepeatChance))
            return (0, 0);

        var turn = context.GenerateDouble();
        var leapChance = (1 - Math.Clamp(stepwiseness, 0, 1)) * MaxLeapChance;
        return (context.TestProbability(leapChance) ? 2 : 1, turn);
    }

    /// <summary>
    ///     The chance a note goes on the way the line goes, leaning to go on towards where its phrase aims and to turn
    ///     back from going on away from it, the more the further it is, up to <see cref="RegisterPull" />.
    /// </summary>
    /// <param name="towardsAim">How far going on moves towards the aim, in semitones; negative away from it.</param>
    public double GetContinueChance(double towardsAim)
    {
        return Tilt.Of(AimOdds, 1).Chance(ContinueChance, Math.Clamp(towardsAim / RegisterPull, -1, 1));
    }
}
