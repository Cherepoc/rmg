using System;
using System.Collections.Generic;
using RMG.Core.Generation;
using RMG.Core.Midi;
using RMG.Core.Music;
using Xunit;

namespace RMG.Tests
{
    public class TestClass
    {
        [Fact]
        public void Test()
        {
            var noteBasePattern = new NoteBasePattern
            {
                Duration = 0,
                Key = new[] {new TimedEvent<int> {Position = 0, Event = 0}},
                Octave = new[] {new TimedEvent<int> {Position = 0, Event = 0}},
                Volume = new[] {new TimedEvent<double> {Position = 0, Event = 1}}
            };

            var songGenerator = new ObjectGenerator<Song>()
                .WithPropertyGenerator(
                    x => x.Scale,
                    new ConstantGenerator<Scale>
                    {
                        Value = new Scale
                        {
                            RankedOffsets = new[]
                            {
                                new[] {0},
                                new[] {5, 7},
                                new[] {2, 3, 8, 10}
                            }
                        }
                    })
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
                    x => x.Part,
                    new ObjectGenerator<Part>()
                        .WithPropertyGenerator(
                            x => x.Duration,
                            new RankedPositionGenerator
                            {
                                RankMultiplier = 0.75,
                                MaxRank = 5,
                                Period = 80,
                                Offset = 80,
                                Min = 80,
                                Max = 160
                            })
                        .WithPropertyGenerator(
                            x => x.TrackPatterns,
                            new DictionaryGenerator<Track, IReadOnlyList<TimedEvent<Pattern>>>
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
                                    Duration = 160,
                                    Offset = 0,
                                    Scale = 4,
                                    MaxRank = 2,
                                    RankProbabilityFunction = new RankProbabilityFunction
                                    {
                                        Min = 0,
                                        Max = 1,
                                        Multiplier = 0.5
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
                                                Duration = 4,
                                                Offset = 0,
                                                Scale = 1,
                                                MaxRank = 4,
                                                RankProbabilityFunction = new RankProbabilityFunction
                                                {
                                                    Min = 0,
                                                    Max = 1,
                                                    Multiplier = 0.75
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
                                                            Min = 0.5,
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
                                                            RankMultiplier = 0.5
                                                        })
                                            })
                                        .WithPropertyGenerator(
                                            x => x.NoteBasePattern,
                                            new ConstantGenerator<NoteBasePattern>
                                            {
                                                Value = noteBasePattern
                                            })
                                }
                            })
                        .WithPropertyGenerator(
                            x => x.NoteBasePattern,
                            new ConstantGenerator<NoteBasePattern>
                            {
                                Value = noteBasePattern
                            }));

            var song = songGenerator.Generate(new GenerationContext(new Random()));

            var songMidiWriter = new SongMidiWriter();
            songMidiWriter.WriteMidi(song, "C:\\Users\\chech\\OneDrive\\Desktop\\songs\\song.mid");
        }
    }
}
