namespace RMG.Core.Generation

open RMG.Core
open RMG.Core.Generation

type PercussionInstrumentDefinition =
    {
        ArticulationCodes: list<ArticulationCode>;
        PatternGenerationDefinitions: PatternGenerationDefinition list;
        Weight: float
    }

module InstrumentDefinition =
    let kickPatternGenerationDefinitions: PatternGenerationDefinition list =
        [
            {
                DurationRank = 1;
                PeriodPowerOffset = 1;
                PeriodPrimeOffset = 0;
                PhaseRankOffset = 0;
                ProbabilityPowerOffset = 4
            }
        ]

    let snarePatternGenerationDefinitions: PatternGenerationDefinition list =
        [
            {
                DurationRank = 1;
                PeriodPowerOffset = 1;
                PeriodPrimeOffset = 0;
                PhaseRankOffset = 2;
                ProbabilityPowerOffset = 4
            }
        ]

    let hiHatPatternGenerationDefinitions: PatternGenerationDefinition list =
        [
            {
                DurationRank = 1;
                PeriodPowerOffset = 2;
                PeriodPrimeOffset = 0;
                PhaseRankOffset = 0;
                ProbabilityPowerOffset = 4
            }
        ]

    let crashPatternGenerationDefinitions: PatternGenerationDefinition list =
        [
            {
                DurationRank = 1;
                PeriodPowerOffset = 0;
                PeriodPrimeOffset = 0;
                PhaseRankOffset = 0;
                ProbabilityPowerOffset = 4
            };
            {
                DurationRank = 2;
                PeriodPowerOffset = 0;
                PeriodPrimeOffset = 0;
                PhaseRankOffset = 0;
                ProbabilityPowerOffset = 4
            }
        ]

    let tomPatternGenerationDefinitions: PatternGenerationDefinition list =
        [
            {
                DurationRank = 1;
                PeriodPowerOffset = 0;
                PeriodPrimeOffset = 0;
                PhaseRankOffset = 2;
                ProbabilityPowerOffset = 4
            };
            {
                DurationRank = 2;
                PeriodPowerOffset = 0;
                PeriodPrimeOffset = 0;
                PhaseRankOffset = 2;
                ProbabilityPowerOffset = 4
            }
        ]

    let percussionInstrumentDefinitions: PercussionInstrumentDefinition list =
        [
            // kick
            {
                ArticulationCodes = [ 35uy; 36uy ];
                PatternGenerationDefinitions = kickPatternGenerationDefinitions;
                Weight = 1.0
            };
            // snare cross stick
            {
                ArticulationCodes = [ 37uy ];
                PatternGenerationDefinitions = snarePatternGenerationDefinitions;
                Weight = 0.2
            };
            // acoustic snare
            {
                ArticulationCodes = [ 38uy ];
                PatternGenerationDefinitions = snarePatternGenerationDefinitions;
                Weight = 0.2
            };
            // electric snare
            {
                ArticulationCodes = [ 40uy ];
                PatternGenerationDefinitions = snarePatternGenerationDefinitions;
                Weight = 1.0
            };
            // hi-hat
            {
                ArticulationCodes = [ 42uy; 44uy; 46uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 1.0
            };
            // tom
            {
                ArticulationCodes = [ 41uy; 43uy; 45uy; 47uy; 48uy; 50uy ];
                PatternGenerationDefinitions = tomPatternGenerationDefinitions;
                Weight = 0.2
            };
            // ride
            {
                ArticulationCodes = [ 51uy; 53uy; 59uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.2
            };
            // cymbal
            {
                ArticulationCodes = [ 51uy; 53uy; 59uy ];
                PatternGenerationDefinitions = crashPatternGenerationDefinitions;
                Weight = 0.2
            };
            // clap
            {
                ArticulationCodes = [ 39uy ];
                PatternGenerationDefinitions = snarePatternGenerationDefinitions;
                Weight = 0.2
            };
            // tambourine
            {
                ArticulationCodes = [ 54uy ];
                PatternGenerationDefinitions = snarePatternGenerationDefinitions;
                Weight = 0.1
            };
            // cowbell
            {
                ArticulationCodes = [ 56uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.1
            };
            // vibraslap
            {
                ArticulationCodes = [ 58uy ];
                PatternGenerationDefinitions = crashPatternGenerationDefinitions;
                Weight = 0.1
            };
            // bongo
            {
                ArticulationCodes = [ 60uy; 61uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.1
            };
            // conga
            {
                ArticulationCodes = [ 62uy; 63uy; 64uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.1
            };
            // timbale
            {
                ArticulationCodes = [ 65uy; 66uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.1
            };
            // agogo
            {
                ArticulationCodes = [ 67uy; 68uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.1
            };
            // cabasa
            {
                ArticulationCodes = [ 69uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.1
            };
            // maracas
            {
                ArticulationCodes = [ 70uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.1
            };
            // whistle
            {
                ArticulationCodes = [ 71uy; 72uy ];
                PatternGenerationDefinitions = crashPatternGenerationDefinitions;
                Weight = 0.1
            };
            // guiro
            {
                ArticulationCodes = [ 73uy; 74uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.1
            };
            // claves
            {
                ArticulationCodes = [ 75uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.1
            };
            // wood block
            {
                ArticulationCodes = [ 76uy; 77uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.1
            };
            // cuica
            {
                ArticulationCodes = [ 78uy; 79uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.1
            };
            // triangle
            {
                ArticulationCodes = [ 80uy; 81uy ];
                PatternGenerationDefinitions = hiHatPatternGenerationDefinitions;
                Weight = 0.1
            }
        ]
