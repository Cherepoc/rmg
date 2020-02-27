using System;
using System.Collections.Generic;
using System.IO;
using Commons.Music.Midi;
using RMG.Core.Generation;
using RMG.Core.Generation.CollectionGenerators;
using RMG.Core.Generation.ContextGenerators;
using RMG.Core.Generation.LogicGenerators;
using RMG.Core.Generation.MathGenerators;
using RMG.Core.Generation.ObjectGenerators;
using RMG.Core.Generation.RandomGenerators;
using RMG.Core.Midi;
using RMG.Core.Music;
using RMG.Core.ProbabilityCalculation;
using Xunit;

namespace RMG.Tests
{
    public class TestClass
    {
        private static readonly IntGenerator KeyEventGenerator = new IntGenerator(-6 + 1, 6 + 1);

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
            ProbabilityFunctionGenerator =
                new ConstantGenerator<IIntProbabilityFunction>(new GeometricProbabilityFunction(0, 0.5, 0.5))
        };

        private static readonly DoubleGenerator VolumeEventGenerator = new DoubleGenerator(0.9, 1);

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

        private static IGenerator<IReadOnlyList<TimedEvent<T>>> GeneratePartNoteBaseTimeEventGenerator
            <T>(IGenerator<T> eventGenerator)
        {
            return new RhythmTimelineGenerator<T>
            {
                OffsetGenerator = new ConstantGenerator<double>(0),
                PeriodGenerator = new DoublePowerGenerator
                {
                    Value1Generator = new ConstantGenerator<double>(4),
                    Value2Generator = new DoubleAdditionGenerator
                    {
                        Value1Generator =
                            new ContextCollectionItemIndexGenerator<IReadOnlyList<Part>>().ToDouble(),
                        Value2Generator = new ConstantGenerator<double>(3)
                    }
                },
                MaxPowerGenerator = new ConstantGenerator<int>(2),
                ProbabilityFunctionGenerator =
                    new ConstantGenerator<GeometricProbabilityFunction>(
                        new GeometricProbabilityFunction
                        {
                            MinProbability = 0,
                            MaxProbability = 1.0 / 2,
                            ProbabilityMultiplier = 1.0 / 2
                        }),
                EventGenerator = eventGenerator
            }.ToList();
        }

        private static readonly ObjectGenerator<NoteBasePattern> PartNoteBaseTimelineGenerator =
            new ObjectGenerator<NoteBasePattern>()
                .WithProperty(
                    x => x.KeyTimeline,
                    property => property
                        .WithGenerator(GeneratePartNoteBaseTimeEventGenerator(KeyEventGenerator)))
                .WithProperty(
                    x => x.OctaveTimeline,
                    property => property
                        .WithGenerator(GeneratePartNoteBaseTimeEventGenerator(OctaveEventGenerator)))
                .WithProperty(
                    x => x.ScaleOffsetTimeline,
                    property => property
                        .WithGenerator(GeneratePartNoteBaseTimeEventGenerator(ScaleNoteOffsetEventGenerator)))
                .WithProperty(
                    x => x.VolumeTimeline,
                    property => property
                        .WithGenerator(GeneratePartNoteBaseTimeEventGenerator(VolumeEventGenerator)));

        private static readonly IntGenerator TemplateCountGenerator = new IntGenerator(8, 16 + 1);

