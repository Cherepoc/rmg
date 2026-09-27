using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public interface IInstrumentTrack
{
    StateMap StateMap { get; }

    /// <summary>What the track plays in the song.</summary>
    TrackRole Role { get; }
}
