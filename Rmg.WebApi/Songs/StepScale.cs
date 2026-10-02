namespace Rmg.WebApi.Songs;

/// <summary>
///     The 64 steps an amount is given in, a character of the page's settings each, from 0 to 63 with an exact middle at
///     32: 32 steps below it and 31 above, as MIDI's pan has, so that a value can be given at its middle, 50%, a pan in
///     the middle or a tempo of 120.
/// </summary>
public static class StepScale
{
    public const int Steps = 64;

    public const int Last = Steps - 1;

    public const int Middle = 32;

    /// <summary>A step's amount, from 0 at the first through 0.5 at the middle to 1 at the last.</summary>
    public static double ToAmount(int step) =>
        step <= Middle ? step / (2.0 * Middle) : 0.5 + (step - Middle) / (2.0 * (Last - Middle));

    /// <summary>An amount from 0 to 1 as its nearest step.</summary>
    public static int ToStep(double amount) =>
        (int)Math.Round(amount <= 0.5 ? amount * 2 * Middle : Middle + (amount - 0.5) * 2 * (Last - Middle));
}
