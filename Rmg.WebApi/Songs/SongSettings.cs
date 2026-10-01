using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.WebApi.Songs;

/// <summary>
///     What a song was heard with, as the page had it: its unconventionality and facets, its tempo, key and meter, its
///     parts and their mix, its drum setup and drum groups, and its volume, each value given, or as the song drew it.
///     Written as a format's character and then a character for each value, in <see cref="Base64" />'s digits, every
///     value from 0 to 63 a character of its own at a fixed place, the values of two characters, the instruments, and
///     the switches, packed six to a character, after them; short enough for a link, which the page writes and this
///     reads: a link carries it to play the song as it was shared, and every event about a song carries it to say what
///     was heard.
/// </summary>
/// <param name="Unconventionality">How far the song strays from convention, from 0 to 63.</param>
/// <param name="Facets">Every facet's, in the order of <see cref="FacetOrder" />.</param>
/// <param name="Tempo">Its tempo, by its step (<see cref="TempoScale" />).</param>
/// <param name="Key">Its key, as semitones above C.</param>
/// <param name="Meter">Its meter, by its place among <see cref="Core.Composition.Meter.Options" />.</param>
/// <param name="Parts">Every part's, in the order of <see cref="PartOrder" />.</param>
/// <param name="DrumSetup">The drums given: 0 for the song's own, or one of <see cref="SetupOrder" />, from 1.</param>
/// <param name="DrumGroups">Every drum group's, in the order of <see cref="SongMix.DrumGroupNames" />.</param>
/// <param name="Volume">How loud the whole song plays, from 0 to 63.</param>
public sealed record SongSettings(
    Setting Unconventionality,
    ImmutableArray<Setting> Facets,
    Setting Tempo,
    Setting Key,
    Setting Meter,
    ImmutableArray<PartSettings> Parts,
    int DrumSetup,
    ImmutableArray<DrumGroupSettings> DrumGroups,
    int Volume
)
{
    /// <summary>The format this writes, as its first character; one that is read differently takes the next.</summary>
    public const char FormatFour = '4';

    /// <summary>
    ///     The format before, which gave a tempo by its place among the tempos a song draws, read with its tempo as the
    ///     song draws it.
    /// </summary>
    public const char FormatThree = '3';

    /// <summary>The format before that, which named every part but the solo part, which it plays as the song draws it.</summary>
    public const char FormatTwo = '2';

    public static ImmutableArray<Facet> FacetOrder { get; } =
        [Facet.Feel, Facet.Groove, Facet.Fills, Facet.Form, Facet.Chords, Facet.Progression, Facet.Scale, Facet.Melody, Facet.Sound];

    /// <summary>
    ///     The parts, by the names a request takes them by, every one the songs play.
    /// </summary>
    public static ImmutableArray<string> PartOrder { get; } = ["melody", "chords", "bass", "pad", "counterMelody", "drum", "riff", "rhythm", "solo"];

    /// <summary>How many parts the format before named, the first of <see cref="PartOrder" />.</summary>
    private const int PartsOfFormatTwo = 8;

    /// <summary>A part the format before did not name: as the song draws it, at its fullest, in the file.</summary>
    private static PartSettings Unnamed { get; } = new(0, new Setting(false, 0), 63, new Setting(false, 32), true);

    public static ImmutableArray<DrumSetup> SetupOrder { get; } = [Core.Composition.DrumSetup.Kit, Core.Composition.DrumSetup.KitAndPercussion, Core.Composition.DrumSetup.Percussion];

    // the values of a character each, then the instruments' two characters, then the switches, six to a character: whether
    // each value is given, and every part's plays (2), whether it is in the file and whether its pan is given, and whether
    // every drum group is in the file
    private static int ValueCount(int parts) => 1 + FacetOrder.Length + 3 + 1 + 2 * parts + 1 + SongMix.DrumGroupNames.Length;

    private static int SwitchCount(int parts) => 1 + FacetOrder.Length + 3 + 4 * parts + SongMix.DrumGroupNames.Length;

    private static int LengthOf(int parts) => ValueCount(parts) + 2 * parts + (SwitchCount(parts) + 5) / 6;

    /// <summary>How many characters follow the format's.</summary>
    public static int Length => LengthOf(PartOrder.Length);

    private IEnumerable<Setting> GivenValues => [Unconventionality, ..Facets, Tempo, Key, Meter];

    /// <summary>The settings as their format's character and a character for each value.</summary>
    public string Format()
    {
        if (Facets.Length != FacetOrder.Length || Parts.Length != PartOrder.Length || DrumGroups.Length != SongMix.DrumGroupNames.Length)
            throw new InvalidOperationException("Settings name every facet, part and drum group.");

        var values = new List<int>();
        values.AddRange(GivenValues.Select(x => x.Value));
        values.Add(Volume);
        foreach (var part in Parts)
            values.AddRange([part.Volume, part.Pan.Value]);
        values.Add(DrumSetup);
        values.AddRange(DrumGroups.Select(x => x.Volume));
        foreach (var part in Parts)
        {
            var instrument = (part.Instrument.IsGiven ? 128 : 0) + part.Instrument.Value;
            values.AddRange([instrument >> 6, instrument & 63]);
        }

        List<bool> switches = [..GivenValues.Select(x => x.IsGiven)];
        foreach (var part in Parts)
            switches.AddRange([(part.Plays & 2) != 0, (part.Plays & 1) != 0, part.IsOn, part.Pan.IsGiven]);
        switches.AddRange(DrumGroups.Select(x => x.IsOn));
        for (var i = 0; i < switches.Count; i += 6)
            values.Add(switches.Skip(i).Take(6).Select((x, bit) => x ? 1 << bit : 0).Sum());

        if (values.Any(x => x is < 0 or > 63))
            throw new InvalidOperationException("A value does not fit in a character.");
        return FormatFour + new string([..values.Select(x => Base64.Digits[x])]);
    }

    /// <summary>
    ///     Reads settings the page wrote, or none for anything that is not settings of the format; the formats before too,
    ///     their tempo, and the solo part the oldest does not name, as the song draws them.
    /// </summary>
    public static SongSettings? Parse(string? text)
    {
        var partCount = text?.Length > 0 ? text[0] switch
        {
            FormatFour or FormatThree => PartOrder.Length,
            FormatTwo => PartsOfFormatTwo,
            _ => 0
        } : 0;
        if (text is null || partCount == 0 || text.Length != 1 + LengthOf(partCount))
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
        var groupVolumes = SongMix.DrumGroupNames.Select(_ => Next()).ToArray();
        var instruments = named.Select(_ => Next() * 64 + Next()).ToArray();
        var switches = values[at..].SelectMany(x => Enumerable.Range(0, 6).Select(bit => (x >> bit & 1) == 1)).ToArray();

        var s = 0;
        var settings = given.Select(value => new Setting(switches[s++], value)).ToArray();
        var parts = named.Select((_, i) =>
            {
                var plays = (switches[s++] ? 2 : 0) + (switches[s++] ? 1 : 0);
                var isOn = switches[s++];
                var pan = new Setting(switches[s++], mixes[i].Pan);
                return new PartSettings(plays, new Setting(instruments[i] >= 128, instruments[i] & 127), mixes[i].Volume, pan, isOn);
            }
        ).Concat(PartOrder.Skip(partCount).Select(_ => Unnamed)).ToImmutableArray();
        var groups = groupVolumes.Select(x => new DrumGroupSettings(switches[s++], x)).ToImmutableArray();

        // a part plays as its own, in or out; a drum setup is one of three, or none; a key one of twelve; and a meter one
        // of its options, as every character is a tempo's step
        if (parts.Any(x => x.Plays > 2) || drumSetup > SetupOrder.Length || instruments.Any(x => x >= 256)
            || settings[^2].Value > 11 || settings[^1].Value >= Core.Composition.Meter.Options.Length)
            return null;
        if (text[0] != FormatFour)
            settings[^3] = new Setting(false, TempoScale.Middle);

        return new SongSettings(settings[0], [..settings[1..^3]], settings[^3], settings[^2], settings[^1], parts, drumSetup, groups, volume);
    }

    /// <summary>
    ///     What of the settings names the song, as a seed does, whatever changes its notes: the switches of which of the
    ///     unconventionality, the facets, the tempo, the key and the meter are given, six to a character, the values of
    ///     those given alone, every part's plays, and the drum setup, with the zeros at its end left off; empty where
    ///     nothing is given, for a song its seed alone names. The switches say how many values follow, so that it reads
    ///     one way only. A part the format before did not name comes after the drum setup, so that a song naming it as
    ///     the song draws it is named as it was.
    /// </summary>
    public string Identity
    {
        get
        {
            var switches = GivenValues.Select(x => x.IsGiven).ToArray();
            var values = new List<int>();
            for (var i = 0; i < switches.Length; i += 6)
                values.Add(switches.Skip(i).Take(6).Select((x, bit) => x ? 1 << bit : 0).Sum());
            values.AddRange(GivenValues.Where(x => x.IsGiven).Select(x => x.Value));
            values.AddRange(Parts.Take(PartsOfFormatTwo).Select(x => x.Plays));
            values.Add(DrumSetup);
            values.AddRange(Parts.Skip(PartsOfFormatTwo).Select(x => x.Plays));
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
/// <param name="IsOn">Whether it is written in the file, rather than switched off.</param>
public sealed record PartSettings(int Plays, Setting Instrument, int Volume, Setting Pan, bool IsOn);

/// <param name="IsOn">Whether its drums are written in the file.</param>
/// <param name="Volume">How loud its drums play, from 0 to 63.</param>
public sealed record DrumGroupSettings(bool IsOn, int Volume);
