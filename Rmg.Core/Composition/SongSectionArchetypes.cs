using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

public static class SongSectionArchetypes
{
    public static class Names
    {
        // ReSharper disable MemberHidesStaticFromOuterClass
        public const string Intro = "Intro";
        public const string Verse = "Verse";
        public const string PreChorus = "Pre-Chorus";
        public const string Chorus = "Chorus";
        public const string Bridge = "Bridge";

        public const string Outro = "Outro";
        // ReSharper restore MemberHidesStaticFromOuterClass
    }

    public static SongSectionArchetype Intro { get; } = new(
        Names.Intro,
        1,
        0,
        [
            new Weighted<string>(1, Names.Verse),
            new Weighted<string>(0.25, Names.Chorus),
        ]
    );

    public static SongSectionArchetype Verse { get; } = new(
        Names.Verse,
        1,
        0,
        [
            new Weighted<string>(1, Names.Chorus),
            new Weighted<string>(0.25, Names.PreChorus),
        ]
    );

    public static SongSectionArchetype PreChorus { get; } = new(
        Names.PreChorus,
        0,
        0,
        [
            new Weighted<string>(1, Names.Chorus),
        ]
    );

    public static SongSectionArchetype Chorus { get; } = new(
        Names.Chorus,
        0.25,
        0,
        [
            new Weighted<string>(1, Names.Verse),
            new Weighted<string>(0.25, Names.Bridge),
        ]
    );

    public static SongSectionArchetype Bridge { get; } = new(
        Names.Bridge,
        0,
        0,
        [
            new Weighted<string>(1, Names.Verse),
            new Weighted<string>(0.25, Names.Chorus),
        ]
    );

    public static SongSectionArchetype Outro { get; } = new(
        Names.Outro,
        0,
        0.25,
        []
    );

    public static ImmutableArray<SongSectionArchetype> All { get; } =
    [
        Intro,
        Verse,
        PreChorus,
        Chorus,
        Bridge,
        Outro,
    ];

    private static readonly ImmutableDictionary<string, SongSectionArchetype> ArchetypesByNames =
        All.ToImmutableDictionary(x => x.Name);

    public static SongSectionArchetype GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (ArchetypesByNames.TryGetValue(name, out var archetype))
            return archetype;

        throw new ArgumentOutOfRangeException(nameof(name), $"No song section archetype with name '{name}'");
    }

    public static ImmutableArray<SongSectionArchetype> GenerateSongStructure(
        IGenerationContext context,
        int sectionCount
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sectionCount);

        var songStructure = ImmutableArray.CreateBuilder<SongSectionArchetype>(sectionCount);

        var remainingSectionCount = sectionCount;
        var hasOutro = context.TestProbability(Outro.SongEndWeight);
        if (hasOutro)
            remainingSectionCount--;

        var sectionWeights = All
            .Where(x => x.SongStartWeight > 0)
            .Select(x => new Weighted<string>(x.SongStartWeight, x.Name))
            .ToImmutableArray();
        
        for(;remainingSectionCount > 0; remainingSectionCount--)
        {
            var sectionName = Generators.WeightedValue(sectionWeights)(context);
            var sectionArchetype = GetByName(sectionName);
            songStructure.Add(sectionArchetype);
            sectionWeights = sectionArchetype.ResolvesToSectionsWeighted;
        }
        
        if (hasOutro)
            songStructure.Add(Outro);

        return songStructure.ToImmutable();
    }
}