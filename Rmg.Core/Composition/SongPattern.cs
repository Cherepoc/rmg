using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

public sealed class SongPattern
{
    public double Duration { get; }
    
    public ImmutableSortedDictionary<int, IInstrumentTrack> TrackDefinitions { get; }
    
    public EventTimeline<WithStateMap<TrackEventStatePattern<StateMap>>> TrackPatternTimelineMap { get; }
    
    public StateMap StateMap { get; }
    
    public Song MergedSong { get; }
    
    public SongPattern(
        double duration,
        ImmutableSortedDictionary<int, IInstrumentTrack> trackDefinitions,
        EventTimeline<WithStateMap<TrackEventStatePattern<StateMap>>> trackPatternTimelineMap,
        StateMap stateMap,
        Song mergedSong
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);
        
        Duration = duration;
        TrackDefinitions = trackDefinitions;
        TrackPatternTimelineMap = trackPatternTimelineMap;
        StateMap = stateMap;
        MergedSong = mergedSong;
    }
    
    public static SongPattern Create(
        double duration,
        IEnumerable<KeyValuePair<int, IInstrumentTrack>> trackDefinitions,
        EventTimeline<WithStateMap<TrackEventStatePattern<StateMap>>> trackPatternTimelineMap,
        StateMap stateMap
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);
        
        var trackDefinitionDictionary = trackDefinitions.ToImmutableSortedDictionary();

        var unwrappedTrackPatternTimelineMap = trackPatternTimelineMap
            .MapValues(x => x.Value.MergeStateMap(x.StateMap))
            .Unwrap()
            .MergeStateMap(stateMap);

        return new SongPattern(
            duration,
            trackDefinitionDictionary,
            trackPatternTimelineMap,
            stateMap,
            new Song(duration, trackDefinitionDictionary, unwrappedTrackPatternTimelineMap)
        );
    }
}