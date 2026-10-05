namespace Rmg.Core;

/// <summary>
///     Numbers written in the 62 letters and digits, which a link carries as they are and a chat's markdown never takes
///     for its own, as it takes "_" for italics: the digits, then the capitals, then the small letters; a song's seed, its
///     settings and a genre's key.
/// </summary>
public static class Base62
{
    public const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>The most digits a seed takes, all 64 of its bits.</summary>
    public const int SeedLength = 11;

    /// <summary>A seed as its digits, with no zeros in front.</summary>
    public static string FromSeed(ulong seed)
    {
        var text = Write(seed, SeedLength).TrimStart('0');
        return text.Length > 0 ? text : "0";
    }

    /// <summary>A number as exactly so many digits, zeros in front.</summary>
    public static string Write(ulong number, int length)
    {
        var digits = new char[length];
        for (var i = length - 1; i >= 0; i--, number /= 62)
            digits[i] = Digits[(int)(number % 62)];
        return new string(digits);
    }

    /// <summary>
    ///     The seed the digits write, or none for anything that is not one: no digits, a character not a digit, or more
    ///     than 64 bits.
    /// </summary>
    public static ulong? ToSeed(string text)
    {
        if (text.Length is 0 or > SeedLength)
            return null;

        var seed = 0UL;
        foreach (var digit in text)
        {
            var value = Digits.IndexOf(digit);
            // a digit more would push the seed past its 64 bits
            if (value < 0 || seed > (ulong.MaxValue - (ulong)value) / 62)
                return null;
            seed = seed * 62 + (ulong)value;
        }

        return seed;
    }
}
