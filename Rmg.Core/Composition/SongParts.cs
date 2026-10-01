using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Which of the band's parts a song has, for its sections to play or rest as they draw (<see cref="Arrangement" />):
///     the melody, the chords and the bass always, and the pad, the counter-melody and the drums by a chance of their
///     own, whatever the song's unconventionality, since a song without one is neither plainer nor stranger for it; but
///     the riff's twin, which harmonises it, by the song's melody facet, as a harmony is a thing a plain song does not
///     play, and a wild one now and then, no oftener than a middling one. A part given in or out is so, each drawn all the same from a sequence of its own, so that giving one leaves the
///     rest of the song as its seed made it.
/// </summary>
public static class SongParts
{
    /// <summary>Every part a song can have.</summary>
    public static ImmutableArray<TrackRole> All { get; } =
        [TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.CounterMelody, TrackRole.Drum, TrackRole.Riff, TrackRole.Rhythm, TrackRole.RiffTwin, TrackRole.Solo];

    /// <summary>The chance a song with a riff has a twin to harmonise it, by the song's melody facet.</summary>
    public static ByConvention TwinChance { get; } = new(0, 0.15, 0.15);

    /// <summary>The chance a song has the part, when not given, by its melody facet; 1 for a part every song has.</summary>
    public static double Chance(TrackRole part, double melody) => part switch
    {
        TrackRole.Pad => 0.8,
        TrackRole.CounterMelody => 0.6,
        TrackRole.Drum => 0.95,
        TrackRole.Riff => 0.5,
        TrackRole.Rhythm => 0.4,
        // of the songs with a riff
        TrackRole.RiffTwin => TwinChance.At(melody),
        // the solo part plays only where a section's appearance is its solo
        TrackRole.Melody or TrackRole.Chords or TrackRole.Bass or TrackRole.Solo => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Not a part of the band.")
    };

    /// <summary>The parts the song leaves out, drawn or given, and never all of them.</summary>
    /// <param name="streams">A part's own sequence.</param>
    /// <param name="given">The parts given in (true) or out (false), in place of their draws.</param>
    /// <param name="melody">The song's melody facet, which its twin leans by.</param>
    public static ImmutableHashSet<TrackRole> DrawAbsent(Func<TrackRole, IGenerationContext> streams, IReadOnlyDictionary<TrackRole, bool>? given, double melody)
    {
        var absent = WithTwins(All
            .Where(part =>
                {
                    var isIn = streams(part).TestProbability(Chance(part, melody));
                    return !(given is not null && given.TryGetValue(part, out var isGiven) ? isGiven : isIn);
                }
            )
            .ToImmutableHashSet());
        // where every part would be out, those drawn out come in, as a song needs a part to play; only every part given
        // out leaves none
        if (absent.Count == All.Length)
            absent = GivenOut(given);
        if (absent.Count == All.Length)
            throw new ArgumentException("A song needs at least one part in it.", nameof(given));

        return absent;
    }

    /// <summary>Whether the parts given out leave none to play, a twin going out with its riff.</summary>
    public static bool LeaveNone(IReadOnlyDictionary<TrackRole, bool>? given) => GivenOut(given).Count == All.Length;

    private static ImmutableHashSet<TrackRole> GivenOut(IReadOnlyDictionary<TrackRole, bool>? given) =>
        WithTwins([..All.Where(part => given is not null && given.TryGetValue(part, out var isIn) && !isIn)]);

    // a twin plays only with its riff
    private static ImmutableHashSet<TrackRole> WithTwins(ImmutableHashSet<TrackRole> absent) =>
        absent.Contains(TrackRole.Riff) ? absent.Add(TrackRole.RiffTwin) : absent;
}
