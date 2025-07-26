using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public sealed class PitchInstrumentTrack : IInstrumentTrack
{
    public PitchInstrumentTrack(
        StateMap stateMap,
        int instrumentCode,
        int minOctaveOffset,
        int maxOctaveOffset
    )
    {
        StateMap = stateMap;
        InstrumentCode = instrumentCode;
        MinOctaveOffset = minOctaveOffset;
        MaxOctaveOffset = maxOctaveOffset;
    }

    public int InstrumentCode { get; }

    public int MinOctaveOffset { get; }

    public int MaxOctaveOffset { get; }
    public StateMap StateMap { get; }
}
