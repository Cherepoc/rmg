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

        private static readonly IntGenerator OctaveEventGenerator = new IntGenerator
        {
            Min = -2,
            Max = 2 + 1
        };

        private static readonly ScaleNoteOffsetGenerator ScaleNoteOffsetEventGenerator = new ScaleNoteOffsetGenerator
        {
            RankProbabilityFunction = new RankProbabilityFunction
            {
                Max = 0.5,
                Min = 0.25,
                Multiplier = 0.5
            }
        };

        private static readonly DoubleGenerator VolumeEventGenerator = new DoubleGenerator
        {
            Min = 0.9,
            Max = 1
        };

        private static TimedEventGenerator<T> GenerateSongNoteBaseTimeEventGenerator<T>(IGenerator eventGenerator)
        {
            return new TimedEventGenerator<T>
            {
                Offset = 0,
                Scale = 64,
                MaxRank = 3,
                RankProbabilityFunction = new RankProbabilityFunction
                {
                    Max = 1.0 / 8,
                    Min = 1.0 / 32,
                    Multiplier = 0.25
                },
                EventGenerator = eventGenerator
            };
        }

        private static readonly ObjectGenerator<NoteBasePattern> SongNoteBaseTimelineGenerator =
            new ObjectGenerator<NoteBasePattern>()
                .WithPropertyGenerator(
                    x => x.KeyTimeline,
                    GenerateSongNoteBaseTimeEventGenerator<int>(KeyEventGenerator))
                .WithPropertyGenerator(
                    x => x.OctaveTimeline,
                    GenerateSongNoteBaseTimeEventGenerator<int>(OctaveEventGenerator))
                .WithPropertyGenerator(
                    x => x.ScaleOffsetTimeline,
                    GenerateSongNoteBaseTimeEventGenerator<int[]>(ScaleNoteOffsetEventGenerator))
                .WithPropertyGenerator(
                    x => x.VolumeTimeline,
                    GenerateSongNoteBaseTimeEventGenerator<double>(VolumeEventGenerator)
                );

        private static TimedEventGenerator<T> GeneratePartNoteBaseTimeEventGenerator<T>(IGenerator eventGenerator)
        {
            return new TimedEventGenerator<T>
            {
                Offset = 0,
                Scale = 16,
                MaxRank = 3,
                RankProbabilityFunction = new RankProbabilityFunction
                {
                    Max = 1.0 / 16,
                    Min = 1.0 / 64,
                    Multiplier = 0.25
                },
                EventGenerator = eventGenerator
            };
        }

        private static readonly ObjectGenerator<NoteBasePattern> PartNoteBaseTimelineGenerator =
            new ObjectGenerator<NoteBasePattern>()
                .WithPropertyGenerator(
                    x => x.KeyTimeline,
                    GeneratePartNoteBaseTimeEventGenerator<int>(KeyEventGenerator))
                .WithPropertyGenerator(
                    x => x.OctaveTimeline,
                    GeneratePartNoteBaseTimeEventGenerator<int>(OctaveEventGenerator))
                .WithPropertyGenerator(
                    x => x.ScaleOffsetTimeline,
                    GeneratePartNoteBaseTimeEventGenerator<int[]>(ScaleNoteOffsetEventGenerator))
                .WithPropertyGenerator(
                    x => x.VolumeTimeline,
                    GeneratePartNoteBaseTimeEventGenerator<double>(VolumeEventGenerator)
                );

        private static TimedEventGenerator<T> GeneratePatternNoteBaseTimeEventGenerator<T>(IGenerator eventGenerator)
        {
            return new TimedEventGenerator<T>
            {
                Offset = 0,
                Scale = 4,
                MaxRank = 3,
                RankProbabilityFunction = new RankProbabilityFunction
                {
                    Max = 1.0 / 32,
                    Min = 1.0 / 128,
                    Multiplier = 0.25
                },
                EventGenerator = eventGenerator
            };
        }

        private static readonly ObjectGenerator<NoteBasePattern> PatternNoteBaseTimelineGenerator =
            new ObjectGenerator<NoteBasePattern>()
                .WithPropertyGenerator(
                    x => x.KeyTimeline,
                    GeneratePatternNoteBaseTimeEventGenerator<int>(
                        new ConstantGenerator<int>()
                        {
                            Value = 0
                        }))
                .WithPropertyGenerator(
                    x => x.OctaveTimeline,
                    GeneratePatternNoteBaseTimeEventGenerator<int>(OctaveEventGenerator))
                .WithPropertyGenerator(
                    x => x.ScaleOffsetTimeline,
                    GeneratePatternNoteBaseTimeEventGenerator<int[]>(ScaleNoteOffsetEventGenerator))
                .WithPropertyGenerator(
                    x => x.VolumeTimeline,
                    GeneratePatternNoteBaseTimeEventGenerator<double>(VolumeEventGenerator)
                );

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
            var songGenerator = new ObjectGenerator<Song>();

            songGenerator.WithPropertyGenerator(
                x => x.Scale,
                scaleGenerator);

            songGenerator
                .WithPropertyGenerator(
                    x => x.Tempo,
                    new RankedPositionGenerator
                    {
                        MaxRank = 5,
                        RankMultiplier = 0.95,
                        Period = 80,
                        Offset = 80,
                        Min = 80,
                        Max = 160
                    })
                .WithPropertyGenerator(
                    x => x.Tracks,
                    new CollectionGenerator<Track>
                    {
                        ItemCountGenerator = new IntGenerator
                        {
                            Min = 4,
                            Max = 8 + 1
                        },
                        ItemGenerator = new ObjectGenerator<Track>()
                            .WithPropertyGenerator(
                                x => x.Instrument,
                                new ObjectGenerator<Instrument>()
                                    .WithPropertyGenerator(
                                        x => x.Code,
                                        new IntGenerator
                                        {
                                            Min = 0,
                                            Max = 128
                                        }))
                    })
                .WithPropertyGenerator(
                    x => x.Duration,
                    new RankedPositionGenerator
                    {
                        RankMultiplier = 1 - 0.125,
                        MaxRank = 6,
                        Period = 240,
                        Offset = 240,
                        Min = 240,
                        Max = 480
                    })
                .WithPropertyGenerator(x => x.Key, KeyEventGenerator)
                .WithPropertyGenerator(x => x.Octave, OctaveEventGenerator)
                .WithPropertyGenerator(x => x.Volume, new ConstantGenerator<double>(1))
                .WithPropertyGenerator(
                    x => x.ScaleNoteOffset,
                    new ScaleNoteOffsetGenerator
                    {
                        RankProbabilityFunction = new RankProbabilityFunction
                        {
                            Max = 0.5,
                            Min = 0.25,
                            Multiplier = 0.5
                        }
                    })
                .WithPropertyGenerator(x => x.NoteBasePattern, SongNoteBaseTimelineGenerator)
                .WithPropertyGenerator(
                    x => x.Parts,
                    new TimedEventGenerator<Part>
                    {
                        Offset = 0,
                        Scale = 16,
                        MaxRank = 2,
                        RankProbabilityFunction = new RankProbabilityFunction
                        {
                            Min = 0,
                            Max = 1,
                            Multiplier = 0.25
                        },
                        EventGenerator = new ObjectGenerator<Part>()
                            .WithPropertyGenerator(
                                x => x.Duration,
                                new RankedPositionGenerator
                                {
                                    Min = 8,
                                    Max = 24,
                                    Offset = 8,
                                    Period = 16,
                                    MaxRank = 3,
                                    RankMultiplier = 0.5
                                })
                            .WithPropertyGenerator(
                                x => x.NoteBasePattern,
                                PartNoteBaseTimelineGenerator
                            )
                            .WithPropertyGenerator(
                                x => x.TrackPatterns,
                                new DictionaryGenerator<Track, IList<TimedEvent<Pattern>>>
                                {
                                    KeyCollectionGenerator = new LinkedEntityCollectionGenerator<Song, Track>
                                    {
                                        ItemCountGenerator = new IntGenerator
                                        {
                                            Min = 2,
                                            Max = 4 + 1
                                        },
                                        LinkedCollectionAccessor = x => x.Tracks
                                    },
                                    ValueGenerator = new TimedEventGenerator<Pattern>
                                    {
                                        Offset = 0,
                                        Scale = 4,
                                        MaxRank = 2,
                                        RankProbabilityFunction = new RankProbabilityFunction
                                        {
                                            Min = 0,
                                            Max = 1,
                                            Multiplier = 0.25
                                        },
                                        EventGenerator = new ObjectGenerator<Pattern>()
                                            .WithPropertyGenerator(
                                                x => x.Duration,
                                                new ConstantGenerator<double>
                                                {
                                                    Value = 4
                                                })
                                            .WithPropertyGenerator(
                                                x => x.Notes,
                                                new TimedEventGenerator<Note>
                                                {
                                                    Offset = 0,
                                                    Scale = 1,
                                                    MaxRank = 4,
                                                    RankProbabilityFunction = new RankProbabilityFunction
                                                    {
                                                        Min = 0,
                                                        Max = 1,
                                                        Multiplier = 0.25
                                                    },
                                                    EventGenerator = new ObjectGenerator<Note>()
                                                        .WithPropertyGenerator(
                                                            x => x.Duration,
                                                            new RankedPositionGenerator
                                                            {
                                                                Min = 0,
                                                                Max = 1,
                                                                Offset = 0,
                                                                Period = 1,
                                                                MaxRank = 4,
                                                                RankMultiplier = 0.5
                                                            })
                                                        .WithPropertyGenerator(
                                                            x => x.Octave,
                                                            new ConstantGenerator<int>
                                                            {
                                                                Value = 0
                                                            })
                                                        .WithPropertyGenerator(
                                                            x => x.Volume,
                                                            new RankedPositionGenerator
                                                            {
                                                                Min = 0.75,
                                                                Max = 1,
                                                                Offset = 0.5,
                                                                Period = 1,
                                                                MaxRank = 5,
                                                                RankMultiplier = 0.75
                                                            })
                                                        .WithPropertyGenerator(
                                                            x => x.ScaleOffset,
                                                            new ScaleNoteOffsetGenerator
                                                            {
                                                                RankProbabilityFunction = new RankProbabilityFunction
                                                                {
                                                                    Max = 0.5,
                                                                    Min = 0.25,
                                                                    Multiplier = 0.5
                                                                }
                                                            })
                                                })
                                            .WithPropertyGenerator(
                                                x => x.NoteBasePattern,
                                                PatternNoteBaseTimelineGenerator
                                            )
                                    }
                                })
                    });

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
