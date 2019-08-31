using System;
using System.Collections.Generic;
using System.IO;
using Commons.Music.Midi;
using RMG.Core.Generation;
using RMG.Core.Midi;
using RMG.Core.Music;
using Xunit;

namespace RMG.Tests
{
    public class TestClass
    {
        private static readonly IntGenerator KeyEventGenerator = new IntGenerator
        {
            Min = -6 + 1,
            Max = 6 + 1
        };

        private static readonly IntPickerGenerator OctaveEventGenerator = new IntPickerGenerator
        {
            MinValueGenerator = new ConstantGenerator<int>(-2),
            MaxValueGenerator = new ConstantGenerator<int>(2),
            ProbabilityFunctionGenerator = new ConstantGenerator<GeometricProbabilityFunction>(
                new GeometricProbabilityFunction
                {
                    MinProbability = 0,
                    MaxProbability = 1,
                    ProbabilityMultiplier = 0.5,
                    Offset = 0
                })
        };

        private static readonly ScaleNoteOffsetGenerator ScaleNoteOffsetEventGenerator = new ScaleNoteOffsetGenerator
        {
            GeometricProbabilityFunction = new GeometricProbabilityFunction
            {
                MaxProbability = 0.5,
                MinProbability = 0,
                ProbabilityMultiplier = 0.5
            }
        };

        private static readonly DoubleGenerator VolumeEventGenerator = new DoubleGenerator
        {
            Min = 0.9,
            Max = 1
        };

        private static ObjectGenerator<NoteBase> CreateSongNoteBaseGenerator()
        {
            return new ObjectGenerator<NoteBase>()
                .WithProperty(x => x.Key, property => property.WithGenerator(KeyEventGenerator))
                .WithProperty(x => x.Octave, property => property.WithGenerator(OctaveEventGenerator))
                .WithProperty(x => x.ScaleOffset, property => property.WithGenerator(ScaleNoteOffsetEventGenerator))
                .WithProperty(x => x.Volume, property => property.WithValue(1));
        }

        private static ObjectGenerator<NoteBase> CreateTrackNoteBaseGenerator()
        {
            return new ObjectGenerator<NoteBase>()
                .WithProperty(x => x.Key, property => property.WithValue(0))
                .WithProperty(x => x.Octave, property => property.WithGenerator(new IntGenerator(-2, 2 + 1)))
                .WithProperty(x => x.ScaleOffset, property => property.WithValue(new int[Scale.ScaleRankCount]))
                .WithProperty(x => x.Volume, property => property.WithGenerator(VolumeEventGenerator));
        }

        private static TimedEventGenerator<T> CreateTimelineGenerator<T>(
            double offset,
            double scale,
            int maxRank,
            double minRankProbability,
            double maxRankProbability,
            double rankProbabilityMultiplier,
            IGenerator eventGenerator
        )
        {
            return new TimedEventGenerator<T>
            {
                OffsetGenerator = new ConstantGenerator<double>(offset),
                ScaleGenerator = new ConstantGenerator<double>(scale),
                MaxRankGenerator = new ConstantGenerator<int>(maxRank),
                RankProbabilityFunctionGenerator =
                    new ConstantGenerator<GeometricProbabilityFunction>(
                        new GeometricProbabilityFunction
                        {
                            MinProbability = minRankProbability,
                            MaxProbability = maxRankProbability,
                            ProbabilityMultiplier = rankProbabilityMultiplier
                        }),
                EventGenerator = eventGenerator
            };
        }

        private static TimedEventGenerator<T> GenerateSongNoteBaseTimeEventGenerator<T>(IGenerator eventGenerator)
        {
            return CreateTimelineGenerator<T>(
                0,
                64,
                3,
                1.0 / 8,
                1.0 / 32,
                0.25,
                eventGenerator);
        }

        private static readonly ObjectGenerator<NoteBasePattern> SongNoteBaseTimelineGenerator =
            new ObjectGenerator<NoteBasePattern>()
                .WithProperty(
                    x => x.KeyTimeline,
                    property => property
                        .WithGenerator(GenerateSongNoteBaseTimeEventGenerator<int>(KeyEventGenerator)))
                .WithProperty(
                    x => x.OctaveTimeline,
                    property => property
                        .WithGenerator(GenerateSongNoteBaseTimeEventGenerator<int>(OctaveEventGenerator)))
                .WithProperty(
                    x => x.ScaleOffsetTimeline,
                    property => property
                        .WithGenerator(GenerateSongNoteBaseTimeEventGenerator<int[]>(ScaleNoteOffsetEventGenerator)))
                .WithProperty(
                    x => x.VolumeTimeline,
                    property => property
                        .WithGenerator(GenerateSongNoteBaseTimeEventGenerator<double>(VolumeEventGenerator)));

