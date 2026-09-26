using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public sealed class Song
{
    public Song(
        double duration,
        ImmutableSortedDictionary<int, IInstrumentTrack> trackDefinitions,
        TrackEventStateTimelineMap<StateMap> trackEventStateTimelineMap,
        SongMap? map = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);

        Duration = duration;
        TrackDefinitions = trackDefinitions;
        TrackEventStateTimelineMap = trackEventStateTimelineMap;
        Map = map;
    }

    /// <summary>Where the song's intro, sections and ending are, for a generated song.</summary>
    public SongMap? Map { get; }

    public double Duration { get; }

    public ImmutableSortedDictionary<int, IInstrumentTrack> TrackDefinitions { get; }

    public TrackEventStateTimelineMap<StateMap> TrackEventStateTimelineMap { get; }
}
