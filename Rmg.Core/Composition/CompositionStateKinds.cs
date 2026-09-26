using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Composition;

public static class CompositionStateKinds
{
    private const string Prefix = "Composition";

    public static RhythmStateKinds Rhythm { get; } = new(Prefix);

    public static StateKind<int> ValueSeed { get; } = StateKinds.CreateAdditive<int>(Prefix + "ValueSeedValue");

    public static IncrementalStateKinds IncrementalArticulationOffset { get; } =
        new(Prefix + StateKinds.ArticulationOffset.Name);

    public static IncrementalStateKinds IncrementalChordRootNoteOffset { get; } =
        new(Prefix + StateKinds.ChordRootNoteOffset.Name);

    public static IncrementalStateKinds IncrementalChordNoteOffset { get; } =
        new(Prefix + StateKinds.ChordNoteOffset.Name);

    // every track plays the same chord shape, so the pool and the pick are the same for all of them
    public static CollectionFromCollectionStateKinds<double> ChordNotePitchOffsets { get; } =
        new(Prefix + "ChordPitchOffsets", isShared: true);

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
        }

        public PeriodStateKinds Period { get; }

        public DyadicRhythmStateKinds Phase { get; }

        public StateKind<int> MaxRank { get; }

        public StateKind<int> Seed { get; }

        public StateKind<int> RankOffset { get; }
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

    public sealed class CollectionFromCollectionStateKinds<T>
    {
        /// <param name="isShared">Whether the pool and the pick must be the same for every track.</param>
        public CollectionFromCollectionStateKinds(string prefix, bool isShared = false)
        {
            // the value picked is a note's own, made from the shared pool at the note's position
            Value = StateKinds.CreateCollection<T>(prefix + "Value");
            Index = StateKinds.CreateAdditive<int>(prefix + "Index", isShared: isShared);
            Collection = StateKinds.CreateCollection<ImmutableArray<T>>(prefix + "Collection", isShared: isShared);
        }

        public StateKind<ImmutableArray<T>> Value { get; }

        public StateKind<int> Index { get; }

        public StateKind<ImmutableArray<ImmutableArray<T>>> Collection { get; }
    }
}
