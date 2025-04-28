using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public sealed class PercussionInstrumentTrack : IInstrumentTrack
{
    public StateMap StateMap { get; }

    public ImmutableArray<int> ArticulationCodes { get; }

    public PercussionInstrumentTrack(
        StateMap stateMap,
        ImmutableArray<int> articulationCodes
    )
    {
        StateMap = stateMap;
        ArticulationCodes = articulationCodes;
    }
}