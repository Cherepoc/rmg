using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Composition;

internal static class CompositionStateKinds
{
    private const string Prefix = "Composition";

    public static RhythmStateKinds Rhythm { get; } = new(Prefix);

    public static FillStateKinds Fill { get; } = new(Prefix);

    public static StateKind<int> ValueSeed { get; } = StateKinds.CreateAdditive<int>(Prefix + "ValueSeedValue");

    public static IncrementalStateKinds IncrementalArticulationOffset { get; } =
        new(Prefix + StateKinds.ArticulationOffset.Name);

    // how much a line, such as the melody, moves by step rather than by leap, from 0 to 1
    public static StateKind<double> LineStepwiseness { get; } = StateKinds.CreateAdditive<double>(Prefix + "LineStepwiseness");

    // how strong a note's beat is, 0 the strongest, from its rhythm pattern; kept by the song, as the render state is,
    // for placing its lines once it is put together, as the line state below is
    public static StateKind<int> BeatRank { get; } = StateKinds.CreateAdditive<int>(Prefix + "BeatRank", StateScope.Render);

    // how far a line's note means to go: 1 a step, 2 a leap, 0 the same note
    public static StateKind<int> LineStep { get; } = StateKinds.CreateAdditive<int>(Prefix + "LineStep", StateScope.Render);

    // a line's note's draw, from 0 to 1, of whether it goes on the way the line goes or turns back, which the aim of
    // its phrase leans (LineProfile.GetContinueChance)
    public static StateKind<double> LineTurn { get; } = StateKinds.CreateAdditive<double>(Prefix + "LineTurn", StateScope.Render);

    // which note of a figure a line's note is: the key of its beat in its bar pattern's rhythm, the same for a beat of a
    // bar that comes back and for a beat of a cycle that repeats the one before, so that the note plays again, and is
    // mutated alike, wherever the figure does; 0 for none
    public static StateKind<int> NoteKey { get; } = StateKinds.CreateAdditive<int>(Prefix + "NoteKey", StateScope.Render);

    // how far above or below the middle of its range a line aims in a bar, in semitones, for the phrase's shape
    public static StateKind<double> LineRegister { get; } = StateKinds.CreateAdditive<double>(Prefix + "LineRegister", StateScope.Render);

    // how a line's note's bar leads out into the next chord (a ChordApproach), and what its first note lands on (a
    // ChordArrival), the line's own, where bar state would be every track's
    public static StateKind<int> LineApproach { get; } = StateKinds.CreateAdditive<int>(Prefix + "LineApproach", StateScope.Render);
    public static StateKind<int> LineLanding { get; } = StateKinds.CreateAdditive<int>(Prefix + "LineLanding", StateScope.Render);

    // how a line's note starts its phrase (a PhraseStart): afresh, at where the phrase aims, or going on from the note
    // before, as the line's freedom to change register draws it (LineProfile.RegisterFreedom); none for a note within one
    public static StateKind<int> LinePhraseStart { get; } = StateKinds.CreateAdditive<int>(Prefix + "LinePhraseStart", StateScope.Render);

    // 1 for a note added in a bar's last beat for the line to lead into the next chord on, as the bass's pickup, which
    // stays only where the line does lead into a new chord (LinePattern.Place)
    public static StateKind<int> LinePickup { get; } = StateKinds.CreateAdditive<int>(Prefix + "LinePickup", StateScope.Render);

    // where the melody's phrase ends in its last bar: 0 for no end, or the beat, from 1 to 3, before which its last note
    // starts; it holds that note, and rests until the next phrase
    public static StateKind<int> MelodyPhraseEnd { get; } = StateKinds.CreateAdditive<int>(Prefix + "MelodyPhraseEnd");

    // the chord of a bar with a role in the phrase, such as the home chord, in place of the pool's pick; empty or one
    public static StateKind<ImmutableArray<Chord>> RoleChord { get; } =
        StateKinds.CreateCollection<Chord>(Prefix + "RoleChord", isShared: true);

    // how far a track's notes move from its level, by their beats' accents and at random: less for a bass that hits its
    // beats alike, more for a melody; a section's conventionality moves it
    public static StateKind<double> NoteDynamics { get; } = StateKinds.CreateMultiplicative<double>(Prefix + "NoteDynamics");

    // a drum's role in the groove (a DrumRole), set by the lowest layer, such as a section's over its song's
    public static StateKind<LayerValue<int>> DrumRole { get; } = StateKinds.CreateLowestLayerWins<int>(Prefix + "DrumRole");

    // how loud and busy a section is meant to be, around 0: it leans the section's draws, never decides them
    public static StateKind<double> Energy { get; } = StateKinds.CreateAdditive<double>(Prefix + "Energy", isShared: true);

    // every track plays the same chord, so the pool and the pick are the same for all of them
    public static PoolStateKinds<Chord> ChordPool { get; } = new(Prefix + "ChordPool", isShared: true);

    public sealed class IncrementalStateKinds
    {
        private readonly ImmutableArray<IStateKind> _all;

        public IncrementalStateKinds(string prefix)
        {
            prefix += "Incremental";

            ConsecutiveOffset = StateKinds.CreateAdditive<double>(prefix + "ConsecutiveOffset");
            RandomOffset = StateKinds.CreateAdditive<double>(prefix + "RandomOffset");
            Multiplier = StateKinds.CreateMultiplicative<double>(prefix + "Multiplier");

            _all =
            [
                ConsecutiveOffset,
                RandomOffset,
                Multiplier
            ];
        }

        public StateKind<double> ConsecutiveOffset { get; }

        public StateKind<double> RandomOffset { get; }

        public StateKind<double> Multiplier { get; }

