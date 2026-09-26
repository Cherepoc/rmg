using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public sealed class Song
{
    public Song(
        double duration,
        ImmutableSortedDictionary<int, IInstrumentTrack> trackDefinitions,
        TrackEventStateTimelineMap<StateMap> trackEventStateTimelineMap,
        SongMap? map = null,
        ImmutableSortedDictionary<int, EventTimeline<RealizedNote>>? notes = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);

        Duration = duration;
        TrackDefinitions = trackDefinitions;
        TrackEventStateTimelineMap = trackEventStateTimelineMap;
        Map = map;
        Notes = notes;
    }

    /// <summary>
    ///     Every playing track's notes, by its number, as the song's generation decided them from its state; none for a
    ///     song made otherwise, which <c>Render</c> decides them for.
    /// </summary>
    public ImmutableSortedDictionary<int, EventTimeline<RealizedNote>>? Notes { get; }

    /// <summary>Where the song's intro, sections and ending are, for a generated song.</summary>
    public SongMap? Map { get; }

    public double Duration { get; }

    public ImmutableSortedDictionary<int, IInstrumentTrack> TrackDefinitions { get; }

    public TrackEventStateTimelineMap<StateMap> TrackEventStateTimelineMap { get; }
}
