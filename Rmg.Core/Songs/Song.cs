using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public sealed class Song
{
    public double Duration { get; }

    public ImmutableSortedDictionary<int, IInstrumentTrack> TrackDefinitions { get; }

    public TrackEventStateTimelineMap<StateMap> TrackEventStateTimelineMap { get; }

    public Song(
        double duration,
        ImmutableSortedDictionary<int, IInstrumentTrack> trackDefinitions,
        TrackEventStateTimelineMap<StateMap> trackEventStateTimelineMap
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);

        Duration = duration;
        TrackDefinitions = trackDefinitions;
        TrackEventStateTimelineMap = trackEventStateTimelineMap;
    }
}