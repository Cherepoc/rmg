using System.Collections.Immutable;
using System.Text.Json;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.WebApi.Songs;

/// <summary>
///     A song to generate: its seed, what is given in place of what it draws, and how it is to be heard. Every amount is
///     a step from 0 to 63 (<see cref="StepScale" />), a character of the page's settings each; anything left out is the song's own.
/// </summary>
/// <param name="Seed">
///     Seed of the song, in letters and digits (<see cref="Rmg.Core.Base64" />), all 64 of its bits, which a number in
///     JSON could not carry. None asks for a random one, which the response reports back, so a song heard once can be
///     asked for again.
/// </param>
/// <param name="Unconventionality">
///     How far the song strays from convention, from 0 for the plainest to 63 for the most experimental, which its
///     facets stray around. None lets the song draw its own, which keeps well away from both ends.
/// </param>
/// <param name="Facets">A facet of the unconventionality given outright, by its name, such as "chords".</param>
/// <param name="Volume">How loud the whole song plays, 63 as it is made. None is 63.</param>
/// <param name="Parts">A part given, by its role's name: "melody", "chords", "bass", "pad", "counterMelody" or "drum".</param>
/// <param name="DrumPieces">
///     The drum pieces the song plays, by their names (<c>/api/songs/options</c>), in place of those it draws, which make
///     its setup.
/// </param>
/// <param name="DrumGroups">A drum group's mix, by its name, such as "kick".</param>
/// <param name="Tempo">The tempo, by its step from 0, 60 beats a minute, through 32, 120, to 63, 240 (<see cref="TempoScale" />).</param>
/// <param name="Key">The key, as semitones above C, from 0 to 11.</param>
/// <param name="Meter">The meter, by its place among the meters a song may play in (<c>/api/songs/options</c>).</param>
/// <param name="Mode">The scale the song starts in, on its key, by its place among the scales (<c>/api/songs/options</c>).</param>
/// <param name="Swing">How much the song swings, from 0, straight, to 63, a triplet's swing, where its meter pairs its notes.</param>
/// <param name="Length">How long the song is to play, by its step (<see cref="LengthScale" />), which its form comes as near as it can.</param>
/// <param name="Genre">
///     The genre the song's draws keep to: every choice it names (<c>/api/genres/choices</c>), and its options' amounts in
///     whole percents, from 0 to 100, each one number, alike at every end, or three, at the plain end, in the middle and at
///     the wild: a pool's weights, a chance's chances, a value's 50 as it is, and a range's ends.
/// </param>
public sealed record GenerateSongRequest(
    string? Seed = null,
    int? Unconventionality = null,
    IReadOnlyDictionary<string, int>? Facets = null,
    int? Volume = null,
    IReadOnlyDictionary<string, PartRequest>? Parts = null,
    IReadOnlyDictionary<string, DrumGroupRequest>? DrumGroups = null,
    int? Tempo = null,
    int? Key = null,
    int? Meter = null,
    IReadOnlyList<string>? DrumPieces = null,
    int? Mode = null,
    int? Swing = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonElement>>? Genre = null,
    int? Length = null
)
{
    /// <summary>A pan from -1, left, to 1, right, as its nearest step, the middle step in the middle.</summary>
    public static int ToPanStep(double pan) => StepScale.ToStep((pan + 1) / 2);

    private static double FromPanStep(int step) => 2 * StepScale.ToAmount(step) - 1;

    /// <summary>
    ///     Reads the request into what is given in place of the song's draws and how it is to be heard, or says what
    ///     cannot be read.
    /// </summary>
    public bool TryRead(out ulong? seed, out SongOverrides overrides, out SongMix mix, out string? error)
    {
        (seed, overrides, mix) = (null, SongOverrides.None, SongMix.None);

        if (Seed is not null)
        {
            if (Core.Base64.ToSeed(Seed) is not { } read)
                return Fail($"{Seed} is not a seed. A seed is up to {Core.Base64.SeedLength} letters and digits.", out error);
            seed = read;
        }

        if (!TryReadStep(Unconventionality, "Unconventionality", out error) || !TryReadStep(Volume, "Volume", out error) || !TryReadStep(Swing, "Swing", out error)
            || !TryReadStep(Length, "Length", out error))
            return false;

        var facets = ImmutableDictionary.CreateBuilder<Facet, double>();
        foreach (var (name, step) in Facets ?? ImmutableDictionary<string, int>.Empty)
        {
            if (!Enum.TryParse<Facet>(name, true, out var facet) || !Enum.IsDefined(facet))
                return Fail($"{name} is not a facet. Facets are {string.Join(", ", Enum.GetNames<Facet>())}.", out error);
            if (!TryReadStep(step, $"Facet {name}", out error))
                return false;
            facets[facet] = StepScale.ToAmount(step);
        }

        var parts = ImmutableDictionary.CreateBuilder<TrackRole, bool>();
        var partMixes = ImmutableDictionary.CreateBuilder<TrackRole, PartMix>();
        foreach (var (name, part) in Parts ?? ImmutableDictionary<string, PartRequest>.Empty)
        {
            if (!Enum.TryParse<TrackRole>(name, true, out var role) || !Enum.IsDefined(role))
                return Fail($"{name} is not a part. Parts are {string.Join(", ", Enum.GetNames<TrackRole>())}.", out error);
            if (part.Instrument is < 0 or > 127)
                return Fail($"The {name}'s instrument {part.Instrument} is not a General MIDI program from 0 to 127.", out error);
            if (!TryReadStep(part.Volume, $"The {name}'s volume", out error)
                || !TryReadStep(part.Pan, $"The {name}'s pan", out error))
                return false;
            if (part.Plays is { } plays)
                parts[role] = plays;
            partMixes[role] = new PartMix(part.Instrument, StepScale.ToAmount(part.Volume ?? StepScale.Last), part.Pan is { } pan ? FromPanStep(pan) : null, part.IsOn);
        }

        if (SongParts.LeaveNone(parts))
            return Fail("Every part is left out, so there is nothing to play.", out error);

        if (DrumPieces is { Count: 0 })
            return Fail("No drum pieces are given; leave the drums out instead.", out error);
        if (DrumPieces?.FirstOrDefault(x => !Core.Composition.DrumPieces.IsPiece(x)) is { } unknown)
            return Fail($"{unknown} is not a drum piece. Pieces are {string.Join(", ", Core.Composition.DrumPieces.All.Select(x => x.Name))}.", out error);

        var groups = ImmutableDictionary.CreateBuilder<string, DrumGroupMix>();
        foreach (var (name, group) in DrumGroups ?? ImmutableDictionary<string, DrumGroupRequest>.Empty)
        {
            var known = SongMix.DrumGroupNames.FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
            if (known is null)
                return Fail($"{name} is not a drum group. Groups are {string.Join(", ", SongMix.DrumGroupNames)}.", out error);
            if (!TryReadStep(group.Volume, $"The {name}'s volume", out error))
                return false;
            groups[known] = new DrumGroupMix(StepScale.ToAmount(group.Volume ?? StepScale.Last), group.IsOn);
        }

        if (Tempo is < 0 or >= TempoScale.Steps)
            return Fail($"Tempo {Tempo} is not from 0 to {TempoScale.Steps - 1}.", out error);
        if (Key is < 0 or > 11)
            return Fail($"Key {Key} is not from 0 to 11.", out error);
        if (Meter is < 0 || Meter >= Core.Composition.Meter.Options.Length)
            return Fail($"Meter {Meter} is not one of the {Core.Composition.Meter.Options.Length} meters.", out error);
        if (Mode is < 0 || Mode >= Scales.All.Length)
            return Fail($"Mode {Mode} is not one of the {Scales.All.Length} scales.", out error);

        Genre? genre = null;
        if (Genre is not null)
        {
            var amounts = new Dictionary<string, IReadOnlyDictionary<string, ByConvention>>();
            foreach (var (choice, options) in Genre)
            {
                var read = new Dictionary<string, ByConvention>();
                foreach (var (option, amount) in options)
                {
                    if (ReadAmounts(amount) is not { } ends)
                        return Fail($"The genre's {choice} {option} is not a whole percent, from 0 to 100, or three of them.", out error);
                    read[option] = ends;
                }

                amounts[choice] = read;
            }

            genre = Core.Composition.Genre.Read(amounts, out error);
            if (genre is null)
                return false;
        }

        overrides = new SongOverrides(
            Base: Unconventionality is { } unconventionality ? StepScale.ToAmount(unconventionality) : null,
            Facets: facets.Count > 0 ? facets.ToImmutable() : null,
            Parts: parts.Count > 0 ? parts.ToImmutable() : null,
            DrumPieces: DrumPieces is null ? null : [..DrumPieces],
            MeterOption: Meter,
            Tempo: Tempo is { } tempo ? TempoScale.Multiplier(tempo) : null,
            Key: Key,
            Scale: Mode is { } mode ? Scales.All[mode] : null,
            Swing: Swing is { } swing ? StepScale.ToAmount(swing) : null,
            Genre: genre,
            Length: Length is { } length ? LengthScale.Seconds(length) : null
        );
        mix = new SongMix(StepScale.ToAmount(Volume ?? StepScale.Last), partMixes.ToImmutable(), groups.ToImmutable());
        return true;
    }

    /// <summary>An option's amounts from whole percents, one alike at every end or three, at the plain end, in the middle and at the wild; none for anything else.</summary>
    private static ByConvention? ReadAmounts(JsonElement amount)
    {
        static double? Percent(JsonElement x) => x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out var value) && value is >= 0 and <= 100 ? value / 100.0 : null;
        if (Percent(amount) is { } alike)
            return new ByConvention(alike, alike, alike);
        if (amount.ValueKind != JsonValueKind.Array || amount.GetArrayLength() != 3)
            return null;
        var ends = amount.EnumerateArray().Select(Percent).ToArray();
        return ends.All(x => x is not null) ? new ByConvention(ends[0]!.Value, ends[1]!.Value, ends[2]!.Value) : null;
    }

    private static bool TryReadStep(int? step, string what, out string? error)
    {
        error = null;
        return step is null or >= 0 and <= StepScale.Last || Fail($"{what} {step} is not a step from 0 to {StepScale.Last}.", out error);
    }

    private static bool Fail(string message, out string? error)
    {
        error = message;
        return false;
    }
}

/// <param name="Plays">Whether the part is in the song, given; none for the song to draw it.</param>
/// <param name="Instrument">The General MIDI program it plays, the drums' kit; none for the song's own.</param>
/// <param name="Volume">How loud it plays, 63 as it is made; none is 63.</param>
/// <param name="Pan">Where it sits, from 0, left, through 32, the middle, to 63, right; none for the song's own.</param>
/// <param name="IsOn">Whether it is written in the file at all, which a part switched off is not.</param>
public sealed record PartRequest(bool? Plays = null, int? Instrument = null, int? Volume = null, int? Pan = null, bool IsOn = true);

/// <param name="Volume">How loud its drums play, 63 as they are made; none is 63.</param>
/// <param name="IsOn">Whether its drums are written in the file at all.</param>
public sealed record DrumGroupRequest(int? Volume = null, bool IsOn = true);
