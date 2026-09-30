using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>A song's swing: how late the second of every pair of notes plays, and how long a pair is, both in beats.</summary>
public readonly record struct Swing(double Delay, double Period)
{
    public static Swing None { get; } = new(0, 1);

    /// <summary>
    ///     Where a position plays, swung: every pair's first half stretched and its second squeezed, so that its middle
    ///     plays <see cref="Delay" /> late, and everything in it, whatever the grid, moves in proportion. It keeps the order
    ///     of any two positions and leaves the pairs' ends where they are, so that no note crosses another and a bar
    ///     starts on time.
    /// </summary>
    public double Apply(double position)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (Delay == 0)
            return position;

        var start = Math.Floor(position / Period) * Period;
        var within = position - start;
        var half = Period / 2;
        return start + (within < half
            ? within * (half + Delay) / half
            : half + Delay + (within - half) * (half - Delay) / half);
    }
}

/// <summary>
///     How a song's notes sit off the grid: its swing, a song's own, which leans to swinging the less conventional the
///     song's rhythm, as straight time is the convention of most of what it plays. It swings the finest notes long
///     enough to be heard swung: 16ths at slow tempos, 8ths otherwise, and none where the meter's groups do not pair
///     them. Generation decides it, <c>Render</c> plays it,
///     every track alike.
/// </summary>
internal static class Groove
{
    /// <summary>The chance a song swings, at a rhythm of middling conventionality.</summary>
    public const double SwingChance = 0.2;

    /// <summary>The lightest swing, as a part of the triplet's; the heaviest is the triplet's, the second note of a pair a third late.</summary>
    public const double LightestSwing = 0.3;

    /// <summary>The shortest note swung, in seconds: 16ths are swung where they are at least this long, 8ths otherwise.</summary>
    public const double ShortestSwungNote = 0.14;

    /// <param name="meter">The meter the song's bars are in, whose groups a pair must divide to swing, as 6/8's 8ths do not.</param>
    public static Swing Generate(IGenerationContext context, double tempo, Tilt rhythm, Meter meter)
    {
        var swings = context.TestProbability(rhythm.Chance(SwingChance, 1));
        var amount = LightestSwing + (1 - LightestSwing) * context.GenerateDouble();

        var sixteenth = 60 / (Meter.BaseTempo * tempo) / 4;
        var period = sixteenth >= ShortestSwungNote ? 0.5 : 1;
        // a triplet's swing plays the pair's second note a third of the pair late of its middle, at two thirds
        return swings && meter.Pairs(period) ? new Swing(amount * period / 6, period) : Swing.None;
    }

    /// <summary>The chance a section's drums play in half time, at a section of middling energy.</summary>
    public const double HalfTimeChance = 0.08;

    /// <summary>The chance a section's drums play in double time, at a section of middling energy, rarer, as it is a busy effect.</summary>
    public const double DoubleTimeChance = 0.04;

    /// <summary>
    ///     How a section's drums keep time: as a step of every drum's period, 1 for half time, the backbeat on the bar's
    ///     third beat and every cycle twice as long, -1 for double time, the backbeat on every beat, and 0 for the song's
    ///     time; half time the likelier the less energy the section has, double time the more.
    /// </summary>
    public static int DrawTimeFeel(IGenerationContext context, Tilt energy)
    {
        var half = energy.Chance(HalfTimeChance, -1);
        var twice = energy.Chance(DoubleTimeChance, 1);
        var draw = context.GenerateDouble();
        return draw < half ? 1 : draw < half + twice ? -1 : 0;
    }

    public static StateMap ToStateMap(Swing swing, IGenerationContext context)
    {
        return new StateMapBuilder("Song")
            .Add(StateKinds.SwingDelay, swing.Delay)
            .Add(StateKinds.SwingPeriod, swing.Period)
            .ToStateMap(context);
    }
}
