using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Where a song's pitched tracks sit from left to right, as a mix places them: the bass in the middle, where the low
///     end is felt rather than placed, the melody near it, and the chords out to a side, each track by how far its role
///     spreads, a part of that drawn, on the other side from the track before, so that they stand apart. The drums share
///     the percussion channel, which the soundfont spreads as a kit stands.
/// </summary>
internal static class Panning
{
    /// <summary>How far from the middle a track of a role sits at most, from 0, the middle, to 1, all the way to a side.</summary>
    public static double GetSpread(TrackRole role)
    {
        return role switch
        {
            TrackRole.Bass => 0,
            TrackRole.Melody => 0.15,
            TrackRole.Chords => 0.6,
            TrackRole.Pad => 0.5,
            TrackRole.CounterMelody => 0.4,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "A drum is not panned on its own.")
        };
    }

    /// <summary>
    ///     Every pitched track's pan, by its number, from -1, left, to 1, right: a half to all of its role's spread, the
    ///     tracks that spread taking sides in turn, the widest first, from a side drawn, so that the widest stand apart.
    /// </summary>
    public static ImmutableDictionary<int, double> Draw(IGenerationContext context, IReadOnlyDictionary<int, TrackRole> roles)
    {
        var side = context.TestProbability(0.5) ? 1 : -1;
        var pans = ImmutableDictionary.CreateBuilder<int, double>();
        foreach (var (track, role) in roles.OrderByDescending(x => GetSpread(x.Value)).ThenBy(x => x.Key))
        {
            var spread = GetSpread(role);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (spread == 0)
            {
                pans[track] = 0;
                continue;
            }

            pans[track] = side * spread * (0.5 + 0.5 * context.GenerateDouble());
            side = -side;
        }

        return pans.ToImmutable();
    }
}
