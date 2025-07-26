using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public sealed class Song
{
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

    public double Duration { get; }

    public ImmutableSortedDictionary<int, IInstrumentTrack> TrackDefinitions { get; }

    public TrackEventStateTimelineMap<StateMap> TrackEventStateTimelineMap { get; }
}