        private static readonly ObjectGenerator<NoteBasePattern> EmptyNoteBaseTimelineGenerator =
            new ObjectGenerator<NoteBasePattern>()
                .WithProperty(
                    x => x.KeyTimeline,
                    property => property
                        .WithValue(new List<TimedEvent<int>>()))
                .WithProperty(
                    x => x.OctaveTimeline,
                    property => property
                        .WithValue(new List<TimedEvent<int>>()))
                .WithProperty(
                    x => x.ScaleOffsetTimeline,
                    property => property
                        .WithValue(new List<TimedEvent<int[]>>()))
                .WithProperty(
                    x => x.VolumeTimeline,
                    property => property
                        .WithValue(new List<TimedEvent<double>>()));

        private static TimedEventGenerator<T> GeneratePartNoteBaseTimeEventGenerator<T>(IGenerator eventGenerator)
        {
            return new TimedEventGenerator<T>
            {
                OffsetGenerator = new ConstantGenerator<double>(0),
                ScaleGenerator = new DoublePowerGenerator
                {
                    ValueGenerator = new ConstantGenerator<double>(4),
                    PowerGenerator = new DoubleAddGenerator
                    {
                        ValueGenerator =
                            new GeneratedCollectionItemIndexGenerator<IList<Part>>(),
                        AdditiveGenerator = new ConstantGenerator<double>(3)
                    }
                },
                MaxRankGenerator = new ConstantGenerator<int>(2),
                RankProbabilityFunctionGenerator =
                    new ConstantGenerator<GeometricProbabilityFunction>(
                        new GeometricProbabilityFunction
                        {
                            MinProbability = 0,
                            MaxProbability = 1.0 / 2,
                            ProbabilityMultiplier = 1.0 / 2
                        }),
                EventGenerator = eventGenerator
            };
        }

        private static readonly ObjectGenerator<NoteBasePattern> PartNoteBaseTimelineGenerator =
            new ObjectGenerator<NoteBasePattern>()
                .WithProperty(
                    x => x.KeyTimeline,
                    property => property
                        .WithGenerator(GeneratePartNoteBaseTimeEventGenerator<int>(KeyEventGenerator)))
                .WithProperty(
                    x => x.OctaveTimeline,
                    property => property
                        .WithGenerator(GeneratePartNoteBaseTimeEventGenerator<int>(OctaveEventGenerator)))
                .WithProperty(
                    x => x.ScaleOffsetTimeline,
                    property => property
                        .WithGenerator(GeneratePartNoteBaseTimeEventGenerator<int[]>(ScaleNoteOffsetEventGenerator)))
                .WithProperty(
                    x => x.VolumeTimeline,
                    property => property
                        .WithGenerator(GeneratePartNoteBaseTimeEventGenerator<double>(VolumeEventGenerator)));

        private static TimedEventGenerator<T> GeneratePatternNoteBaseTimeEventGenerator<T>(IGenerator eventGenerator)
        {
            return CreateTimelineGenerator<T>(
                0,
                4,
                3,
                0,
                1.0 / 8,
                0.25,
                eventGenerator);
        }

        private static readonly ObjectGenerator<NoteBasePattern> PatternNoteBaseTimelineGenerator =
            new ObjectGenerator<NoteBasePattern>()
                .WithProperty(
                    x => x.KeyTimeline,
                    property => property
                        .WithGenerator(
                            GeneratePatternNoteBaseTimeEventGenerator<int>(
                                new ConstantGenerator<int>
                                {
                                    Value = 0
                                })))
                .WithProperty(
                    x => x.OctaveTimeline,
                    property => property
                        .WithGenerator(GeneratePatternNoteBaseTimeEventGenerator<int>(OctaveEventGenerator)))
                .WithProperty(
                    x => x.ScaleOffsetTimeline,
                    property => property
                        .WithGenerator(GeneratePatternNoteBaseTimeEventGenerator<int[]>(ScaleNoteOffsetEventGenerator)))
                .WithProperty(
                    x => x.VolumeTimeline,
                    property => property
                        .WithGenerator(GeneratePatternNoteBaseTimeEventGenerator<double>(VolumeEventGenerator)));

