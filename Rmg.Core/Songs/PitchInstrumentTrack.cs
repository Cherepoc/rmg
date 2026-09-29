using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public sealed class PitchInstrumentTrack : IInstrumentTrack
{
    public PitchInstrumentTrack(
        StateMap stateMap,
        int instrumentCode,
        int minOctaveOffset,
        int maxOctaveOffset,
        TrackRole role,
        double pan
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pan, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pan, 1);

        if (role == TrackRole.Drum)
            throw new ArgumentException("A pitched track does not play a drum.", nameof(role));

        Role = role;
        StateMap = stateMap;
        InstrumentCode = instrumentCode;
        MinOctaveOffset = minOctaveOffset;
        MaxOctaveOffset = maxOctaveOffset;
        Pan = pan;
    }

    /// <summary>Where the track sits from left to right, from -1, left, through 0, the middle, to 1, right.</summary>
    public double Pan { get; }

    public int InstrumentCode { get; }

    public int MinOctaveOffset { get; }

    public int MaxOctaveOffset { get; }
    public StateMap StateMap { get; }

    public TrackRole Role { get; }
}
