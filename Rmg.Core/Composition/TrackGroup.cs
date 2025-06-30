using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Composition;

public sealed class TrackGroup
{
    public ImmutableSortedSet<int> TrackNumbers { get; }
    
    public StateMap StateMap { get; }
    
    public TrackGroup(ImmutableSortedSet<int> trackNumbers, StateMap stateMap)
    {
        TrackNumbers = trackNumbers;
        StateMap = stateMap;
    }
}