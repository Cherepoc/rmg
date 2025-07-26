namespace Rmg.Core.Songs;

public readonly struct TimeSignature
{
    public int Numerator { get; }

    public int Denominator { get; }

    public TimeSignature(int numerator, int denominator)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numerator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(denominator);

        Numerator = numerator;
        Denominator = denominator;
    }
}
