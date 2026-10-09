using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public sealed class PercussionInstrumentTrack : IInstrumentTrack
{
    public PercussionInstrumentTrack(
        StateMap stateMap,
        ImmutableArray<DrumSound> sounds
    )
    {
        StateMap = stateMap;
        Sounds = sounds;
    }

    public ImmutableArray<DrumSound> Sounds { get; }

    public StateMap StateMap { get; }

    public TrackRole Role => TrackRole.Drum;
}
