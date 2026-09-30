using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.WebApi.Songs;

/// <summary>
///     What a song drew or was given, as the page shows it: every amount a step from 0 to 127, the names as the request
///     takes them, and every part's channel, counted from 1, as its mix plays it.
/// </summary>
/// <param name="Unconventionality">How far it strays from convention as a whole.</param>
/// <param name="Facets">Every facet's, by its name.</param>
/// <param name="Parts">Every part's, by its role's name.</param>
/// <param name="DrumSetup">The drums it plays.</param>
/// <param name="DrumGroups">The drum groups it plays, by name.</param>
public sealed record SongReport(
    Drawn<int> Unconventionality,
    ImmutableSortedDictionary<string, Drawn<int>> Facets,
    ImmutableSortedDictionary<string, PartReport> Parts,
    Drawn<string> DrumSetup,
    ImmutableArray<string> DrumGroups
)
{
    public static SongReport Of(Song song, RenderedSong rendered, SongOverrides overrides)
    {
        var draws = song.Draws!;
        var channels = rendered.GetPartChannels();
        // the song's own instrument and pan, whatever the mix plays, and the drums' kit, the standard one, in the middle
        var own = song.TrackDefinitions.Values.OfType<PitchInstrumentTrack>().ToDictionary(x => x.Role, x => (x.InstrumentCode, x.Pan));
        own[TrackRole.Drum] = (0, 0);
        var groups = SongMix.DrumGroupsOf(song).Order();

        return new SongReport(
            new Drawn<int>(GenerateSongRequest.ToStep(draws.Unconventionality.Base), overrides.Base is not null),
            draws.Unconventionality.Facets.ToImmutableSortedDictionary(
                x => Name(x.Key),
                x => new Drawn<int>(GenerateSongRequest.ToStep(x.Value), overrides.Facets?.ContainsKey(x.Key) == true)
            ),
            Enum.GetValues<TrackRole>().ToImmutableSortedDictionary(
                Name,
                role => new PartReport(
                    new Drawn<bool>(draws.Parts.Contains(role), overrides.Parts?.ContainsKey(role) == true),
                    channels.TryGetValue(role, out var channel) ? channel + 1 : null,
                    own[role].InstrumentCode,
                    GenerateSongRequest.ToPanStep(own[role].Pan)
                )
            ),
            new Drawn<string>(Name(draws.DrumSetup), overrides.DrumSetup is not null),
            [..groups.Select(Name)]
        );
    }

    /// <summary>A name as the request takes it: camel case, "counterMelody".</summary>
    private static string Name<T>(T value) where T : notnull
    {
        var name = value.ToString()!;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}

/// <param name="Value">What the song has.</param>
/// <param name="IsGiven">Whether it was given, rather than drawn.</param>
public sealed record Drawn<T>(T Value, bool IsGiven);

/// <param name="Plays">Whether the part is in the song.</param>
/// <param name="Channel">The channel it plays on, from 1; none where it is not written.</param>
/// <param name="Instrument">The General MIDI program the song gives it, the drums' kit, whatever the mix plays.</param>
/// <param name="Pan">Where the song sits it, from 0, left, through 64, the middle, to 127, right, whatever the mix does.</param>
public sealed record PartReport(Drawn<bool> Plays, int? Channel, int Instrument, int Pan);
