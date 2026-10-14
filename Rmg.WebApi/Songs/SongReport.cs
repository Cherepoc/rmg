using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.WebApi.Songs;

/// <summary>
///     What a song drew or was given, as the page shows it: every amount a step from 0 to 61, the middle 31 (<see cref="StepScale" />), the names as the request
///     takes them, and every part's channel, counted from 1, as its mix plays it.
/// </summary>
/// <param name="Unconventionality">How far it strays from convention as a whole.</param>
/// <param name="Facets">Every facet's, by its name.</param>
/// <param name="Parts">Every part's, by its role's name.</param>
/// <param name="DrumPieces">The drum pieces it plays, by their names (<c>/api/songs/options</c>).</param>
/// <param name="DrumGroups">The drum groups it plays, by name.</param>
/// <param name="Tempo">Its tempo, by its step (<see cref="TempoScale" />), the nearest to a tempo it drew.</param>
/// <param name="Key">Its key, as semitones above C.</param>
/// <param name="Meter">Its meter, by its place among the meters (<c>/api/songs/options</c>).</param>
/// <param name="Mode">The scale it starts in, by its place among the scales (<c>/api/songs/options</c>).</param>
/// <param name="Swing">How much it swings, from 0, straight, to 61, a triplet's swing: 0 where its meter pairs no notes to swing.</param>
/// <param name="Length">How long it plays, by its step (<see cref="LengthScale" />), the nearest to its length, given or not.</param>
/// <param name="Darkness">How dark its mood is, from 0, the brightest, to 61, the darkest.</param>
/// <param name="Intensity">How intense its mood is, from 0, the calmest, to 61, the fiercest.</param>
/// <param name="Seconds">How long it plays, in seconds.</param>
/// <param name="Sections">Every section it plays, in its order: where it starts, its role and the parts that solo it.</param>
public sealed record SongReport(
    Drawn<int> Unconventionality,
    ImmutableSortedDictionary<string, Drawn<int>> Facets,
    ImmutableSortedDictionary<string, PartReport> Parts,
    Drawn<ImmutableArray<string>> DrumPieces,
    ImmutableArray<string> DrumGroups,
    Drawn<int> Tempo,
    Drawn<int> Key,
    Drawn<int> Meter,
    Drawn<int> Mode,
    Drawn<int> Swing,
    Drawn<int> Length,
    Drawn<int> Darkness,
    Drawn<int> Intensity,
    double Seconds,
    ImmutableArray<SectionReport> Sections
)
{
    public static SongReport Of(Song song, RenderedSong rendered, SongOverrides overrides)
    {
        var draws = song.Draws!;
        // the key and the tempo the song starts in, before a key change or an ending's ritardando
        var common = song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0);
        var channels = rendered.GetPartChannels();
        var doubles = rendered.GetDoubleChannels();
        var risers = rendered.GetRiserChannels();
        var levels = rendered.GetPartLevels();
        // the song's own instrument and pan, whatever the mix plays, and the drums' kit, in the middle
        var own = song.TrackDefinitions.Values.OfType<PitchInstrumentTrack>().ToDictionary(x => x.Role, x => (x.InstrumentCode, x.Pan));
        own[TrackRole.Drum] = (draws.Kit, 0);
        var groups = SongMix.DrumGroupsOf(song).Order();

        return new SongReport(
            new Drawn<int>(StepScale.ToStep(draws.Unconventionality.Base), overrides.Base is not null),
            draws.Unconventionality.Facets.ToImmutableSortedDictionary(
                x => Name(x.Key),
                x => new Drawn<int>(StepScale.ToStep(x.Value), overrides.Facets?.ContainsKey(x.Key) == true)
            ),
            Enum.GetValues<TrackRole>().ToImmutableSortedDictionary(
                Name,
                role => new PartReport(
                    new Drawn<bool>(draws.Parts.Contains(role), overrides.Parts?.ContainsKey(role) == true),
                    channels.TryGetValue(role, out var channel) ? channel + 1 : null,
                    doubles.TryGetValue(role, out var second) ? second + 1 : null,
                    risers.TryGetValue(role, out var riser) ? riser + 1 : null,
                    levels.GetValueOrDefault(role, 1),
                    own[role].InstrumentCode,
                    GenerateSongRequest.ToPanStep(own[role].Pan)
                )
            ),
            new Drawn<ImmutableArray<string>>(draws.DrumPieces, overrides.DrumPieces is not null),
            [..groups.Select(Name)],
            new Drawn<int>(TempoScale.StepOf(common.GetStateValue(StateKinds.Tempo)), overrides.Tempo is not null),
            new Drawn<int>(common.GetStateValue(StateKinds.KeyOffset).Mod(12), overrides.Key is not null),
            new Drawn<int>(Core.Composition.Meter.OptionOf(song.Meter), overrides.MeterOption is not null || overrides.Meter is not null),
            new Drawn<int>(Scales.All.IndexOf(draws.Scale), overrides.Scale is not null),
            new Drawn<int>(StepScale.ToStep(draws.SwingAmount), overrides.Swing is not null),
            new Drawn<int>(LengthScale.StepOf(rendered.Seconds), overrides.Length is not null),
            new Drawn<int>(StepScale.ToStep(Mood.AmountOf(draws.Mood.Darkness)), overrides.Darkness is not null),
            new Drawn<int>(StepScale.ToStep(Mood.AmountOf(draws.Mood.Intensity)), overrides.Intensity is not null),
            rendered.Seconds,
            [
                ..song.Map!.Sections.Zip(draws.Appearances, (span, appearance) =>
                    new SectionReport(rendered.SecondsAt(span.Start), Name(appearance.Role), [..appearance.Soloists.Select(Name)]))
            ]
        );
    }

    /// <summary>A name as the request takes it: camel case, "counterMelody".</summary>
    private static string Name<T>(T value) where T : notnull
    {
        var name = value.ToString()!;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}

/// <param name="Start">Where it starts, in seconds.</param>
/// <param name="Role">What it does in the song's form, by its name: "verse", "chorus".</param>
/// <param name="Soloists">The parts that solo it, by their roles' names; none for most.</param>
public sealed record SectionReport(double Start, string Role, ImmutableArray<string> Soloists);

/// <param name="Value">What the song has.</param>
/// <param name="IsGiven">Whether it was given, rather than drawn.</param>
public sealed record Drawn<T>(T Value, bool IsGiven);

/// <param name="Plays">Whether the part is in the song.</param>
/// <param name="Channel">The channel it plays on, from 1; none where it is not written.</param>
/// <param name="Double">The channel its second take plays on, from 1, a doubled driven guitar's; none for a part played once.</param>
/// <param name="Riser">The channel its riser into a drop plays on, from 1, mixed as the part is; none for a part that carries none.</param>
/// <param name="Level">
///     The level its channels play at under the mix's faders, as the file writes them: a doubled part's two takes each
///     about 0.84, so that the pair sounds as one part; 1 for the others.
/// </param>
/// <param name="Instrument">The General MIDI program the song gives it, the drums' kit, whatever the mix plays.</param>
/// <param name="Pan">Where the song sits it, from 0, left, through 31, the middle, to 61, right, whatever the mix does.</param>
public sealed record PartReport(Drawn<bool> Plays, int? Channel, int? Double, int? Riser, double Level, int Instrument, int Pan);
