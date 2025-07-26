using System.Collections.Immutable;
using System.Diagnostics;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

[DebuggerDisplay("SongSectionArchetype {Name}")]
public sealed class SongSectionArchetype
{
    public SongSectionArchetype(
        string name,
        double songStartWeight,
        double songEndWeight,
        ImmutableArray<Weighted<string>> resolvesToSectionsWeighted
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(songStartWeight);
        ArgumentOutOfRangeException.ThrowIfNegative(songEndWeight);

        Name = name;
        SongStartWeight = songStartWeight;
        SongEndWeight = songEndWeight;
        ResolvesToSectionsWeighted = resolvesToSectionsWeighted;
    }

    public string Name { get; }

    public double SongStartWeight { get; }

    public double SongEndWeight { get; }

    public ImmutableArray<Weighted<string>> ResolvesToSectionsWeighted { get; }
}
