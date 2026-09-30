using System.Numerics;

namespace Rmg.Core;

/// <summary>
///     Numbers written in letters and digits alone, 0-9, A-Z and a-z, which a link carries as they are and a double click
///     selects whole: a song's seed, and its settings.
/// </summary>
public static class Base62
{
    private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>The most digits a seed takes, its 32 bits read as unsigned.</summary>
    public const int SeedLength = 6;

    /// <summary>A number as the given count of digits, zero-padded.</summary>
    public static string Encode(BigInteger number, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number.Sign);

        var digits = new char[length];
        for (var i = length - 1; i >= 0; i--)
        {
            digits[i] = Digits[(int)(number % Digits.Length)];
            number /= Digits.Length;
        }

        if (!number.IsZero)
            throw new ArgumentOutOfRangeException(nameof(number), $"The number takes more than {length} digits.");

        return new string(digits);
    }

    /// <summary>The number the digits write, or none for anything that is not digits of base 62.</summary>
    public static BigInteger? Decode(string text)
    {
        if (text.Length == 0)
            return null;

        var number = BigInteger.Zero;
        foreach (var digit in text)
        {
            var value = Digits.IndexOf(digit);
            if (value < 0)
                return null;
            number = number * Digits.Length + value;
        }

        return number;
    }

    /// <summary>A seed as its digits, its 32 bits read as unsigned, with no zeros in front.</summary>
    public static string FromSeed(int seed)
    {
        var text = Encode((uint)seed, SeedLength).TrimStart('0');
        return text.Length > 0 ? text : "0";
    }

    /// <summary>The seed the digits write, or none for anything that is not one.</summary>
    public static int? ToSeed(string text)
    {
        return text.Length <= SeedLength && Decode(text) is { } number && number <= uint.MaxValue ? unchecked((int)(uint)number) : null;
    }
}