        public ImmutableArray<IStateKind> GetAll()
        {
            return _all;
        }
    }

    /// <summary>
    ///     What the fills are drawn by: the chances of their rarer choices, which the layers multiply, such as the
    ///     section's chance scale and a drummer's signature, and each drum group's chance of joining a run.
    /// </summary>
    public sealed class FillStateKinds
    {
        public FillStateKinds(string prefix)
        {
            prefix += "Fill";

            OffBeatChance = StateKinds.CreateMultiplicative<double>(prefix + "OffBeatChance");
            FadeChance = StateKinds.CreateMultiplicative<double>(prefix + "FadeChance");
            EarlyLandingChance = StateKinds.CreateMultiplicative<double>(prefix + "EarlyLandingChance");
            RunChance = StateKinds.CreateAdditive<double>(prefix + "RunChance");
            Unconventionality = StateKinds.CreateAdditive<double>(prefix + "Unconventionality");
        }

        /// <summary>The chance a fill starts a note near an 8th off the beat, earlier or later.</summary>
        public StateKind<double> OffBeatChance { get; }

        /// <summary>The chance a run fades rather than swells.</summary>
        public StateKind<double> FadeChance { get; }

        /// <summary>The chance a landing is pushed a note near an 8th early.</summary>
        public StateKind<double> EarlyLandingChance { get; }

        /// <summary>A drum group's chance of joining a run, before the section's chance scale.</summary>
        public StateKind<double> RunChance { get; }

        /// <summary>
        ///     How unconventional a drum group is in a run, the power of the section's chance scale that its run chance
        ///     is multiplied by: 0 for the snare and the toms, which a plain section plays as often as a wild one.
        /// </summary>
        public StateKind<double> Unconventionality { get; }
    }

    public sealed class RhythmStateKinds
    {
        public RhythmStateKinds(string prefix)
        {
            prefix += "Rhythm";

            Period = new PeriodStateKinds(prefix + "Period");
            Phase = new DyadicRhythmStateKinds(prefix + "Phase");
            MaxRank = StateKinds.CreateAdditive<int>(prefix + "MaxRank");
            Seed = StateKinds.CreateAdditive<int>(prefix + "SeedValue");
            RankOffset = StateKinds.CreateAdditive<int>(prefix + "RankOffset");
            Fullness = StateKinds.CreateAdditive<double>(prefix + "Fullness");
            Variation = StateKinds.CreateAdditive<double>(prefix + "Variation");
            All =
            [
                Period.Value, Period.Power, Period.PrimeIndex, Phase.Value, Phase.Rank, Phase.RankedOffset, MaxRank, Seed, RankOffset,
                Fullness, Variation
            ];
        }

        /// <summary>Every kind of the rhythm's state, such as a drum that doubles another takes from it.</summary>
        public ImmutableArray<IStateKind> All { get; }

        public PeriodStateKinds Period { get; }

        public DyadicRhythmStateKinds Phase { get; }

        public StateKind<int> MaxRank { get; }

        public StateKind<int> Seed { get; }

        public StateKind<int> RankOffset { get; }

        /// <summary>
        ///     How much of a position's chance of being kept is left for every rank it is from the rank offset: near 1
        ///     every position is kept, as in a roll, and lower the pattern thins out.
        /// </summary>
        public StateKind<double> Fullness { get; }

        /// <summary>
        ///     The chance of a cycle drawing afresh what it keeps: 1 draws every cycle afresh, and 0 repeats the first,
        ///     as a riff.
        /// </summary>
        public StateKind<double> Variation { get; }
    }

    public sealed class PeriodStateKinds
    {
        public PeriodStateKinds(string prefix)
        {
            Value = StateKinds.CreateAdditive<double>(prefix + "Value");
            Power = StateKinds.CreateAdditive<int>(prefix + "Power");
            PrimeIndex = StateKinds.CreateAdditive<int>(prefix + "PrimeIndex");
        }

        public StateKind<double> Value { get; }

        public StateKind<int> Power { get; }

        public StateKind<int> PrimeIndex { get; }
    }

    public sealed class DyadicRhythmStateKinds
    {
        public DyadicRhythmStateKinds(string prefix)
        {
            Value = StateKinds.CreateAdditive<double>(prefix + "Value");
            Rank = StateKinds.CreateAdditive<int>(prefix + "Rank");
            RankedOffset = StateKinds.CreateAdditive<double>(prefix + "RankedOffset");
        }

        public StateKind<double> Value { get; }

        public StateKind<int> Rank { get; }

        public StateKind<double> RankedOffset { get; }
    }

    /// <summary>
    ///     A pool the layers add entries to, in the order they are merged, and an index into it the layers add draws
    ///     to; the index is folded into the pool's length to pick an entry.
    /// </summary>
    public sealed class PoolStateKinds<T>
    {
        /// <param name="isShared">Whether the pool and the pick must be the same for every track.</param>
        public PoolStateKinds(string prefix, bool isShared = false)
        {
            Index = StateKinds.CreateAdditive<int>(prefix + "Index", isShared: isShared);
            Collection = StateKinds.CreateCollection<T>(prefix + "Collection", isShared: isShared);
        }

        public StateKind<int> Index { get; }

        public StateKind<ImmutableArray<T>> Collection { get; }

        /// <summary>The entry the map's index picks from its pool.</summary>
        public T Pick(StateMap stateMap)
        {
            var collection = stateMap.GetStateValue(Collection);
            if (collection.IsEmpty)
                throw new InvalidOperationException($"The pool {Collection.Name} is empty.");

            return collection[stateMap.GetStateValue(Index).BounceInBounds(0, collection.Length - 1)];
        }
    }
}
