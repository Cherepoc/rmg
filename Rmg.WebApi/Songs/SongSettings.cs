using System.Collections.Immutable;
using System.Numerics;

namespace Rmg.WebApi.Songs;

/// <summary>
///     What a song was heard with: its unconventionality, drawn or given, and its mix, as the page had them. Written as
///     a format's character and a number of letters and digits (<see cref="Format" />), short enough for a link, which
///     the page writes and this reads: a link carries it to play the song as it was shared, and a rating carries it to
///     say what was rated.
/// </summary>
/// <param name="Unconventionality">From 0 for the plainest to 127 for the wildest.</param>
/// <param name="IsGiven">Whether it was asked for, which makes it part of what names the song, or drawn by the song.</param>
/// <param name="SongVolume">How loud the whole song was, from 0 to 127.</param>
/// <param name="Channels">Every MIDI channel's, from the first, whether the song plays it or not.</param>
public sealed record SongSettings(int Unconventionality, bool IsGiven, int SongVolume, ImmutableArray<ChannelSettings> Channels)
{
    /// <summary>The format this writes, as its first character; one that is read differently takes the next.</summary>
    public const char FormatOne = '1';

    public const int ChannelCount = 16;

    private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    // every value is 7 bits, as MIDI's are, and every switch 1: the unconventionality and whether it was given, the
    // song's volume, and each channel's instrument, whether it is on, and its volume, with a bit to spare, which is 256
    private const int Bits = 7 + 1 + 7 + ChannelCount * (7 + 1 + 7) + 1;

    /// <summary>How many digits the number takes, as many as 256 bits need in base 62.</summary>
    public const int Length = 43;

    /// <summary>The settings as their format's character and the number, zero-padded to <see cref="Length" />.</summary>
    public string Format()
    {
        if (Channels.Length != ChannelCount)
            throw new InvalidOperationException($"Settings are of {ChannelCount} channels, not {Channels.Length}.");

        var number = BigInteger.Zero;
        void Put(int value, int bits)
        {
            if (value < 0 || value >> bits != 0) throw new InvalidOperationException($"{value} does not fit in {bits} bits.");
            number = (number << bits) | value;
        }

        Put(Unconventionality, 7);
        Put(IsGiven ? 1 : 0, 1);
        Put(SongVolume, 7);
        foreach (var channel in Channels)
        {
            Put(channel.Instrument, 7);
            Put(channel.IsEnabled ? 1 : 0, 1);
            Put(channel.Volume, 7);
        }

        Put(0, 1);

        var digits = new char[Length];
        for (var i = Length - 1; i >= 0; i--)
        {
            digits[i] = Digits[(int)(number % Digits.Length)];
            number /= Digits.Length;
        }

        return FormatOne + new string(digits);
    }

    /// <summary>Reads settings the page wrote, or none for anything that is not settings of a known format.</summary>
    public static SongSettings? Parse(string? text)
    {
        if (text is null || text.Length != 1 + Length || text[0] != FormatOne) return null;

        var number = BigInteger.Zero;
        foreach (var digit in text.AsSpan(1))
        {
            var value = Digits.IndexOf(digit);
            if (value < 0) return null;
            number = number * Digits.Length + value;
        }

        // 43 digits hold a little more than 256 bits, and the spare bit is always 0 in this format
        if (number >> Bits != 0 || !(number & 1).IsZero) return null;

        var position = Bits;
        int Take(int bits)
        {
            position -= bits;
            return (int)((number >> position) & ((1 << bits) - 1));
        }

        var unconventionality = Take(7);
        var isGiven = Take(1) == 1;
        var songVolume = Take(7);
        var channels = Enumerable.Range(0, ChannelCount)
            .Select(_ => new ChannelSettings(Take(7), Take(1) == 1, Take(7)))
            .ToImmutableArray();

        return new SongSettings(unconventionality, isGiven, songVolume, channels);
    }
}

/// <param name="Instrument">Its General MIDI program, 0 to 127.</param>
/// <param name="IsEnabled">Whether it is in the song, rather than switched off.</param>
/// <param name="Volume">How loud it is, from 0 to 127, before the song's volume.</param>
public sealed record ChannelSettings(int Instrument, bool IsEnabled, int Volume);
