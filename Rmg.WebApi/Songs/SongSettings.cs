using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.WebApi.Songs;

/// <summary>
///     What a song was heard with, as the page had it: its unconventionality and facets, its tempo, key and meter, its
///     parts and their mix, its drum groups and drum pieces, and its volume, each value given, or as the song drew it.
///     Written as a format's character, then a character for every value of 64 steps, in <see cref="Base64" />'s digits,
///     each at a fixed place, then every other value as its bits, one after another, six to a character: short enough
///     for a link, which the page writes and this reads. A link carries it to play the song as it was shared, and every
///     event about a song carries it to say what was heard.
/// </summary>
/// <param name="Unconventionality">How far the song strays from convention, from 0 to 63.</param>
/// <param name="Facets">Every facet's, in the order of <see cref="FacetOrder" />.</param>
/// <param name="Tempo">Its tempo, by its step (<see cref="TempoScale" />).</param>
/// <param name="Key">Its key, as semitones above C.</param>
/// <param name="Meter">Its meter, by its place among <see cref="Core.Composition.Meter.Options" />.</param>
/// <param name="Parts">Every part's, in the order of <see cref="PartOrder" />.</param>
/// <param name="DrumGroups">Every drum group's, in the order of <see cref="SongMix.DrumGroupNames" />.</param>
/// <param name="Volume">How loud the whole song plays, from 0 to 63.</param>
/// <param name="DrumPieces">The drum pieces given, as a set, or not; every piece in or out, in the order of <see cref="Core.Composition.DrumPieces.All" />.</param>
public sealed record SongSettings(
    Setting Unconventionality,
    ImmutableArray<Setting> Facets,
    Setting Tempo,
    Setting Key,
    Setting Meter,
    ImmutableArray<PartSettings> Parts,
    ImmutableArray<DrumGroupSettings> DrumGroups,
    int Volume,
    DrumPieceSettings DrumPieces
)
{
    /// <summary>The format this writes, as its first character; one that is read differently takes the next.</summary>
    public const char FormatSix = '6';

    /// <summary>
    ///     The format before, every value a character, a drum setup and every part's and drum group's being in the file
    ///     among them, which it is read without, the drum pieces making the setup and everything being in the file.
    /// </summary>
    public const char FormatFive = '5';

    /// <summary>The format before that, which gave no drum pieces, read with the song's own.</summary>
    public const char FormatFour = '4';

    /// <summary>
    ///     A format before, which gave a tempo by its place among the tempos a song draws, read with its tempo as the
    ///     song draws it.
    /// </summary>
    public const char FormatThree = '3';

    /// <summary>The oldest format, which named every part but the solo part, which it plays as the song draws it.</summary>
    public const char FormatTwo = '2';

    public static ImmutableArray<Facet> FacetOrder { get; } =
        [Facet.Feel, Facet.Groove, Facet.Fills, Facet.Form, Facet.Chords, Facet.Progression, Facet.Scale, Facet.Melody, Facet.Sound];

    /// <summary>
    ///     The parts, by the names a request takes them by, every one the songs play.
    /// </summary>
    public static ImmutableArray<string> PartOrder { get; } = ["melody", "chords", "bass", "pad", "counterMelody", "drum", "riff", "rhythm", "solo"];

    /// <summary>How many parts the oldest format named, the first of <see cref="PartOrder" />.</summary>
    private const int PartsOfFormatTwo = 8;

    /// <summary>A part the oldest format did not name: as the song draws it, at its fullest.</summary>
    private static PartSettings Unnamed { get; } = new(0, new Setting(false, 0), 63, new Setting(false, 32));

    // the bits of a key, of twelve, of a meter, of up to sixteen options, of a part's plays, of three, and of a program, of 128
    private const int KeyBits = 4;
    public const int MeterBits = 4;
    private const int PlaysBits = 2;
    private const int ProgramBits = 7;

    // the values of 64 steps, a character each: the unconventionality, the facets, the tempo, the volume, every part's
    // volume and pan, and every drum group's volume
    private static int CharacterCount => 1 + FacetOrder.Length + 1 + 1 + 2 * PartOrder.Length + SongMix.DrumGroupNames.Length;

    // and the rest, bit by bit: whether each value is given, the key and the meter, every part's plays, whether its pan
    // and its instrument are given and its program, and whether the drum pieces are given and every piece's being in
    private static int BitCount => 1 + FacetOrder.Length + 3 + KeyBits + MeterBits + PartOrder.Length * (PlaysBits + 2 + ProgramBits)
                                   + 1 + Core.Composition.DrumPieces.All.Length;

    /// <summary>How many characters follow the format's.</summary>
    public static int Length => CharacterCount + (BitCount + 5) / 6;

    private IEnumerable<Setting> GivenValues => [Unconventionality, ..Facets, Tempo, Key, Meter];

    /// <summary>The settings as their format's character, a character for every value of 64 steps, and the rest's bits.</summary>
    public string Format()
    {
        if (Facets.Length != FacetOrder.Length || Parts.Length != PartOrder.Length || DrumGroups.Length != SongMix.DrumGroupNames.Length
            || DrumPieces.Pieces.Length != Core.Composition.DrumPieces.All.Length)
            throw new InvalidOperationException("Settings name every facet, part, drum group and drum piece.");

        List<int> characters = [Unconventionality.Value, ..Facets.Select(x => x.Value), Tempo.Value, Volume];
        foreach (var part in Parts)
            characters.AddRange([part.Volume, part.Pan.Value]);
        characters.AddRange(DrumGroups.Select(x => x.Volume));
        if (characters.Any(x => x is < 0 or > 63))
            throw new InvalidOperationException("A value does not fit in a character.");

        var bits = new List<bool>();
        void Write(int value, int count)
        {
            if (value < 0 || value >= 1 << count)
                throw new InvalidOperationException($"{value} does not fit in {count} bits.");
            bits.AddRange(Enumerable.Range(0, count).Select(bit => (value >> bit & 1) == 1));
        }

        bits.AddRange(GivenValues.Select(x => x.IsGiven));
        Write(Key.Value, KeyBits);
        Write(Meter.Value, MeterBits);
        foreach (var part in Parts)
        {
            Write(part.Plays, PlaysBits);
            bits.AddRange([part.Pan.IsGiven, part.Instrument.IsGiven]);
            Write(part.Instrument.Value, ProgramBits);
        }

        bits.AddRange([DrumPieces.IsGiven, ..DrumPieces.Pieces]);
        characters.AddRange(Pack(bits));
        return FormatSix + new string([..characters.Select(x => Base64.Digits[x])]);
    }

    /// <summary>Bits six to a character, the first the lowest, the last character's left over as zeros.</summary>
    private static IEnumerable<int> Pack(IReadOnlyList<bool> bits)
    {
        for (var i = 0; i < bits.Count; i += 6)
            yield return bits.Skip(i).Take(6).Select((x, bit) => x ? 1 << bit : 0).Sum();
    }

    /// <summary>
    ///     Reads settings the page wrote, or none for anything that is not settings of a format; the formats before too
    ///     (<see cref="ParseBefore" />).
    /// </summary>
    public static SongSettings? Parse(string? text)
    {
        if (text?.Length > 0 && text[0] != FormatSix)
            return ParseBefore(text);
        if (text is null || text.Length != 1 + Length)
            return null;

        var values = text[1..].Select(x => Base64.Digits.IndexOf(x)).ToArray();
        if (values.Any(x => x < 0))
            return null;

        var at = 0;
        int Next() => values[at++];
        var given = Enumerable.Range(0, 1 + FacetOrder.Length + 1).Select(_ => Next()).ToArray();
        var volume = Next();
        var mixes = PartOrder.Select(_ => (Volume: Next(), Pan: Next())).ToArray();
        var groups = SongMix.DrumGroupNames.Select(_ => new DrumGroupSettings(Next())).ToImmutableArray();
        var bits = values[at..].SelectMany(x => Enumerable.Range(0, 6).Select(bit => (x >> bit & 1) == 1)).ToArray();

        var b = 0;
        bool Bit() => bits[b++];
        int Read(int count) => Enumerable.Range(0, count).Sum(bit => Bit() ? 1 << bit : 0);
        var isGiven = Enumerable.Range(0, 1 + FacetOrder.Length + 3).Select(_ => Bit()).ToArray();
        var key = Read(KeyBits);
        var meter = Read(MeterBits);
        var parts = mixes.Select(mix =>
            {
                var plays = Read(PlaysBits);
                var pan = new Setting(Bit(), mix.Pan);
                var instrumentGiven = Bit();
                return new PartSettings(plays, new Setting(instrumentGiven, Read(ProgramBits)), mix.Volume, pan);
            }
        ).ToImmutableArray();
        var pieces = new DrumPieceSettings(Bit(), [..Core.Composition.DrumPieces.All.Select(_ => Bit())]);

        // a part plays as its own, in or out; a key is one of twelve, and a meter one of its options
        if (parts.Any(x => x.Plays > 2) || key > 11 || meter >= Core.Composition.Meter.Options.Length)
            return null;

        var facets = given[1..^1].Select((value, i) => new Setting(isGiven[1 + i], value));
        return new SongSettings(new Setting(isGiven[0], given[0]), [..facets], new Setting(isGiven[^3], given[^1]), new Setting(isGiven[^2], key),
            new Setting(isGiven[^1], meter), parts, groups, volume, pieces);
    }

    /// <summary>
    ///     Reads settings of a format before: a character for every value, the instruments' two each, and the switches after
    ///     them, six to a character; their drum pieces, their tempo, and the solo part the oldest does not name as the song
    ///     draws them, and their drum setup and their being in the file let go of.
    /// </summary>
    private static SongSettings? ParseBefore(string text)
    {
        var partCount = text[0] switch
        {
            FormatFive or FormatFour or FormatThree => PartOrder.Length,
            FormatTwo => PartsOfFormatTwo,
            _ => 0
        };
        var hasPieces = text[0] == FormatFive;
        var switchCount = 1 + FacetOrder.Length + 3 + 4 * partCount + SongMix.DrumGroupNames.Length + (hasPieces ? 1 + Core.Composition.DrumPieces.All.Length : 0);
        var valueCount = 1 + FacetOrder.Length + 3 + 1 + 2 * partCount + 1 + SongMix.DrumGroupNames.Length;
        if (partCount == 0 || text.Length != 1 + valueCount + 2 * partCount + (switchCount + 5) / 6)
            return null;

        var values = text[1..].Select(x => Base64.Digits.IndexOf(x)).ToArray();
        if (values.Any(x => x < 0))
            return null;

        var at = 0;
        int Next() => values[at++];
        var given = Enumerable.Range(0, 1 + FacetOrder.Length + 3).Select(_ => Next()).ToArray();
        var volume = Next();
        var named = PartOrder.Take(partCount).ToArray();
        var mixes = named.Select(_ => (Volume: Next(), Pan: Next())).ToArray();
        var drumSetup = Next();
        var groups = SongMix.DrumGroupNames.Select(_ => new DrumGroupSettings(Next())).ToImmutableArray();
        var instruments = named.Select(_ => Next() * 64 + Next()).ToArray();
        var switches = values[at..].SelectMany(x => Enumerable.Range(0, 6).Select(bit => (x >> bit & 1) == 1)).ToArray();

        var s = 0;
        var settings = given.Select(value => new Setting(switches[s++], value)).ToArray();
        var parts = named.Select((_, i) =>
            {
                var plays = (switches[s++] ? 2 : 0) + (switches[s++] ? 1 : 0);
                // whether it was in the file, which every part now is
                s++;
                var pan = new Setting(switches[s++], mixes[i].Pan);
                return new PartSettings(plays, new Setting(instruments[i] >= 128, instruments[i] & 127), mixes[i].Volume, pan);
            }
        ).Concat(PartOrder.Skip(partCount).Select(_ => Unnamed)).ToImmutableArray();
        // whether every drum group was in the file, which every one now is
        s += SongMix.DrumGroupNames.Length;
        var pieces = hasPieces
            ? new DrumPieceSettings(switches[s++], [..Core.Composition.DrumPieces.All.Select(_ => switches[s++])])
            : DrumPieceSettings.Drawn;

        // a part plays as its own, in or out; a drum setup is one of three, or none; a key one of twelve; and a meter one
        // of its options, as every character is a tempo's step
        if (parts.Any(x => x.Plays > 2) || drumSetup > 3 || instruments.Any(x => x >= 256)
            || settings[^2].Value > 11 || settings[^1].Value >= Core.Composition.Meter.Options.Length)
            return null;
        if (text[0] is FormatThree or FormatTwo)
            settings[^3] = new Setting(false, TempoScale.Middle);

        return new SongSettings(settings[0], [..settings[1..^3]], settings[^3], settings[^2], settings[^1], parts, groups, volume, pieces);
    }

    /// <summary>
    ///     What of the settings names the song, as a seed does, whatever changes its notes: the switches of which of the
    ///     unconventionality, the facets, the tempo, the key and the meter are given, six to a character, the values of
    ///     those given alone, every part's plays, and a 0 where a drum setup was, with the zeros at its end left off; empty
    ///     where nothing is given, for a song its seed alone names. The switches say how many values follow, so that it
    ///     reads one way only. A part the oldest format did not name comes after the 0, so that a song naming it as the
    ///     song draws it is named as it was, and the drum pieces given after it, six to a character behind a 1, so that a
    ///     song with its own drums is named as it was.
    /// </summary>
    public string Identity
    {
        get
        {
            var values = Pack([..GivenValues.Select(x => x.IsGiven)]).ToList();
            values.AddRange(GivenValues.Where(x => x.IsGiven).Select(x => x.Value));
            values.AddRange(Parts.Take(PartsOfFormatTwo).Select(x => x.Plays));
            values.Add(0);
            values.AddRange(Parts.Skip(PartsOfFormatTwo).Select(x => x.Plays));
            if (DrumPieces.IsGiven)
                values.AddRange([1, ..Pack(DrumPieces.Pieces)]);

            return new string([..values.Select(x => Base64.Digits[x])]).TrimEnd('0');
        }
    }
}