        private static IGenerator<IReadOnlyList<TimedEvent<Note>>> CreateNoteGenerator()
        {
            return new RhythmTimelineGenerator<Note>
            {
                OffsetGenerator = new BinaryTreePickerGenerator
                {
                    MinValueGenerator = new ConstantGenerator<double>(0),
                    MaxValueGenerator = new ConstantGenerator<double>(4),
                    MaxPowerGenerator = new ConstantGenerator<int>(4),
                    OffsetGenerator = new ConstantGenerator<double>(2),
                    PeriodGenerator = new ConstantGenerator<double>(4),
                    ProbabilityFunctionGenerator =
                        new ConstantGenerator<IIntProbabilityFunction>(new GeometricProbabilityFunction(0.5))
                },
                PeriodGenerator = new DoubleDivisionGenerator
                {
                    Value1Generator = new RhythmPeriodGenerator
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
                    Value2Generator = new DoublePowerGenerator
                    {
                        Value1Generator = new ConstantGenerator<double>(2),
                        Value2Generator = new IntPickerGenerator
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
                        }.ToDouble()
                    }
                },
                MaxPowerGenerator = new ConstantGenerator<int>(4),
                ProbabilityFunctionGenerator =
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
                            new BinaryTreePickerGenerator
                            {
                                MinValueGenerator = new ConstantGenerator<double>(0),
                                MaxValueGenerator = new ConstantGenerator<double>(2),
                                MaxPowerGenerator = new ConstantGenerator<int>(4),
                                OffsetGenerator = new ConstantGenerator<double>(1),
                                PeriodGenerator = new ConstantGenerator<double>(2),
                                ProbabilityFunctionGenerator =
                                    new ConstantGenerator<IIntProbabilityFunction>(
                                        new GeometricProbabilityFunction(0.5))
                            }))
                    .WithProperty(
                        x => x.Octave,
                        property => property.WithValue(0))
                    .WithProperty(
                        x => x.Volume,
                        property => property.WithGenerator(
                            new BinaryTreePickerGenerator
                            {
                                MinValueGenerator = new ConstantGenerator<double>(0.75),
                                MaxValueGenerator = new ConstantGenerator<double>(1),
                                MaxPowerGenerator = new ConstantGenerator<int>(5),
                                OffsetGenerator = new ConstantGenerator<double>(0.5),
                                PeriodGenerator = new ConstantGenerator<double>(1),
                                ProbabilityFunctionGenerator =
                                    new ConstantGenerator<IIntProbabilityFunction>(
                                        new GeometricProbabilityFunction(0.75))
                            }))
                    .WithProperty(
                        x => x.ScaleOffset,
                        property => property.WithGenerator(
                            new ScaleNoteOffsetGenerator
                            {
                                ProbabilityFunctionGenerator =
                                    new ConstantGenerator<IIntProbabilityFunction>(
                                        new GeometricProbabilityFunction(0, 0.5, 1))
                            }))
            }.ToList();
        }

        private static IGenerator<IReadOnlyList<IReadOnlyList<Pattern>>> CreateRankedPatternTemplatesGenerator()
        {
            return new ReverseCollectionGenerator<IReadOnlyList<Pattern>>
            {
                CollectionGenerator = new RandomCollectionGenerator<IReadOnlyList<Pattern>>
                {
                    ItemCountGenerator = new ConstantGenerator<int>(2),
                    ItemGenerator = new RandomCollectionGenerator<Pattern>
                    {
                        ItemGenerator = new ObjectGenerator<Pattern>()
                            .WithProperty(
                                x => x.Duration,
                                property => property.WithGenerator(
                                    new DoublePowerGenerator
                                    {
                                        Value1Generator = new ConstantGenerator<double>(2),
                                        Value2Generator = new DoublePowerGenerator
                                        {
                                            Value1Generator = new IntGenerator(2, 4 + 1).ToDouble(),
                                            Value2Generator = new DoubleMultiplicationGenerator
                                            {
                                                Value1Generator =
                                                    new ContextCollectionItemIndexGenerator<IReadOnlyList<Pattern>>()
                                                        .ToDouble(),
                                                Value2Generator = new ConstantGenerator<double>(2)
                                            }
                                        }
                                    }))
                            .WithProperty(
                                x => x.NoteBasePattern,
                                property => property.WithGenerator(EmptyNoteBaseTimelineGenerator))
                            .WithProperty(
                                x => x.Notes,
                                notesProperty => notesProperty.WithGenerator(
                                    new BranchGenerator<IReadOnlyList<TimedEvent<Note>>>
                                    {
                                        ConditionGenerator = new EqualGenerator<int, int>
                                        {
                                            Value1Generator =
                                                new ContextCollectionItemIndexGenerator<IReadOnlyList<Pattern>>(),
                                            Value2Generator = new ConstantGenerator<int>(0)
                                        },
                                        ThenGenerator = CreateNoteGenerator(),
                                        ElseGenerator = new ConstantGenerator<IReadOnlyList<TimedEvent<Note>>>(null)
                                    }))
                            .WithProperty(
                                x => x.Patterns,
                                patternsProperty => patternsProperty.WithGenerator(
                                    new BranchGenerator<IReadOnlyList<TimedEvent<Pattern>>>
                                    {
                                        ConditionGenerator = new NotGenerator
                                        {
                                            ConditionGenerator = new EqualGenerator<int, int>
                                            {
                                                Value1Generator =
                                                    new ContextCollectionItemIndexGenerator<IReadOnlyList<Pattern>>(),
                                                Value2Generator = new ConstantGenerator<int>(0)
                                            }
                                        },
                                        ThenGenerator = new SequentialTimelineGenerator<Pattern>
                                        {
                                            EventGenerator = new CollectionItemPickerGenerator<Pattern>
                                            {
                                                CollectionGenerator = new UniqueCollectionPickerGenerator<Pattern>
                                                {
                                                    CollectionGenerator =
                                                        new CollectionItemAccessor<IEnumerable<Pattern>>
                                                        {
                                                            CollectionGenerator =
                                                                new ContextEntityGenerator<
                                                                    IEnumerable<IEnumerable<Pattern>>>(),
                                                            IndexGenerator = new IntAdditionGenerator
                                                            {
                                                                Value1Generator =
                                                                    new ContextCollectionItemIndexGenerator<
                                                                        IReadOnlyList<Pattern>>(),
                                                                Value2Generator = new ConstantGenerator<int>(-1)
                                                            }
                                                        },
                                                    ItemCountGenerator = new IntGenerator(1, 4 + 1)
                                                }.Cache<IEnumerable<Pattern>, Pattern>()
                                            }
                                        }.ToList(),
                                        ElseGenerator = new ConstantGenerator<IReadOnlyList<TimedEvent<Pattern>>>(null)
                                    })),
                        ItemCountGenerator = TemplateCountGenerator
                    }.ToList()
                }
            }.ToList();
        }

        private static IGenerator<IReadOnlyList<IReadOnlyList<Part>>> CreateRankedPartTemplatesGenerator()
        {
            var partCollectionIndexGenerator = new ContextCollectionItemIndexGenerator<IReadOnlyList<Part>>();
            var isFirstPartCollectionGenerator = new EqualGenerator<int, int>
            {
                Value1Generator = partCollectionIndexGenerator,
                Value2Generator = new ConstantGenerator<int>(0)
            };
            return new ReverseCollectionGenerator<IReadOnlyList<Part>>
            {
                CollectionGenerator = new RandomCollectionGenerator<IReadOnlyList<Part>>
                {
                    ItemCountGenerator = new ConstantGenerator<int>(2),
                    ItemGenerator = new RandomCollectionGenerator<Part>
                    {
                        ItemGenerator = new ObjectGenerator<Part>()
                            .WithProperty(
                                x => x.Duration,
                                property => property.WithGenerator(
                                    new DoublePowerGenerator
                                    {
                                        Value1Generator = new ConstantGenerator<double>(4),
                                        Value2Generator = new DoubleAdditionGenerator
                                        {
                                            Value1Generator = partCollectionIndexGenerator.ToDouble(),
                                            Value2Generator = new ConstantGenerator<double>(3)
                                        }
                                    }))
                            .WithProperty(
                                x => x.NoteBasePattern,
                                property => property.WithGenerator(PartNoteBaseTimelineGenerator))
                            .WithProperty(
                                x => x.TrackPatterns,
                                trackPatternsProperty => trackPatternsProperty.WithGenerator(
                                    new BranchGenerator<IReadOnlyDictionary<Track, IReadOnlyList<TimedEvent<Pattern>>>
                                    >
                                    {
                                        ConditionGenerator = isFirstPartCollectionGenerator,
                                        ThenGenerator =
                                            new DictionaryGenerator<Track, IReadOnlyList<TimedEvent<Pattern>>>
                                            {
                                                KeyCollectionGenerator = new UniqueCollectionPickerGenerator<Track>
                                                {
                                                    CollectionGenerator =
                                                        new ContextEntityGenerator<Song>().Property(x => x.Tracks),
                                                    ItemCountGenerator = new IntGenerator(2, 4 + 1)
                                                },
                                                ValueGenerator = new SequentialTimelineGenerator<Pattern>
                                                {
                                                    EventGenerator = new CollectionItemPickerGenerator<Pattern>
                                                    {
                                                        CollectionGenerator =
                                                            new UniqueCollectionPickerGenerator<Pattern>
                                                            {
                                                                CollectionGenerator =
                                                                    new CollectionItemAccessor<IEnumerable<Pattern>>
                                                                    {
                                                                        CollectionGenerator =
                                                                            new ContextEntityGenerator<Song>().Property(
                                                                                x => x.RankedPatternTemplates),
                                                                        IndexGenerator = new ConstantGenerator<int>(0)
                                                                    },
                                                                ItemCountGenerator = new IntGenerator(2, 8 + 1)
                                                            }.Cache<IEnumerable<Pattern>, Song>()
                                                    }
                                                }.ToList()
                                            },
                                        ElseGenerator =
                                            new ConstantGenerator<IReadOnlyDictionary<Track,
                                                IReadOnlyList<TimedEvent<Pattern>>>>()
                                    }))
                            .WithProperty(
                                x => x.Parts,
                                partsProperty => partsProperty.WithGenerator(
                                    new BranchGenerator<IReadOnlyList<TimedEvent<Part>>>
                                    {
                                        ConditionGenerator = new NotGenerator
                                        {
                                            ConditionGenerator = isFirstPartCollectionGenerator
                                        },
                                        ThenGenerator = new SequentialTimelineGenerator<Part>
                                        {
                                            EventGenerator = new CollectionItemPickerGenerator<Part>
                                            {
                                                CollectionGenerator = new CollectionItemAccessor<IEnumerable<Part>>
                                                {
                                                    CollectionGenerator =
                                                        new ContextEntityGenerator<IEnumerable<IEnumerable<Part>>>(),
                                                    IndexGenerator = new IntSubtractionGenerator
                                                    {
                                                        Value1Generator = partCollectionIndexGenerator,
                                                        Value2Generator = new ConstantGenerator<int>(1)
                                                    }
                                                }
                                            }
                                        }.ToList(),
                                        ElseGenerator = new ConstantGenerator<IReadOnlyList<TimedEvent<Part>>>()
                                    })),
                        ItemCountGenerator = TemplateCountGenerator
                    }.ToList()
                }
            }.ToList();
        }

        [Fact]
        public void Test()
        {
            var scaleGenerator = new ConstantGenerator<Scale>
            {
                Value = new Scale
                {
                    NoteOffsets = new List<int> {0, 2, 3, 5, 7, 8, 10},
                    RankedOffsetIndexes = new List<List<int>>
                    {
                        new List<int> {0},
                        new List<int> {3, 4},
                        new List<int> {1, 2, 5, 6}
                    }
                }
            };
            var trackMinOctaveGenerator = new ContextEntityGenerator<Track>().Property(x => x.MinOctave);
            var songGenerator = new ObjectGenerator<Song>()
                .WithProperty(
                    x => x.Scale,
                    property => property.WithGenerator(scaleGenerator))
                .WithProperty(
                    x => x.Tempo,
                    property => property.WithGenerator(
                        new BinaryTreePickerGenerator
                        {
                            MinValueGenerator = new ConstantGenerator<double>(80),
                            MaxValueGenerator = new ConstantGenerator<double>(160),
                            MaxPowerGenerator = new ConstantGenerator<int>(5),
                            OffsetGenerator = new ConstantGenerator<double>(80),
                            PeriodGenerator = new ConstantGenerator<double>(80),
                            ProbabilityFunctionGenerator =
                                new ConstantGenerator<IIntProbabilityFunction>(
                                    new GeometricProbabilityFunction(0.95))
                        }))
                .WithProperty(
                    x => x.Tracks,
                    tracksProperty => tracksProperty
                        .WithGenerator(
                            new RandomCollectionGenerator<Track>
                            {
                                ItemCountGenerator = new IntGenerator(4, 8 + 1),
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
                                            new IntAdditionGenerator
                                            {
                                                Value1Generator = new IntPickerGenerator
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
                                                Value2Generator = new IntGenerator(-2, 2 + 1)
                                            }))
                                    .WithProperty(
                                        x => x.MaxOctave,
                                        property => property.WithGenerator(
                                                new IntAdditionGenerator
                                                {
                                                    Value1Generator = new IntAdditionGenerator
                                                    {
                                                        Value1Generator = new IntMaxGenerator
                                                        {
                                                            CollectionGenerator = new CollectionGenerator<int>(
                                                                new IntSubtractionGenerator
                                                                {
                                                                    Value1Generator = new ConstantGenerator<int>(2),
                                                                    Value2Generator = trackMinOctaveGenerator
                                                                },
                                                                new ConstantGenerator<int>(2)
                                                            )
                                                        },
                                                        Value2Generator = new IntPickerGenerator
                                                        {
                                                            MinValueGenerator = new ConstantGenerator<int>(0),
                                                            MaxValueGenerator = new IntMaxGenerator
                                                            {
                                                                CollectionGenerator = new CollectionGenerator<int>(
                                                                    new IntSubtractionGenerator
                                                                    {
                                                                        Value1Generator = new ConstantGenerator<int>(4),
                                                                        Value2Generator = trackMinOctaveGenerator
                                                                    },
                                                                    new ConstantGenerator<int>(2)
                                                                )
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
                                                    Value2Generator =
                                                        new ContextEntityGenerator<Track>().Property(x => x.MinOctave)
                                                })
                                            .DependsOn(x => x.MinOctave))
                            }.ToList()))
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
                                    CollectionGenerator = new CollectionItemAccessor<IEnumerable<Part>>
                                    {
                                        CollectionGenerator =
                                            new ContextEntityGenerator<Song>().Property(x => x.RankedPartTemplates),
                                        IndexGenerator = new ConstantGenerator<int>(0)
                                    }
                                }
                            }.ToList())
                        .DependsOn(x => x.RankedPartTemplates))
                .WithProperty(
                    x => x.RankedPatternTemplates,
                    property => property.WithGenerator(CreateRankedPatternTemplatesGenerator()))
                .WithProperty(
                    x => x.RankedPartTemplates,
                    property => property
                        .WithGenerator(CreateRankedPartTemplatesGenerator())
                        .DependsOn(x => x.RankedPatternTemplates));

            //var song = songGenerator.Generate(new GenerationContext(new Random()));

            //var midiMusic = MidiSongConverter.ConvertSong(song);
            using (var stream = new FileStream(
                "C:\\Projects\\RMG\\songs\\song1.mid",
                FileMode.Create,
                FileAccess.Write))
            {
                var midiWriter = new SmfWriter(stream);
                var track = new Track
                {
                    Instrument = new Instrument()
                    {
                        Code = 0
                    },
                    MinOctave = -2,
                    MaxOctave = 2,
                    NoteBase = new NoteBase()
                    {
                        Volume = 1,
                        ScaleOffset = new int[] {0, 0, 0}
                    }
                };
                midiWriter.WriteMusic(MidiSongConverter.ConvertSong(new Song
                {
                    Duration = 4,
                    Scale = scaleGenerator.Generate(new GenerationContext(new Random())),
                    Tempo = 120,
                    Tracks = new Track[]
                    {
                        track
                    },
                    NoteBase = new NoteBase()
                    {
                        Volume = 1,
                        ScaleOffset = new int[]{0, 0, 0}
                    },
                    Parts = new []
                    {
                        new TimedEvent<Part>(0, new Part
                        {
                            Duration = 4,
                            NoteBasePattern = new NoteBasePattern
                            {
                                Duration = 0,
                                KeyTimeline = new []{new TimedEvent<int>(0, 0)},
                                OctaveTimeline = new []{new TimedEvent<int>(0, 0)},
                                VolumeTimeline = new[]{new TimedEvent<double>(0, 1), },
                                ScaleOffsetTimeline = new[]{new TimedEvent<int[]>(0, new int[]{0, 0, 0})}
                            },
                            TrackPatterns = new Dictionary<Track, IReadOnlyList<TimedEvent<Pattern>>>()
                            {
                                [track] = new[]{new TimedEvent<Pattern>(0, new Pattern()
                                {
                                    Duration = 4,
                                    NoteBasePattern = new NoteBasePattern
                                    {
                                        Duration = 0,
                                        KeyTimeline = new []{new TimedEvent<int>(0, 0)},
                                        OctaveTimeline = new []{new TimedEvent<int>(0, 0)},
                                        VolumeTimeline = new[]{new TimedEvent<double>(0, 1), },
                                        ScaleOffsetTimeline = new[]{new TimedEvent<int[]>(0, new int[]{0, 0, 0})}
                                    },
                                    Notes = new[]{new TimedEvent<Note>(0, new Note
                                    {
                                         Duration = 4,
                                         Octave = 0,
                                         Volume = 1,
                                         ScaleOffset = new int[]{0, 0, 0}
                                    }), }
                                }), }
                            }
                        }),
                    },NoteBasePattern = new NoteBasePattern
                    {
                        Duration = 0,
                        KeyTimeline = new []{new TimedEvent<int>(0, 0)},
                        OctaveTimeline = new []{new TimedEvent<int>(0, 0)},
                        VolumeTimeline = new[]{new TimedEvent<double>(0, 1), },
                        ScaleOffsetTimeline = new[]{new TimedEvent<int[]>(0, new int[]{0, 0, 0})}
                    }
                }));
            }
        }
    }
}
