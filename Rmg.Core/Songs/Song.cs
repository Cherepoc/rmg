using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public sealed class Song
{
    public Song(
        double duration,
        Meter meter,
        ImmutableSortedDictionary<int, IInstrumentTrack> trackDefinitions,
        TrackEventStateTimelineMap<StateMap> trackEventStateTimelineMap,
        SongMap? map = null,
        ImmutableSortedDictionary<int, EventTimeline<RealizedNote>>? notes = null,
        SongDraws? draws = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);

        Duration = duration;
        Meter = meter;
        TrackDefinitions = trackDefinitions;
        TrackEventStateTimelineMap = trackEventStateTimelineMap;
        Map = map;
        Notes = notes;
        Draws = draws;
    }

    /// <summary>What a generated song drew, or was given in place of drawing; none for a song made otherwise.</summary>
    public SongDraws? Draws { get; }

    /// <summary>
    ///     Every playing track's notes, by its number, as the song's generation decided them from its state; none for a
    ///     song made otherwise, which <c>Render</c> decides them for.
    /// </summary>
    public ImmutableSortedDictionary<int, EventTimeline<RealizedNote>>? Notes { get; }

    /// <summary>Where the song's intro, sections and ending are, for a generated song.</summary>
    public SongMap? Map { get; }

    public double Duration { get; }

    /// <summary>The meter the song's bars are in.</summary>
    public Meter Meter { get; }

    public ImmutableSortedDictionary<int, IInstrumentTrack> TrackDefinitions { get; }

    public TrackEventStateTimelineMap<StateMap> TrackEventStateTimelineMap { get; }
}