        private static readonly IntGenerator TemplateCountGenerator = new IntGenerator(8, 16 + 1);

        private static IGenerator CreateNoteGenerator()
        {
            return new TimedEventGenerator<Note>
            {
                OffsetGenerator = new RankedPositionGenerator
                {
                    Min = 0,
                    Max = 4,
                    MaxRank = 4,
                    Offset = 2,
                    Period = 4,
                    RankMultiplier = 0.5
                },
                ScaleGenerator = new DoubleDivisionGenerator
                {
                    DividentGenerator = new RhythmPeriodGenerator
                    {
                        MaxNumberGenerator = new ConstantGenerator<int>(8),
                        RankProbabilityFunctionGenerator = new ConstantGenerator<GeometricProbabilityFunction>
                        {
                            Value = new GeometricProbabilityFunction
                            {
                                MinProbability = 0,
                                MaxProbability = 1,
                                ProbabilityMultiplier = 0.75
                            }
                        }
                    },
                    DivisorGenerator = new DoublePowerGenerator
                    {
                        ValueGenerator = new ConstantGenerator<int>(2),
                        PowerGenerator = new IntPickerGenerator
                        {
                            MinValueGenerator = new ConstantGenerator<int>(-1),
                            MaxValueGenerator = new ConstantGenerator<int>(3),
                            ProbabilityFunctionGenerator = new ConstantGenerator<GeometricProbabilityFunction>
                            {
                                Value = new GeometricProbabilityFunction
                                {
                                    MinProbability = 0,
                                    MaxProbability = 1,
                                    ProbabilityMultiplier = 0.75,
                                    Offset = 0
                                }
                            }
                        }
                    }
                },
                MaxRankGenerator = new ConstantGenerator<int>(4),
                RankProbabilityFunctionGenerator =
                    new ConstantGenerator<GeometricProbabilityFunction>(
                        new GeometricProbabilityFunction
                        {
                            MinProbability = 0,
                            MaxProbability = 1,
                            ProbabilityMultiplier = 0.25
                        }),
                EventGenerator = new ObjectGenerator<Note>()
                    .WithProperty(
                        x => x.Duration,
                        property => property.WithGenerator(
                            new RankedPositionGenerator
                            {
                                Min = 0,
                                Max = 2,
                                Offset = 1,
                                Period = 2,
                                MaxRank = 4,
                                RankMultiplier = 0.5
                            }))
                    .WithProperty(
                        x => x.Octave,
                        property => property.WithValue(0))
                    .WithProperty(
                        x => x.Volume,
                        property => property.WithGenerator(
                            new RankedPositionGenerator
                            {
                                Min = 0.75,
                                Max = 1,
                                Offset = 0.5,
                                Period = 1,
                                MaxRank = 5,
                                RankMultiplier = 0.75
                            }))
                    .WithProperty(
                        x => x.ScaleOffset,
                        property => property.WithGenerator(
                            new ScaleNoteOffsetGenerator
                            {
                                GeometricProbabilityFunction =
                                    new GeometricProbabilityFunction
                                    {
                                        MaxProbability = 0.5,
                                        MinProbability = 0,
                                        ProbabilityMultiplier = 1
                                    }
                            }))
            };
        }

