using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.WebApi.Songs;

/// <summary>
///     What a song was heard with, as the page had it: its unconventionality and facets, its tempo, key, meter, scale and
///     swing, its parts and their mix, its drum groups and drum pieces, and its volume, each value given, or as the song
///     drew it. Written as a format's character, then a character for every value of <see cref="StepScale" />'s steps, in
///     <see cref="Base62" />'s digits, each at a fixed place, then every other value as its bits, one after another, five to
///     a character, from 0 to V, the last character's left over as zeros: short enough for a link, which the page writes
///     and this reads, and in letters and digits alone, which a chat leaves as they are. A link carries it to play the song as
///     it was shared, and every event about a song carries it to say what was heard.
/// </summary>
/// <param name="Unconventionality">How far the song strays from convention, from 0 to 61.</param>
/// <param name="Facets">Every facet's, in the order of <see cref="FacetOrder" />.</param>
/// <param name="Tempo">Its tempo, by its step (<see cref="TempoScale" />).</param>
/// <param name="Key">Its key, as semitones above C.</param>
/// <param name="Meter">Its meter, by its place among <see cref="Core.Composition.Meter.Options" />.</param>
/// <param name="Mode">The scale it starts in, by its place among <see cref="Scales.All" />.</param>
/// <param name="Swing">How much it swings, from 0, straight, to 61, a triplet's swing.</param>
/// <param name="Length">How long it plays, by its step (<see cref="LengthScale" />).</param>
/// <param name="Parts">Every part's, in the order of <see cref="PartOrder" />.</param>
/// <param name="DrumGroups">Every drum group's, in the order of <see cref="SongMix.DrumGroupNames" />.</param>
/// <param name="Volume">How loud the whole song plays, from 0 to 61.</param>
/// <param name="DrumPieces">The drum pieces given, as a set, or not; every piece in or out, in the order of <see cref="Core.Composition.DrumPieces.All" />.</param>
/// <param name="Darkness">How dark its mood is, from 0 to 61, written behind the rest with <paramref name="Intensity" /> (<see cref="MoodLength" />).</param>
/// <param name="Intensity">How intense its mood is, from 0 to 61.</param>
public sealed record SongSettings(
    Setting Unconventionality,
    ImmutableArray<Setting> Facets,
    Setting Tempo,
    Setting Key,
    Setting Meter,
    Setting Mode,
    Setting Swing,
    Setting Length,
    ImmutableArray<PartSettings> Parts,
    ImmutableArray<DrumGroupSettings> DrumGroups,
    int Volume,
    DrumPieceSettings DrumPieces,
    Setting Darkness,
    Setting Intensity
)
{
    /// <summary>The format this writes, as its first character; one that is read differently takes the next.</summary>
    public const char FormatCharacter = 'B';

    public static ImmutableArray<Facet> FacetOrder { get; } =
        [Facet.Feel, Facet.Groove, Facet.Fills, Facet.Form, Facet.Chords, Facet.Progression, Facet.Scale, Facet.Melody, Facet.Sound];

    /// <summary>
    ///     The parts, by the names a request takes them by, every one the songs play but the riff's twin, which the page
    ///     mixes as its riff.
    /// </summary>
    public static ImmutableArray<string> PartOrder { get; } = ["melody", "chords", "bass", "pad", "counterMelody", "drum", "riff", "rhythm", "solo"];

    // the bits of a key, of twelve, of a meter and of a scale, of up to sixteen options each, of a part's plays, of three,
    // and of a program, of 128
    private const int KeyBits = 4;
    public const int MeterBits = 4;
    public const int ModeBits = 4;
    private const int PlaysBits = 2;
    private const int ProgramBits = 7;

    // the values of the steps, a character each: the unconventionality, the facets, the tempo, the swing, the length,
    // the volume, every part's volume and pan, and every drum group's volume
    private static int CharacterCount => 1 + FacetOrder.Length + 1 + 1 + 1 + 1 + 2 * PartOrder.Length + SongMix.DrumGroupNames.Length;

    // and the rest, bit by bit: whether each value is given, the key, the meter and the scale, every part's plays, whether
    // its pan and its instrument are given and its program, and whether the drum pieces are given and every piece's being in
    private static int BitCount => 1 + FacetOrder.Length + 6 + KeyBits + MeterBits + ModeBits + PartOrder.Length * (PlaysBits + 2 + ProgramBits)
                                   + 1 + Core.Composition.DrumPieces.All.Length;

    /// <summary>How many characters follow the format's.</summary>
    public static int FixedLength => CharacterCount + (BitCount + BitsPerCharacter - 1) / BitsPerCharacter;

    /// <summary>
    ///     How many characters the mood takes behind the fixed places, its darkness's and its intensity's values and whether
    ///     each is given, a character's two bits: settings written before it have none, their mood drawn.
    /// </summary>
    public const int MoodLength = 3;

    /// <summary>How many bits a character carries, from 0 to V, of the 62 digits the whole of those 32 values can be.</summary>
    private const int BitsPerCharacter = 5;

    private IEnumerable<Setting> GivenValues => [Unconventionality, ..Facets, Tempo, Key, Meter, Mode, Swing, Length];

    /// <summary>The settings as their format's character, a character for every value of the steps, and the rest's bits.</summary>
    public string Format()
    {
        if (Facets.Length != FacetOrder.Length || Parts.Length != PartOrder.Length || DrumGroups.Length != SongMix.DrumGroupNames.Length
            || DrumPieces.Pieces.Length != Core.Composition.DrumPieces.All.Length)
            throw new InvalidOperationException("Settings name every facet, part, drum group and drum piece.");

        List<int> characters = [Unconventionality.Value, ..Facets.Select(x => x.Value), Tempo.Value, Swing.Value, Length.Value, Volume];
        foreach (var part in Parts)
            characters.AddRange([part.Volume, part.Pan.Value]);
        characters.AddRange(DrumGroups.Select(x => x.Volume));
        if (characters.Any(x => x is < 0 or > StepScale.Last))
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
        Write(Mode.Value, ModeBits);
        foreach (var part in Parts)
        {
            Write(part.Plays, PlaysBits);
            bits.AddRange([part.Pan.IsGiven, part.Instrument.IsGiven]);
            Write(part.Instrument.Value, ProgramBits);
        }

        bits.AddRange([DrumPieces.IsGiven, ..DrumPieces.Pieces]);
        characters.AddRange(Pack(bits));
        characters.AddRange([Darkness.Value, Intensity.Value, (Darkness.IsGiven ? 1 : 0) + (Intensity.IsGiven ? 2 : 0)]);
        if (Darkness.Value is < 0 or > StepScale.Last || Intensity.Value is < 0 or > StepScale.Last)
            throw new InvalidOperationException("A mood does not fit in a character.");
        return FormatCharacter + new string([..characters.Select(x => Base62.Digits[x])]);
    }

    /// <summary>Bits five to a character, the first the lowest, the last character's left over as zeros.</summary>
    private static IEnumerable<int> Pack(IReadOnlyList<bool> bits)
    {
        for (var i = 0; i < bits.Count; i += BitsPerCharacter)
            yield return bits.Skip(i).Take(BitsPerCharacter).Select((x, bit) => x ? 1 << bit : 0).Sum();
    }

    /// <summary>Reads settings the page wrote, or none for anything that is not settings of this format.</summary>
    public static SongSettings? Parse(string? text)
    {
        if (text is null || text.Length != 1 + FixedLength && text.Length != 1 + FixedLength + MoodLength || text[0] != FormatCharacter)
            return null;

        // its mood where it has one, a step and whether it is given for each, as written; drawn where it has none
        var (darkness, intensity) = (new Setting(false, StepScale.Middle), new Setting(false, StepScale.Middle));
        if (text.Length == 1 + FixedLength + MoodLength)
        {
            var mood = text[(1 + FixedLength)..].Select(x => Base62.Digits.IndexOf(x)).ToArray();
            if (mood[..2].Any(x => x is < 0 or > StepScale.Last) || mood[2] is < 0 or > 3)
                return null;
            (darkness, intensity) = (new Setting((mood[2] & 1) == 1, mood[0]), new Setting((mood[2] & 2) == 2, mood[1]));
        }

        var values = text[1..(1 + FixedLength)].Select(x => Base62.Digits.IndexOf(x)).ToArray();
        // a value no more than the last step, and a character of bits no more than five of them, its padding zeros, so
        // that settings are written one way only
        if (values[..CharacterCount].Any(x => x is < 0 or > StepScale.Last) || values[CharacterCount..].Any(x => x is < 0 or >= 1 << BitsPerCharacter)
            || values[^1] >> (BitCount - (FixedLength - CharacterCount - 1) * BitsPerCharacter) != 0)
            return null;

        var at = 0;
        int Next() => values[at++];
        var given = Enumerable.Range(0, 1 + FacetOrder.Length + 1).Select(_ => Next()).ToArray();
        var swing = Next();
        var length = Next();
        var volume = Next();
        var mixes = PartOrder.Select(_ => (Volume: Next(), Pan: Next())).ToArray();
        var groups = SongMix.DrumGroupNames.Select(_ => new DrumGroupSettings(Next())).ToImmutableArray();
        var bits = values[at..].SelectMany(x => Enumerable.Range(0, BitsPerCharacter).Select(bit => (x >> bit & 1) == 1)).ToArray();

        var b = 0;
        bool Bit() => bits[b++];
        int Read(int count) => Enumerable.Range(0, count).Sum(bit => Bit() ? 1 << bit : 0);
        var isGiven = Enumerable.Range(0, 1 + FacetOrder.Length + 6).Select(_ => Bit()).ToArray();
        var key = Read(KeyBits);
        var meter = Read(MeterBits);
        var mode = Read(ModeBits);
        var parts = mixes.Select(mix =>
            {
                var plays = Read(PlaysBits);
                var pan = new Setting(Bit(), mix.Pan);
                var instrumentGiven = Bit();
                return new PartSettings(plays, new Setting(instrumentGiven, Read(ProgramBits)), mix.Volume, pan);
            }
        ).ToImmutableArray();
        var pieces = new DrumPieceSettings(Bit(), [..Core.Composition.DrumPieces.All.Select(_ => Bit())]);

        // a part plays as its own, in or out; a key is one of twelve, a meter and a scale one of their options, and drum
        // pieces given one or more, as a request takes them
        if (parts.Any(x => x.Plays > 2) || key > 11 || meter >= Core.Composition.Meter.Options.Length || mode >= Scales.All.Length || pieces.IsGiven && !pieces.Pieces.Contains(true))
            return null;

        var facets = given[1..^1].Select((value, i) => new Setting(isGiven[1 + i], value));
        return new SongSettings(new Setting(isGiven[0], given[0]), [..facets], new Setting(isGiven[^6], given[^1]), new Setting(isGiven[^5], key),
            new Setting(isGiven[^4], meter), new Setting(isGiven[^3], mode), new Setting(isGiven[^2], swing), new Setting(isGiven[^1], length), parts, groups, volume,
            pieces, darkness, intensity);
    }

    /// <summary>
    ///     The song of the seed asked for with these settings, as the page asks for it: every value given as given and the
    ///     rest the song's own, every part's and drum group's mix, and the drum pieces where they are given.
    /// </summary>
    public GenerateSongRequest ToRequest(string seed)
    {
        int? Given(Setting setting) => setting.IsGiven ? setting.Value : null;
        return new GenerateSongRequest(
            seed,
            Given(Unconventionality),
            FacetOrder.Zip(Facets).Where(x => x.Second.IsGiven).ToDictionary(x => x.First.ToString(), x => x.Second.Value),
            Volume,
            PartOrder.Zip(Parts).ToDictionary(
                x => x.First,
                x => new PartRequest(x.Second.Plays switch { 1 => true, 2 => false, _ => null }, Given(x.Second.Instrument), x.Second.Volume, Given(x.Second.Pan))
            ),
            SongMix.DrumGroupNames.Zip(DrumGroups).ToDictionary(x => x.First, x => new DrumGroupRequest(x.Second.Volume)),
            Given(Tempo),
            Given(Key),
            Given(Meter),
            DrumPieces.IsGiven ? [..Core.Composition.DrumPieces.All.Where((_, i) => DrumPieces.Pieces[i]).Select(x => x.Name)] : null,
            Given(Mode),
            Given(Swing),
            null,
            Given(Length),
            null,
            null,
            Given(Darkness),
            Given(Intensity)
        );
    }

    /// <summary>
    ///     What of the settings names the song, as a seed does, whatever changes its notes: the switches of which of the
    ///     unconventionality, the facets, the tempo, the key, the meter, the scale, the swing and the length are given, five to a
    ///     character, the values of those given alone, every part's plays, the drum pieces given, five to a character behind
    ///     a 1, with the zeros at its end left off; empty where nothing is given, for a song its seed alone names. The switches say
    ///     how many values follow, and the parts are as many as ever, so that it reads one way only.
    /// </summary>
    public string Identity
    {
        get
        {
            var values = Pack([..GivenValues.Select(x => x.IsGiven)]).ToList();
            values.AddRange(GivenValues.Where(x => x.IsGiven).Select(x => x.Value));
            values.AddRange(Parts.Select(x => x.Plays));
            if (DrumPieces.IsGiven)
                values.AddRange([1, ..Pack(DrumPieces.Pieces)]);
            // and the mood where it is given, behind a 2, so that a song given none keeps the name it had before
            if (Darkness.IsGiven || Intensity.IsGiven)
                values.AddRange([2, ..Pack([Darkness.IsGiven, Intensity.IsGiven]), ..new[] { Darkness, Intensity }.Where(x => x.IsGiven).Select(x => x.Value)]);

            return new string([..values.Select(x => Base62.Digits[x])]).TrimEnd('0');
        }
    }
}

/// <param name="IsGiven">Whether it was given, rather than drawn by the song.</param>
/// <param name="Value">What it is, from 0 to 61 or the count of its options: as given, or as the song drew it.</param>
public sealed record Setting(bool IsGiven, int Value);

/// <param name="Plays">Whether the part is in the song: 0 as the song draws it, 1 given in, 2 given out.</param>
/// <param name="Instrument">Its General MIDI program, the drums' kit, from 0 to 127.</param>
/// <param name="Volume">How loud it plays, from 0 to 61.</param>
/// <param name="Pan">Where it sits, from 0, left, through 31, the middle, to 61, right.</param>
public sealed record PartSettings(int Plays, Setting Instrument, int Volume, Setting Pan);

/// <param name="IsGiven">Whether the drum pieces are given, in place of those the song draws.</param>
/// <param name="Pieces">Every piece's being in the song, in the order of <see cref="Core.Composition.DrumPieces.All" />, where they are given.</param>
public sealed record DrumPieceSettings(bool IsGiven, ImmutableArray<bool> Pieces);

/// <param name="Volume">How loud its drums play, from 0 to 61.</param>
public sealed record DrumGroupSettings(int Volume);
