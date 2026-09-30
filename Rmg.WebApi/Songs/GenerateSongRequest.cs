using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.WebApi.Songs;

/// <summary>
///     A song to generate: its seed, what is given in place of what it draws, and how it is to be heard. Every amount is
///     a step from 0 to 63, a character of the page's settings each; anything left out is the song's own.
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
/// <param name="DrumSetup">The drums the song plays: "kit", "kitAndPercussion" or "percussion".</param>
/// <param name="DrumGroups">A drum group's mix, by its name, such as "kick".</param>
/// <param name="Tempo">The tempo, by its place among the tempos a song may play at (<c>/api/songs/options</c>).</param>
/// <param name="Key">The key, as semitones above C, from 0 to 11.</param>
/// <param name="Meter">The meter, by its place among the meters a song may play in (<c>/api/songs/options</c>).</param>
public sealed record GenerateSongRequest(
    string? Seed = null,
    int? Unconventionality = null,
    IReadOnlyDictionary<string, int>? Facets = null,
    int? Volume = null,
    IReadOnlyDictionary<string, PartRequest>? Parts = null,
    string? DrumSetup = null,
    IReadOnlyDictionary<string, DrumGroupRequest>? DrumGroups = null,
    int? Tempo = null,
    int? Key = null,
    int? Meter = null
)
{
    /// <summary>The last step of an amount, from 0: as many as a character of the settings holds.</summary>
    public const int LastStep = 63;

    /// <summary>The middle of a pan's steps, where a part sits in the middle: 32 to its left and 31 to its right, as MIDI's 64 is.</summary>
    public const int MiddlePan = 32;

    /// <summary>An amount from 0 to 1 as its nearest step.</summary>
    public static int ToStep(double amount) => (int)Math.Round(amount * LastStep);

    /// <summary>A pan from -1, left, to 1, right, as its nearest step, 32 in the middle.</summary>
    public static int ToPanStep(double pan) => (int)Math.Round(MiddlePan + pan * (pan < 0 ? MiddlePan : LastStep - MiddlePan));

    private static double FromPanStep(int step) => (step - MiddlePan) / (double)(step < MiddlePan ? MiddlePan : LastStep - MiddlePan);

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

        if (!TryReadStep(Unconventionality, "Unconventionality", out error) || !TryReadStep(Volume, "Volume", out error))
            return false;

        var facets = ImmutableDictionary.CreateBuilder<Facet, double>();
        foreach (var (name, step) in Facets ?? ImmutableDictionary<string, int>.Empty)
        {
            if (!Enum.TryParse<Facet>(name, true, out var facet) || !Enum.IsDefined(facet))
                return Fail($"{name} is not a facet. Facets are {string.Join(", ", Enum.GetNames<Facet>())}.", out error);
            if (!TryReadStep(step, $"Facet {name}", out error))
                return false;
            facets[facet] = step / (double)LastStep;
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
            partMixes[role] = new PartMix(part.Instrument, (part.Volume ?? LastStep) / (double)LastStep, part.Pan is { } pan ? FromPanStep(pan) : null, part.IsOn);
        }

        if (Enum.GetValues<TrackRole>().All(x => parts.TryGetValue(x, out var plays) && !plays))
            return Fail("Every part is left out, so there is nothing to play.", out error);

        DrumSetup? drumSetup = null;
        if (DrumSetup is not null)
        {
            if (!Enum.TryParse<DrumSetup>(DrumSetup, true, out var setup) || !Enum.IsDefined(setup))
                return Fail($"{DrumSetup} is not a drum setup. Setups are {string.Join(", ", Enum.GetNames<DrumSetup>())}.", out error);
            drumSetup = setup;
        }

        var groups = ImmutableDictionary.CreateBuilder<string, DrumGroupMix>();
        foreach (var (name, group) in DrumGroups ?? ImmutableDictionary<string, DrumGroupRequest>.Empty)
        {
            var known = SongMix.DrumGroupNames.FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
            if (known is null)
                return Fail($"{name} is not a drum group. Groups are {string.Join(", ", SongMix.DrumGroupNames)}.", out error);
            if (!TryReadStep(group.Volume, $"The {name}'s volume", out error))
                return false;
            groups[known] = new DrumGroupMix((group.Volume ?? LastStep) / (double)LastStep, group.IsOn);
        }

        if (Tempo is < 0 || Tempo >= SongGenerator.TempoOptions.Length)
            return Fail($"Tempo {Tempo} is not one of the {SongGenerator.TempoOptions.Length} tempos.", out error);
        if (Key is < 0 or > 11)
            return Fail($"Key {Key} is not from 0 to 11.", out error);
        if (Meter is < 0 || Meter >= Core.Composition.Meter.Options.Length)
            return Fail($"Meter {Meter} is not one of the {Core.Composition.Meter.Options.Length} meters.", out error);

        overrides = new SongOverrides(
            Base: Unconventionality / (double)LastStep,
            Facets: facets.Count > 0 ? facets.ToImmutable() : null,
            Parts: parts.Count > 0 ? parts.ToImmutable() : null,
            DrumSetup: drumSetup,
            MeterOption: Meter,
            Tempo: Tempo,
            Key: Key
        );
        mix = new SongMix((Volume ?? LastStep) / (double)LastStep, partMixes.ToImmutable(), groups.ToImmutable());
        return true;
    }

    private static bool TryReadStep(int? step, string what, out string? error)
    {
        error = null;
        return step is null or >= 0 and <= LastStep || Fail($"{what} {step} is not a step from 0 to {LastStep}.", out error);
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
