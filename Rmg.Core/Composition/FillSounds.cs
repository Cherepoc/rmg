using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The sounds a run plays: a few of the song's drums, drawn role by role, mostly the snare and the toms, in an order
///     that a run walks from note to note, each note playing a window of it. Every sound of every drum of the song may
///     play, and the stranger a section's rhythm, the likelier the unconventional ones, the random walks and the wide
///     windows, and the less the toms keep to their order of pitch. A drum plays one sound at a time, so a window
///     plays its sounds of different drums together.
/// </summary>
internal sealed class FillSounds
{
    private static readonly ImmutableDictionary<DrumGroup, DrumRole> GroupRoles = new Dictionary<DrumGroup, DrumRole>
    {
        [DrumGroups.Kick] = DrumRole.Kick,
        [DrumGroups.Snare] = DrumRole.Snare,
        [DrumGroups.Toms] = DrumRole.Toms,
        [DrumGroups.Timekeepers] = DrumRole.HiHat,
        [DrumGroups.Accents] = DrumRole.Cymbal,
        [DrumGroups.Percussion] = DrumRole.Percussion
    }.ToImmutableDictionary();

    private readonly ImmutableSortedDictionary<DrumRole, ImmutableArray<RunSound>> _sounds;

    public FillSounds(IEnumerable<PercussionInstrumentDefinition> songDrums)
    {
        var sounds = new SortedDictionary<DrumRole, List<RunSound>>();
        foreach (var drum in songDrums)
        {
            var role = GroupRoles.Single(x => x.Key.Drums.Contains(drum)).Value;
            if (!sounds.TryGetValue(role, out var list))
                sounds[role] = list = [];
            list.AddRange(drum.ArticulationCodes.Select(code =>
                    new RunSound(role, DrumGroups.GetTrackNumber(drum), drum.GetArticulationIndex(code), code, drum.Name)
                )
            );
        }

        _sounds = sounds.ToImmutableSortedDictionary(x => x.Key, x => x.Value.ToImmutableArray());
    }

    /// <summary>Every sound a run may play, by role.</summary>
    public ImmutableSortedDictionary<DrumRole, ImmutableArray<RunSound>> Sounds => _sounds;

    /// <summary>A run's sounds, their order, its walk and window, how full it is, and whether it speeds up.</summary>
    /// <param name="fullness">Where the run's fullness is spread around, before the drummer moves it.</param>
    public FillRun Draw(IGenerationContext context, Drummer drummer, double chanceScale, double fullness)
    {
        var sounds = new List<RunSound>();
        foreach (var (role, candidates) in _sounds)
        {
            var chance = FillLayers.RoleChances[role] * (FillLayers.ConventionalRoles.Contains(role) ? 1 : chanceScale);
            if (!context.TestProbability(Math.Min(1, chance)))
                continue;

            var count = Math.Min(Pick(context, FillLayers.SoundCounts), candidates.Length);
            sounds.AddRange(Shuffle(context, candidates).Take(count));
        }

        // a run that draws no drum plays the snare, or the song's first drum
        if (sounds.Count == 0 && _sounds.Count > 0)
            sounds.Add((_sounds.TryGetValue(DrumRole.Snare, out var snares) ? snares : _sounds.Values.First())[0]);

        var order = Shuffle(context, sounds);
        // the toms keep their order of pitch, as their note numbers have it, down or up
        if (context.TestProbability(Math.Min(1, FillLayers.PitchOrderChance / chanceScale)))
        {
            var isDown = context.TestProbability(0.5);
            var toms = new Queue<RunSound>(order.Where(x => x.Role == DrumRole.Toms).OrderBy(x => isDown ? -x.Code : x.Code));
            order = [..order.Select(x => x.Role == DrumRole.Toms ? toms.Dequeue() : x)];
        }

        ImmutableArray<Weighted<int>> widths = [..FillLayers.Widths.Select(x => x.Value > 1 ? x with { Weight = x.Weight * chanceScale } : x)];
        var width = Math.Min(Pick(context, widths), Math.Max(1, order.Length));
        var path = Pick(context, drummer.WeighPaths(FillLayers.Paths, chanceScale));
        var runFullness = Math.Clamp(
            fullness + FillLayers.RunFullnessSpread * Generators.SplineValue()(context) + drummer.FullnessOffset,
            FillLayers.MinRunFullness,
            FillLayers.MaxRunFullness
        );
        var speedsUp = context.TestProbability(FillLayers.SpeedUpChance);
        var rankLimitLift = context.TestProbability(Math.Min(1, FillLayers.RankLimitLiftChance * chanceScale)) ? 1 : 0;
        return new FillRun(order, path, width, runFullness, speedsUp, rankLimitLift);
    }