        private static IGenerator CreateRankedPatternTemplatesGenerator()
        {
            return new ReverseCollectionGenerator<IList<Pattern>>
            {
                CollectionGenerator = new CollectionGenerator<IList<Pattern>>
                {
                    ItemCountGenerator = new ConstantGenerator<int>(2),
                    ItemGenerator = new CollectionGenerator<Pattern>
                    {
                        ItemGenerator = new ObjectGenerator<Pattern>()
                            .WithProperty(
                                x => x.Duration,
                                property => property.WithGenerator(
                                    new DoublePowerGenerator
                                    {
                                        ValueGenerator = new ConstantGenerator<double>(4),
                                        PowerGenerator = new DoubleAddGenerator
                                        {
                                            ValueGenerator =
                                                new GeneratedCollectionItemIndexGenerator<IList<Pattern>>(),
                                            AdditiveGenerator = new ConstantGenerator<double>(1)
                                        }
                                    }))
                            .WithProperty(
                                x => x.NoteBasePattern,
                                property => property.WithGenerator(EmptyNoteBaseTimelineGenerator))
                            .WithProperty(
                                x => x.Notes,
                                notesProperty => notesProperty.WithGenerator(
                                    new ConditionalGenerator
                                    {
                                        ConditionGenerator = new ConditionalEqualGenerator
                                        {
                                            FirstGenerator =
                                                new GeneratedCollectionItemIndexGenerator<IList<Pattern>>(),
                                            SecondGenerator = new ConstantGenerator<int>(0)
                                        },
                                        TrueGenerator = CreateNoteGenerator(),
                                        FalseGenerator = new ConstantGenerator<IList<TimedEvent<Note>>>(null)
                                    }))
                            .WithProperty(
                                x => x.Patterns,
                                patternsProperty => patternsProperty.WithGenerator(
                                    new ConditionalGenerator
                                    {
                                        ConditionGenerator = new ConditionalNotGenerator
                                        {
                                            ConditionGenerator = new ConditionalEqualGenerator
                                            {
                                                FirstGenerator =
                                                    new GeneratedCollectionItemIndexGenerator<IList<Pattern>>(),
                                                SecondGenerator = new ConstantGenerator<int>(0)
                                            }
                                        },
                                        TrueGenerator = new SequentialTimelineGenerator<Pattern>
                                        {
                                            EventGenerator = new CollectionItemPickerGenerator<Pattern>
                                            {
                                                CollectionGenerator = new UniqueCollectionPickerGenerator<Pattern>
                                                {
                                                    CollectionGenerator =
                                                        new CollectionItemGenerator<IList<Pattern>>
                                                        {
                                                            CollectionGenerator =
                                                                new GeneratedCollectionGenerator<IList<Pattern>>(),
                                                            IndexGenerator = new DoubleAddGenerator
                                                            {
                                                                ValueGenerator =
                                                                    new GeneratedCollectionItemIndexGenerator<
                                                                        IList<Pattern>
                                                                    >(),
                                                                AdditiveGenerator = new ConstantGenerator<int>(-1)
                                                            }
                                                        },
                                                    ItemCountGenerator = new IntGenerator(1, 4 + 1)
                                                }.Cache<Pattern, IList<Pattern>>()
                                            }
                                        },
                                        FalseGenerator = new ConstantGenerator<IList<TimedEvent<Pattern>>>(null)
                                    })),
                        ItemCountGenerator = TemplateCountGenerator
                    }
                }
            };
        }

