using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Which of the band's parts a song has, for its sections to play or rest as they draw (<see cref="Arrangement" />):
///     the melody, the chords and the bass always, and the pad, the counter-melody and the drums by a chance of their
///     own, whatever the song's unconventionality, since a song without one is neither plainer nor stranger for it. A
///     part given in or out is so, each drawn all the same from a sequence of its own, so that giving one leaves the
///     rest of the song as its seed made it.
/// </summary>
internal static class SongParts
{
    /// <summary>Every part a song can have.</summary>
    public static ImmutableArray<TrackRole> All { get; } =
        [TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.CounterMelody, TrackRole.Drum];

    /// <summary>The chance a song has the part, when not given; 1 for a part every song has.</summary>
    public static double Chance(TrackRole part) => part switch
    {
        TrackRole.Pad => 0.8,
        TrackRole.CounterMelody => 0.6,
        TrackRole.Drum => 0.95,
        TrackRole.Melody or TrackRole.Chords or TrackRole.Bass => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Not a part of the band.")
    };

    /// <summary>The parts the song leaves out, drawn or given, and never all of them.</summary>
    /// <param name="streams">A part's own sequence.</param>
    /// <param name="given">The parts given in (true) or out (false), in place of their draws.</param>
    public static ImmutableHashSet<TrackRole> DrawAbsent(Func<TrackRole, IGenerationContext> streams, IReadOnlyDictionary<TrackRole, bool>? given)
    {
        var absent = All
            .Where(part =>
                {
                    var isIn = streams(part).TestProbability(Chance(part));
                    return !(given is not null && given.TryGetValue(part, out var isGiven) ? isGiven : isIn);
                }
            )
            .ToImmutableHashSet();
        if (absent.Count == All.Length)
            throw new ArgumentException("A song needs at least one part in it.", nameof(given));

        return absent;
    }
}
