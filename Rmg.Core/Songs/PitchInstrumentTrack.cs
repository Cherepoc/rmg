using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public sealed class PitchInstrumentTrack : IInstrumentTrack
{
    public PitchInstrumentTrack(
        StateMap stateMap,
        int instrumentCode,
        int minOctaveOffset,
        int maxOctaveOffset,
        TrackRole role
    )
    {
        if (role == TrackRole.Drum)
            throw new ArgumentException("A pitched track does not play a drum.", nameof(role));

        Role = role;
        StateMap = stateMap;
        InstrumentCode = instrumentCode;
        MinOctaveOffset = minOctaveOffset;
        MaxOctaveOffset = maxOctaveOffset;
    }

    public int InstrumentCode { get; }

    public int MinOctaveOffset { get; }

    public int MaxOctaveOffset { get; }
    public StateMap StateMap { get; }

    public TrackRole Role { get; }
}