        private static IGenerator CreateRankedPartTemplatesGenerator()
        {
            return new ReverseCollectionGenerator<IList<Part>>
            {
                CollectionGenerator = new CollectionGenerator<IList<Part>>
                {
                    ItemCountGenerator = new ConstantGenerator<int>(2),
                    ItemGenerator = new CollectionGenerator<Part>
                    {
                        ItemGenerator = new ObjectGenerator<Part>()
                            .WithProperty(
                                x => x.Duration,
                                property => property.WithGenerator(
                                    new DoublePowerGenerator
                                    {
                                        ValueGenerator = new ConstantGenerator<double>(4),
                                        PowerGenerator = new DoubleAddGenerator
                                        {
                                            ValueGenerator =
                                                new GeneratedCollectionItemIndexGenerator<IList<Part>>(),
                                            AdditiveGenerator = new ConstantGenerator<double>(3)
                                        }
                                    }))
                            .WithProperty(
                                x => x.NoteBasePattern,
                                property => property.WithGenerator(PartNoteBaseTimelineGenerator))
                            .WithProperty(
                                x => x.TrackPatterns,
                                trackPatternsProperty => trackPatternsProperty.WithGenerator(
                                    new ConditionalGenerator
                                    {
                                        ConditionGenerator = new ConditionalEqualGenerator
                                        {
                                            FirstGenerator = new GeneratedCollectionItemIndexGenerator<IList<Part>>(),
                                            SecondGenerator = new ConstantGenerator<int>(0)
                                        },
                                        TrueGenerator = new DictionaryGenerator<Track, IList<TimedEvent<Pattern>>>
                                        {
                                            KeyCollectionGenerator = new UniqueCollectionPickerGenerator<Track>
                                            {
                                                CollectionGenerator = new PropertyLinkGenerator<Song, IList<Track>>()
                                                    .FromProperty(x => x.Tracks),
                                                ItemCountGenerator = new IntGenerator(2, 4 + 1)
                                            },
                                            ValueGenerator = new SequentialTimelineGenerator<Pattern>
                                            {
                                                EventGenerator = new CollectionItemPickerGenerator<Pattern>
                                                {
                                                    CollectionGenerator = new UniqueCollectionPickerGenerator<Pattern>
                                                    {
                                                        CollectionGenerator =
                                                            new CollectionItemGenerator<IList<Pattern>>
                                                            {
                                                                CollectionGenerator =
                                                                    new PropertyLinkGenerator<Song,
                                                                            IList<IList<Pattern>>>()
                                                                        .FromProperty(x => x.RankedPatternTemplates),
                                                                IndexGenerator = new ConstantGenerator<int>(0)
                                                            },
                                                        ItemCountGenerator = new IntGenerator(2, 8 + 1)
                                                    }.Cache<Part, IList<Pattern>>()
                                                }
                                            }
                                        },
                                        FalseGenerator =
                                            new ConstantGenerator<IDictionary<Track, IList<TimedEvent<Pattern>>>>(null)
                                    }))
                            .WithProperty(
                                x => x.Parts,
                                partsProperty => partsProperty.WithGenerator(
                                    new ConditionalGenerator
                                    {
                                        ConditionGenerator = new ConditionalNotGenerator
                                        {
                                            ConditionGenerator = new ConditionalEqualGenerator
                                            {
                                                FirstGenerator =
                                                    new GeneratedCollectionItemIndexGenerator<IList<Part>>(),
                                                SecondGenerator = new ConstantGenerator<int>(0)
                                            }
                                        },
                                        TrueGenerator = new SequentialTimelineGenerator<Part>
                                        {
                                            EventGenerator = new CollectionItemPickerGenerator<Part>
                                            {
                                                CollectionGenerator = new CollectionItemGenerator<IList<Part>>
                                                {
                                                    CollectionGenerator =
                                                        new GeneratedCollectionGenerator<IList<Part>>(),
                                                    IndexGenerator = new DoubleAddGenerator
                                                    {
                                                        ValueGenerator =
                                                            new GeneratedCollectionItemIndexGenerator<IList<Part>>(),
                                                        AdditiveGenerator = new ConstantGenerator<int>(-1)
                                                    }
                                                }
                                            }
                                        },
                                        FalseGenerator = new ConstantGenerator<IList<TimedEvent<Part>>>(null)
                                    })),
                        ItemCountGenerator = TemplateCountGenerator
                    }
                }
            };
        }