/// <param name="IsGiven">Whether it was given, rather than drawn by the song.</param>
/// <param name="Value">What it is, from 0 to 63 or the count of its options: as given, or as the song drew it.</param>
public sealed record Setting(bool IsGiven, int Value);

/// <param name="Plays">Whether the part is in the song: 0 as the song draws it, 1 given in, 2 given out.</param>
/// <param name="Instrument">Its General MIDI program, the drums' kit, from 0 to 127.</param>
/// <param name="Volume">How loud it plays, from 0 to 63.</param>
/// <param name="Pan">Where it sits, from 0, left, through 32, the middle, to 63, right.</param>
public sealed record PartSettings(int Plays, Setting Instrument, int Volume, Setting Pan);

/// <param name="IsGiven">Whether the drum pieces are given, in place of those the song draws.</param>
/// <param name="Pieces">Every piece's being in the song, in the order of <see cref="Core.Composition.DrumPieces.All" />, where they are given.</param>
public sealed record DrumPieceSettings(bool IsGiven, ImmutableArray<bool> Pieces)
{
    /// <summary>The song's own drum pieces.</summary>
    public static DrumPieceSettings Drawn { get; } = new(false, [..Core.Composition.DrumPieces.All.Select(_ => false)]);
}

/// <param name="Volume">How loud its drums play, from 0 to 63.</param>
public sealed record DrumGroupSettings(int Volume);
