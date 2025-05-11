using System.Collections.Immutable;
using System.Numerics;
using Rmg.Core.Events;

namespace Rmg.Core.Composition;

public static class CompositionStateKinds
{
    private const string Prefix = "Composition";
    private static readonly ImmutableArray<IStateKind> _all;
        
    public static RhythmStateKinds Rhythm { get; }
    
    public static NumberFromCollectionStateKinds<int> ValueSeed { get; }

    public static IncrementalStateKinds IncrementalArticulationOffset { get; }

    public static IncrementalStateKinds IncrementalChordRootNoteOffset { get; }

    public static IncrementalStateKinds IncrementalChordNoteOffset { get; }

    public static StateKind<int> ChordNoteOffsetCount { get; }
    
    public static StateKindControl Control { get; }
    
    public static CollectionFromCollectionStateKinds<double> ChordNoteInScaleOffsets { get; } =
        new(Prefix + "ChordScaleOffsets");

    static CompositionStateKinds()
    {
        
        Rhythm = new RhythmStateKinds(Prefix);
        ValueSeed = new NumberFromCollectionStateKinds<int>(Prefix + "ValueSeed");
        IncrementalArticulationOffset = new IncrementalStateKinds(Prefix + StateKinds.ArticulationOffset.Name);
        IncrementalChordRootNoteOffset = new IncrementalStateKinds(Prefix + StateKinds.ChordRootNoteOffset.Name);
        IncrementalChordNoteOffset = new IncrementalStateKinds(Prefix + StateKinds.ChordNoteOffset.Name);
        ChordNoteOffsetCount = StateKinds.CreateAdditive<int>(Prefix + StateKinds.ChordNoteOffset.Name + "Count");
        Control = new StateKindControl(Prefix);

        _all =
        [
            ..Rhythm.GetAll(),
            ..ValueSeed.GetAll(),
            ..IncrementalArticulationOffset.GetAll(),
            ..IncrementalChordRootNoteOffset.GetAll(),
            ..IncrementalChordNoteOffset.GetAll(),
            ..ChordNoteInScaleOffsets.GetAll(),
            ChordNoteOffsetCount,
            ..Control.GetAll(),
        ];
    }
        
    public static ImmutableArray<IStateKind> GetAll() => _all;

    public sealed class StateKindControl
    {
        private readonly ImmutableArray<IStateKind> _all;
        
        public StateKind<bool> ChordRootOffsetEnabled { get; }
        
        public StateKind<bool> ChordNoteOffsetEnabled { get; }

        public StateKindControl(string prefix)
        {
            prefix += "Control";
            
            ChordRootOffsetEnabled = StateKinds.CreateBoolPessimistic(prefix + "ChordRootOffsetEnabled");
            ChordNoteOffsetEnabled = StateKinds.CreateBoolPessimistic(prefix + "ChordNoteOffsetEnabled");
            
            _all =
            [
                ChordRootOffsetEnabled,
                ChordNoteOffsetEnabled,
            ];
        }
        
        public ImmutableArray<IStateKind> GetAll() => _all;
    }

    public sealed class IncrementalStateKinds
    {
        private readonly ImmutableArray<IStateKind> _all;

        public StateKind<double> ConsecutiveOffset { get; }

        public StateKind<double> RandomOffset { get; }

