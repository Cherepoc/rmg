namespace Rmg.Core.Composition;

/// <summary>
///     The drums. A weight is how likely the drum is to be chosen among the other drums it competes with, both
///     when choosing the drums of a song and the drums of a section.
/// </summary>
public static class DrumDefinitions
{
    public static PercussionInstrumentDefinition Kick { get; } = new("Kick", [35, 36], 1.0);

    public static PercussionInstrumentDefinition CrossStick { get; } = new("Snare Cross Stick", [37], 0.2);

    public static PercussionInstrumentDefinition AcousticSnare { get; } = new("Acoustic Snare", [38], 1.0);

    public static PercussionInstrumentDefinition ElectricSnare { get; } = new("Electric Snare", [40], 0.7);

    public static PercussionInstrumentDefinition Clap { get; } = new("Clap", [39], 0.3);

    // hi-hat plays faster than the other timekeepers
    public static PercussionInstrumentDefinition HiHat { get; } = new(
        "Hi-Hat",
        [42, 44, 46],
        1.0,
        builder => builder.Add(CompositionStateKinds.Rhythm.Period.Power, -1)
    );

    public static PercussionInstrumentDefinition Ride { get; } = new("Ride", [51, 53, 59], 0.5);

    public static PercussionInstrumentDefinition Tambourine { get; } = new("Tambourine", [54], 0.2);

    public static PercussionInstrumentDefinition Cabasa { get; } = new("Cabasa", [69], 0.15);

    public static PercussionInstrumentDefinition Maracas { get; } = new("Maracas", [70], 0.15);

    public static PercussionInstrumentDefinition Tom { get; } = new("Tom", [41, 43, 45, 47, 48, 50], 1.0);

    public static PercussionInstrumentDefinition Cymbal { get; } = new("Cymbal", [49, 52, 55, 57], 1.0);

    public static PercussionInstrumentDefinition Vibraslap { get; } = new("Vibraslap", [58], 0.15);

    public static PercussionInstrumentDefinition Bongo { get; } = new("Bongo", [60, 61], 0.15);

    public static PercussionInstrumentDefinition Conga { get; } = new("Conga", [62, 63, 64], 0.15);

    public static PercussionInstrumentDefinition Timbale { get; } = new("Timbale", [65, 66], 0.1);

    public static PercussionInstrumentDefinition Agogo { get; } = new("Agogo", [67, 68], 0.1);

    public static PercussionInstrumentDefinition Cowbell { get; } = new("Cowbell", [56], 0.1);

    public static PercussionInstrumentDefinition Claves { get; } = new("Claves", [75], 0.1);

    public static PercussionInstrumentDefinition WoodBlock { get; } = new("Wood Block", [76, 77], 0.1);

    public static PercussionInstrumentDefinition Guiro { get; } = new("Guiro", [73, 74], 0.1);

    public static PercussionInstrumentDefinition Triangle { get; } = new("Triangle", [80, 81], 0.1);

    public static PercussionInstrumentDefinition Cuica { get; } = new("Cuica", [78, 79], 0.05);

    public static PercussionInstrumentDefinition Whistle { get; } = new("Whistle", [71, 72], 0.05);
}