        [Fact]
        public void Test()
        {
            var scaleGenerator = new ConstantGenerator<Scale>
            {
                Value = new Scale
                {
                    NoteOffsets = new List<int> {0, 2, 3, 5, 7, 8, 10},
                    RankedOffsetIndexes = new List<IList<int>>
                    {
                        new List<int> {0},
                        new List<int> {3, 4},
                        new List<int> {1, 2, 5, 6}
                    }
                }
            };
            var songGenerator = new ObjectGenerator<Song>()
                .WithProperty(
                    x => x.Scale,
                    property => property.WithGenerator(scaleGenerator))
                .WithProperty(
                    x => x.Tempo,
                    property => property.WithGenerator(
                        new RankedPositionGenerator
                        {
                            MaxRank = 5,
                            RankMultiplier = 0.95,
                            Period = 80,
                            Offset = 80,
                            Min = 80,
                            Max = 160
                        }))
                .WithProperty(
                    x => x.Tracks,
                    tracksProperty => tracksProperty
                        .WithGenerator(
                            new CollectionGenerator<Track>
                            {
                                ItemCountGenerator = new IntGenerator
                                {
                                    Min = 4,
                                    Max = 8 + 1
                                },
                                ItemGenerator = new ObjectGenerator<Track>()
                                    .WithProperty(
                                        x => x.Instrument,
                                        instrumentProperty => instrumentProperty
                                            .WithGenerator(
                                                new ObjectGenerator<Instrument>()
                                                    .WithProperty(
                                                        x => x.Code,
                                                        codeProperty =>
                                                            codeProperty.WithGenerator(new IntGenerator(0, 128)))))
                                    .WithProperty(
                                        x => x.NoteBase,
                                        property => property.WithGenerator(CreateTrackNoteBaseGenerator()))
                                    .WithProperty(
                                        x => x.MinOctave,
                                        property => property.WithGenerator(
                                            new DoubleAddGenerator
                                            {
                                                ValueGenerator = new IntPickerGenerator
                                                {
                                                    MinValueGenerator = new ConstantGenerator<int>(-2),
                                                    MaxValueGenerator = new ConstantGenerator<int>(0),
                                                    ProbabilityFunctionGenerator =
                                                        new ConstantGenerator<GeometricProbabilityFunction>(
                                                            new GeometricProbabilityFunction
                                                            {
                                                                MinProbability = 0,
                                                                MaxProbability = 1,
                                                                ProbabilityMultiplier = 0.5,
                                                                Offset = 0
                                                            })
                                                },
                                                AdditiveGenerator = new IntGenerator(-2, 2 + 1)
                                            }))
                                    .WithProperty(
                                        x => x.MaxOctave,
                                        property => property.WithGenerator(
                                                new DoubleAddGenerator
                                                {
                                                    ValueGenerator = new DoubleAddGenerator
                                                    {
                                                        ValueGenerator = new DoubleMaxGenerator
                                                        {
                                                            Value1Generator = new DoubleSubtractionGenerator
                                                            {
                                                                SubtrahendGenerator = new ConstantGenerator<int>(2),
                                                                MinuendGenerator =
                                                                    new PropertyLinkGenerator<Track, int>()
                                                                        .FromProperty(x => x.MinOctave)
                                                            },
                                                            Value2Generator = new ConstantGenerator<int>(2)
                                                        },
                                                        AdditiveGenerator = new IntPickerGenerator
                                                        {
                                                            MinValueGenerator = new ConstantGenerator<int>(0),
                                                            MaxValueGenerator = new DoubleMaxGenerator
                                                            {
                                                                Value1Generator = new DoubleSubtractionGenerator
                                                                {
                                                                    SubtrahendGenerator = new ConstantGenerator<int>(4),
                                                                    MinuendGenerator =
                                                                        new PropertyLinkGenerator<Track, int>()
                                                                            .FromProperty(x => x.MinOctave)
                                                                },
                                                                Value2Generator = new ConstantGenerator<int>(2)
                                                            },
                                                            ProbabilityFunctionGenerator =
                                                                new ConstantGenerator<GeometricProbabilityFunction>(
                                                                    new GeometricProbabilityFunction
                                                                    {
                                                                        MinProbability = 0,
                                                                        MaxProbability = 1,
                                                                        ProbabilityMultiplier = 0.5,
                                                                        Offset = 0
                                                                    })
                                                        }
                                                    },
                                                    AdditiveGenerator =
                                                        new PropertyLinkGenerator<Track, int>().FromProperty(
                                                            x => x.MinOctave)
                                                })
                                            .DependsOn(x => x.MinOctave))
                            }))
                .WithProperty(
                    x => x.Duration,
                    property => property.WithValue(256))
                .WithProperty(x => x.NoteBase, property => property.WithGenerator(CreateSongNoteBaseGenerator()))
                .WithProperty(
                    x => x.NoteBasePattern,
                    property => property.WithGenerator(EmptyNoteBaseTimelineGenerator))
                .WithProperty(
                    x => x.Parts,
                    partsProperty => partsProperty.WithGenerator(
                            new SequentialTimelineGenerator<Part>
                            {
                                EventGenerator = new CollectionItemPickerGenerator<Part>
                                {
                                    CollectionGenerator = new CollectionItemGenerator<IList<Part>>
                                    {
                                        CollectionGenerator = new PropertyLinkGenerator<Song, IList<IList<Part>>>()
                                            .FromProperty(x => x.RankedPartTemplates),
                                        IndexGenerator = new ConstantGenerator<int>(0)
                                    }
                                }
                            })
                        .DependsOn(x => x.RankedPartTemplates))
                .WithProperty(
                    x => x.RankedPatternTemplates,
                    property => property.WithGenerator(CreateRankedPatternTemplatesGenerator()))
                .WithProperty(
                    x => x.RankedPartTemplates,
                    property => property
                        .WithGenerator(CreateRankedPartTemplatesGenerator())
                        .DependsOn(x => x.RankedPatternTemplates));

            var song = songGenerator.Generate(new GenerationContext(new Random()));

            var midiMusic = MidiSongConverter.ConvertSong(song);
            using (var stream = new FileStream(
                "C:\\Users\\chech\\OneDrive\\Desktop\\songs\\song.mid",
                FileMode.Create,
                FileAccess.Write))
            {
                var midiWriter = new SmfWriter(stream);
                midiWriter.WriteMusic(midiMusic);
            }
        }
    }
}
