using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public sealed class PitchInstrumentTrack : IInstrumentTrack
{
    public StateMap StateMap { get; }

    public int InstrumentCode { get; }

    public int MinOctaveOffset { get; }

    public int MaxOctaveOffset { get; }

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
}