namespace Rmg.Core;

/// <summary>
///     Numbers written in 64 characters that a link carries as they are: the digits, the letters, and "-" and "_", in that
///     order, which extends base 62's, so that a small number reads as it did; a song's seed and its settings. The order
///     is not RFC 4648's base64url, whose "A" is 0, since these are numbers rather than bytes.
/// </summary>
public static class Base64
{
    public const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

    /// <summary>The most digits a seed takes, all 64 of its bits.</summary>
    public const int SeedLength = 11;

    /// <summary>A seed as its digits, six bits each, with no zeros in front.</summary>
    public static string FromSeed(ulong seed)
    {
        var digits = new char[SeedLength];
        for (var i = SeedLength - 1; i >= 0; i--, seed >>= 6)
            digits[i] = Digits[(int)(seed & 63)];

        var text = new string(digits).TrimStart('0');
        return text.Length > 0 ? text : "0";
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
            // a digit more would push bits past the 64th
            if (value < 0 || seed > ulong.MaxValue >> 6)
                return null;
            seed = seed << 6 | (uint)value;
        }

        return seed;
    }
}