        public StateKind<double> Multiplier { get; }

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
                Multiplier,
            ];
        }
        
        public ImmutableArray<IStateKind> GetAll() => _all;
    }

    public sealed class RhythmStateKinds
    {
        private readonly ImmutableArray<IStateKind> _all;
        
        public StateKind<double> Duration { get; }

        public PeriodStateKinds Period { get; }

        public DyadicRhythmStateKinds Phase { get; }

        public StateKind<int> MaxRank { get; }

        public StateKind<double> Intensity { get; }
        
        public NumberFromCollectionStateKinds<int> Seed { get; }
        
        public StateKind<int> RankOffset { get; }

        public RhythmStateKinds(string prefix)
        {
            prefix += "Rhythm";
            
            Duration = StateKinds.CreateAdditive<double>(prefix + "Duration");
            Period = new PeriodStateKinds(prefix + "Period");
            Phase = new DyadicRhythmStateKinds(prefix + "Phase");
            MaxRank = StateKinds.CreateAdditive<int>(prefix + "MaxRank");
            Intensity = StateKinds.CreateMultiplicative<double>(prefix + "Intensity");
            Seed = new NumberFromCollectionStateKinds<int>(prefix + "Seed");
            RankOffset = StateKinds.CreateAdditive<int>(prefix + "RankOffset");

            _all =
            [
                Duration,
                ..Period.GetAll(),
                ..Phase.GetAll(),
                MaxRank,
                Intensity,
                ..Seed.GetAll(),
            ];
        }
        
        public ImmutableArray<IStateKind> GetAll() => _all;
    }

    public sealed class PeriodStateKinds
    {
        private readonly ImmutableArray<IStateKind> _all;
        
        public StateKind<double> Value { get; }
        
        public StateKind<int> Power { get; }
        
        public StateKind<int> PrimeIndex { get; }
        
        public PeriodStateKinds(string prefix)
        {
            Value = StateKinds.CreateAdditive<double>(prefix + "Value");
            Power = StateKinds.CreateAdditive<int>(prefix + "Power");
            PrimeIndex = StateKinds.CreateAdditive<int>(prefix + "PrimeIndex");

            _all =
            [
                Value,
                Power,
                PrimeIndex
            ];
        }
        
        public ImmutableArray<IStateKind> GetAll() => _all;
    }

    public sealed class DyadicRhythmStateKinds
    {
        private readonly ImmutableArray<IStateKind> _all;
        
        public StateKind<double> Value { get; }
        
        public StateKind<int> Rank { get; }
        
        public StateKind<double> RankedOffset { get; }
        
        public DyadicRhythmStateKinds(string prefix)
        {
            Value = StateKinds.CreateAdditive<double>(prefix + "Value");
            Rank = StateKinds.CreateAdditive<int>(prefix + "Rank");
            RankedOffset = StateKinds.CreateAdditive<double>(prefix + "RankedOffset");

            _all =
            [
                Value,
                Rank,
                RankedOffset
            ];
        }
        
        public ImmutableArray<IStateKind> GetAll() => _all;
    }
    
    public sealed class NumberFromCollectionStateKinds<T>
        where T : INumber<T>
    {
        // ReSharper disable once MemberHidesStaticFromOuterClass
        private readonly ImmutableArray<IStateKind> _all;
        
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public StateKind<T> Value { get; }

        public StateKind<int> Index { get; }

        public StateKind<ImmutableArray<T>> Collection { get; }

        public NumberFromCollectionStateKinds(string prefix)
        {
            Value = StateKinds.CreateAdditive<T>(prefix + "Value");
            Index = StateKinds.CreateAdditive<int>(prefix + "Index");
            Collection = StateKinds.CreateCollection<T>(prefix + "Collection");

            _all =
            [
                Value,
                Index,
                Collection
            ];
        }
        
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public ImmutableArray<IStateKind> GetAll() => _all;
    }
    
    public sealed class CollectionFromCollectionStateKinds<T>
    {
        // ReSharper disable once MemberHidesStaticFromOuterClass
        private readonly ImmutableArray<IStateKind> _all;
        
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public StateKind<ImmutableArray<T>> Value { get; }

        public StateKind<int> Index { get; }

        public StateKind<ImmutableArray<ImmutableArray<T>>> Collection { get; }

        public CollectionFromCollectionStateKinds(string prefix)
        {
            Value = StateKinds.CreateCollection<T>(prefix + "Value");
            Index = StateKinds.CreateAdditive<int>(prefix + "Index");
            Collection = StateKinds.CreateCollection<ImmutableArray<T>>(prefix + "Collection");

            _all =
            [
                Value,
                Index,
                Collection
            ];
        }
        
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public ImmutableArray<IStateKind> GetAll() => _all;
    }
}