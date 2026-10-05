namespace Rmg.WebApi.Songs;

/// <summary>
///     The lengths a song may be given, in 62 steps from half a minute to fifteen, each step a like ratio from the next,
///     the finer the shorter: about 2:49 at the middle step, near how long a song plays as drawn.
/// </summary>
public static class LengthScale
{
    public const int Steps = StepScale.Steps;

    private const double Shortest = Core.Composition.GenreChoices.ShortestLength;

    private const double Longest = Core.Composition.GenreChoices.LongestLength;

    /// <summary>A step's length, in seconds.</summary>
    public static double Seconds(int step) => Shortest * Math.Pow(Longest / Shortest, step / (double)StepScale.Last);

    /// <summary>The step nearest a length in seconds, by its ratio to it; the ends for one beyond them.</summary>
    public static int StepOf(double seconds) =>
        Enumerable.Range(0, Steps).MinBy(step => Math.Abs(Math.Log(Seconds(step) / seconds)));
}
