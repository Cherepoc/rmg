using System.Collections.Immutable;

namespace Rmg.Core.Composition;

public sealed class PercussionInstrumentDefinition
{
    public ImmutableArray<int> ArticulationCodes { get; }

    public double Weight { get; }

    public PercussionInstrumentDefinition(ImmutableArray<int> articulationCodes, double weight)
    {
        if (articulationCodes.Length == 0)
            throw new ArgumentException("Articulation codes cannot be empty.", nameof(articulationCodes));

        ArticulationCodes = articulationCodes;
        Weight = weight;
    }

    public static ImmutableArray<PercussionInstrumentDefinition> Definitions { get; } =
    [
        // kick
        new([35, 36], 1.0),
        // snare cross stick
        new([37], 0.2),
        // acoustic snare
        new([38], 0.2),
        // electric snare
        new([40], 1.0),
        // hi-hat
        new([42, 44, 46], 1.0),
        // tom
        new([41, 43, 45, 47, 48, 50], 0.2),
        // ride
        new([51, 53, 59], 0.2),
        // cymbal
        new([49, 52, 55, 57], 0.2),
        // clap
        new([39], 0.2),
        // tambourine
        new([54], 0.1),
        // cowbell
        new([56], 0.1),
        // vibraslap
        new([58], 0.1),
        // bongo
        new([60, 61], 0.1),
        // conga
        new([62, 63, 64], 0.1),
        // timbale
        new([65, 66], 0.1),
        // agogo
        new([67, 68], 0.1),
        // cabasa
        new([69], 0.1),
        // maracas
        new([70], 0.1),
        // whistle
        new([71, 72], 0.1),
        // guiro
        new([73, 74], 0.1),
        // claves
        new([75], 0.1),
        // wood block
        new([76, 77], 0.1),
        // cuica
        new([78, 79], 0.1),
        // triangle
        new([80, 81], 0.1),
    ];
}