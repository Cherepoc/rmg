using System.Collections.Immutable;
using System.Numerics;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.WebApi.Songs;

/// <summary>
///     What a song was heard with, as the page had it: its unconventionality and facets, its parts and their mix, its
///     drum setup and drum groups, and its volume, each value given, or as the song drew it. Written as a format's
///     character and a fixed number of letters and digits (<see cref="Format" />), short enough for a link, which the page
///     writes and this reads: a link carries it to play the song as it was shared, and every event about a song carries
///     it to say what was heard.
/// </summary>
/// <param name="Unconventionality">How far the song strays from convention, from 0 to 127.</param>
/// <param name="Facets">Every facet's, in the order of <see cref="FacetOrder" />.</param>
/// <param name="Parts">Every part's, in the order of <see cref="PartOrder" />.</param>
/// <param name="DrumSetup">The drums given: 0 for the song's own, or one of <see cref="SetupOrder" />, from 1.</param>
/// <param name="DrumGroups">Every drum group's, in the order of <see cref="SongMix.DrumGroupNames" />.</param>
/// <param name="Volume">How loud the whole song plays, from 0 to 127.</param>
public sealed record SongSettings(
    Setting Unconventionality,
    ImmutableArray<Setting> Facets,
    ImmutableArray<PartSettings> Parts,
    int DrumSetup,
    ImmutableArray<DrumGroupSettings> DrumGroups,
    int Volume
)
{
    /// <summary>The format this writes, as its first character; one that is read differently takes the next.</summary>
    public const char FormatOne = '1';

    public static ImmutableArray<Facet> FacetOrder { get; } =
        [Facet.Feel, Facet.Groove, Facet.Fills, Facet.Form, Facet.Chords, Facet.Progression, Facet.Scale, Facet.Melody];

    public static ImmutableArray<TrackRole> PartOrder { get; } =
        [TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.CounterMelody, TrackRole.Drum];

    public static ImmutableArray<DrumSetup> SetupOrder { get; } = [Core.Composition.DrumSetup.Kit, Core.Composition.DrumSetup.KitAndPercussion, Core.Composition.DrumSetup.Percussion];

    // every value is 7 bits, as MIDI's are, a switch 1, whether a part plays 2 (its own, in, out) and the drum setup 2
    private static int Bits => 8 + FacetOrder.Length * 8 + PartOrder.Length * (2 + 8 + 7 + 8 + 1) + 2 + SongMix.DrumGroupNames.Length * 8 + 7;

    /// <summary>How many digits the number takes: as many as its bits need in base 62.</summary>
    public static int Length => (int)Math.Ceiling(Bits / Math.Log2(62));

    /// <summary>The settings as their format's character and the number, zero-padded to <see cref="Length" />.</summary>
    public string Format()
    {
        if (Facets.Length != FacetOrder.Length || Parts.Length != PartOrder.Length || DrumGroups.Length != SongMix.DrumGroupNames.Length)
            throw new InvalidOperationException("Settings name every facet, part and drum group.");

        var writer = new BitWriter();
        writer.Put(Unconventionality);
        foreach (var facet in Facets)
            writer.Put(facet);
        foreach (var part in Parts)
        {
            writer.Put(part.Plays, 2);
            writer.Put(part.Instrument);
            writer.Put(part.Volume, 7);
            writer.Put(part.Pan);
            writer.Put(part.IsOn ? 1 : 0, 1);
        }

        writer.Put(DrumSetup, 2);
        foreach (var group in DrumGroups)
        {
            writer.Put(group.IsOn ? 1 : 0, 1);
            writer.Put(group.Volume, 7);
        }

        writer.Put(Volume, 7);
        return FormatOne + Base62.Encode(writer.Number, Length);
    }

    /// <summary>Reads settings the page wrote, or none for anything that is not settings of the format.</summary>
    public static SongSettings? Parse(string? text)
    {
        if (text is null || text.Length != 1 + Length || text[0] != FormatOne || Base62.Decode(text[1..]) is not { } number || number >> Bits != 0)
            return null;

        var reader = new BitReader(number, Bits);
        var unconventionality = reader.Setting();
        ImmutableArray<Setting> facets = [..FacetOrder.Select(_ => reader.Setting())];
        ImmutableArray<PartSettings> parts = [..PartOrder.Select(_ => new PartSettings(reader.Take(2), reader.Setting(), reader.Take(7), reader.Setting(), reader.Take(1) == 1))];
        var drumSetup = reader.Take(2);
        ImmutableArray<DrumGroupSettings> groups = [..SongMix.DrumGroupNames.Select(_ => new DrumGroupSettings(reader.Take(1) == 1, reader.Take(7)))];
        var volume = reader.Take(7);

        // a part plays as its own, in or out, and a drum setup is one of three, or none
        if (parts.Any(x => x.Plays > 2))
            return null;

        return new SongSettings(unconventionality, facets, parts, drumSetup, groups, volume);
    }

    /// <summary>
    ///     What of the settings names the song, as a seed does: every value given that changes its notes, the
    ///     unconventionality, a facet, a part in or out and the drum setup, as letters and digits; empty where none is,
    ///     for a song its seed alone names.
    /// </summary>
    public string Identity
    {
        get
        {
            var writer = new BitWriter();
            foreach (var setting in Facets.Prepend(Unconventionality))
                writer.Put(setting.IsGiven ? setting : new Setting(false, 0));
            foreach (var part in Parts)
                writer.Put(part.Plays, 2);
            writer.Put(DrumSetup, 2);
            return writer.Number.IsZero ? "" : Base62.Encode(writer.Number, 15).TrimStart('0');
        }
    }

    private sealed class BitWriter
    {
        public BigInteger Number { get; private set; } = BigInteger.Zero;

        public void Put(int value, int bits)
        {
            if (value < 0 || value >> bits != 0)
                throw new InvalidOperationException($"{value} does not fit in {bits} bits.");
            Number = (Number << bits) | value;
        }

        public void Put(Setting setting)
        {
            Put(setting.IsGiven ? 1 : 0, 1);
            Put(setting.Value, 7);
        }
    }

    private sealed class BitReader(BigInteger number, int bits)
    {
        private int _position = bits;

        public int Take(int count)
        {
            _position -= count;
            return (int)((number >> _position) & ((1 << count) - 1));
        }

        public Setting Setting() => new(Take(1) == 1, Take(7));
    }
}

/// <param name="IsGiven">Whether it was given, rather than drawn by the song.</param>
/// <param name="Value">What it is, from 0 to 127: as given, or as the song drew it.</param>
public sealed record Setting(bool IsGiven, int Value);

/// <param name="Plays">Whether the part is in the song: 0 as the song draws it, 1 given in, 2 given out.</param>
/// <param name="Instrument">Its General MIDI program, the drums' kit.</param>
/// <param name="Volume">How loud it plays, from 0 to 127.</param>
/// <param name="Pan">Where it sits, from 0, left, through 64, the middle, to 127, right.</param>
/// <param name="IsOn">Whether it is written in the file, rather than switched off.</param>
public sealed record PartSettings(int Plays, Setting Instrument, int Volume, Setting Pan, bool IsOn);

/// <param name="IsOn">Whether its drums are written in the file.</param>
/// <param name="Volume">How loud its drums play, from 0 to 127.</param>
public sealed record DrumGroupSettings(bool IsOn, int Volume);