    /// <summary>Where in its order each of a run's notes starts its window, as its walk goes.</summary>
    public static int[] Walk(IGenerationContext context, FillPath path, int soundCount, int noteCount)
    {
        var places = new int[noteCount];
        if (soundCount <= 1)
            return places;

        var place = context.GenerateInt(0, soundCount);
        for (var k = 0; k < noteCount; k++)
        {
            switch (path)
            {
                case FillPath.OneWay:
                    places[k] = k * soundCount / noteCount;
                    break;
                case FillPath.Turn:
                {
                    // there and back, the last sound once
                    var step = k * (2 * soundCount - 1) / noteCount;
                    places[k] = step < soundCount ? step : 2 * soundCount - 2 - step;
                    break;
                }
                case FillPath.Loop:
                    places[k] = k % soundCount;
                    break;
                default:
                    if (k > 0)
                        place = !context.TestProbability(FillLayers.NeighbourStepChance) ? context.GenerateInt(0, soundCount)
                            // from an end, the one neighbour
                            : place == 0 ? 1
                            : place == soundCount - 1 ? place - 1
                            : place + (context.TestProbability(0.5) ? 1 : -1);
                    places[k] = place;
                    break;
            }
        }

        return places;
    }

    /// <summary>
    ///     The sounds of a run's note: the window of its order from the note's place, each if its role plays the note's
    ///     rank; a drum plays one sound at a time, the first of the window's.
    /// </summary>
    public static IEnumerable<RunSound> GetNoteSounds(FillRun run, int place, int rank)
    {
        var tracks = new HashSet<int>();
        for (var j = 0; j < run.Width; j++)
        {
            var sound = run.Sounds[(place + j) % run.Sounds.Length];
            if ((!FillLayers.RankLimits.TryGetValue(sound.Role, out var limit) || rank <= limit + run.RankLimitLift) && tracks.Add(sound.Track))
                yield return sound;
        }
    }

    private static T Pick<T>(IGenerationContext context, ImmutableArray<Weighted<T>> weights)
    {
        return weights[Generators.WeightedIndex(weights)(context)].Value;
    }

    private static ImmutableArray<T> Shuffle<T>(IGenerationContext context, IEnumerable<T> items)
    {
        var list = items.ToArray();
        for (var i = list.Length - 1; i > 0; i--)
        {
            var j = context.GenerateInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return [..list];
    }
}

/// <summary>A sound a run plays: its drum's role, track, the sound's number on the drum, counted from 1, and its note number.</summary>
internal sealed record RunSound(DrumRole Role, int Track, int Articulation, int Code, string Drum)
{
    public override string ToString() => $"{Drum} {Code}";
}

/// <summary>A run as drawn: its sounds in order, how it walks them, how many each note plays, how full it is, and more.</summary>
/// <param name="Fullness">How likely it keeps a note, less for every rank the note is weaker.</param>
/// <param name="RankLimitLift">How many ranks finer than their limit its sounds may play.</param>
internal sealed record FillRun(
    ImmutableArray<RunSound> Sounds,
    FillPath Path,
    int Width,
    double Fullness,
    bool SpeedsUp,
    int RankLimitLift = 0
)
{
    public override string ToString() =>
        $"on {string.Join(" ", Sounds)}, {Path}{(Width > 1 ? $", {Width} at once" : "")}, fullness {Fullness:F2}{(SpeedsUp ? ", speeding up" : "")}";
}
