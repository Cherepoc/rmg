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
                            }))
                .WithProperty(
                    x => x.Duration,
                    property => property.WithGenerator(
                        new RankedPositionGenerator
                        {
                            RankMultiplier = 1 - 0.125,
                            MaxRank = 6,
                            Period = 240,
                            Offset = 240,
                            Min = 240,
                            Max = 480
                        }))
                .WithProperty(x => x.Key, property => property.WithGenerator(KeyEventGenerator))
                .WithProperty(x => x.Octave, property => property.WithGenerator(OctaveEventGenerator))
                .WithProperty(x => x.Volume, property => property.WithValue(1))
                .WithProperty(
                    x => x.ScaleNoteOffset,
                    property => property.WithGenerator(
                        new ScaleNoteOffsetGenerator
                        {
                            RankProbabilityFunction = new RankProbabilityFunction
                            {
                                Max = 0.5,
                                Min = 0.25,
                                Multiplier = 0.5
                            }
                        }))
                .WithProperty(
                    x => x.NoteBasePattern,
                    property => property.WithGenerator(SongNoteBaseTimelineGenerator))
                .WithProperty(
                    x => x.Parts,
                    partsProperty => partsProperty.WithGenerator(
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
                                .WithProperty(
                                    x => x.Duration,
                                    property => property.WithGenerator(
                                        new RankedPositionGenerator
                                        {
                                            Min = 8,
                                            Max = 24,
                                            Offset = 8,
                                            Period = 16,
                                            MaxRank = 3,
                                            RankMultiplier = 0.5
                                        }))
                                .WithProperty(
                                    x => x.NoteBasePattern,
                                    property => property.WithGenerator(PartNoteBaseTimelineGenerator))
                                .WithProperty(
                                    x => x.TrackPatterns,
                                    trackPatternsProperty => trackPatternsProperty.WithGenerator(
                                        new DictionaryGenerator<Track, IList<TimedEvent<Pattern>>>
                                        {
                                            KeyCollectionGenerator = new LinkedEntityCollectionGenerator<Track>()
                                                .LinkEntityCollection(tracksLink => tracksLink.FromParentProperty<Song>(x => x.Tracks))
                                                .WithItemCountGenerator(new IntGenerator(2, 4+1)),
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
                                                    .WithProperty(
                                                        x => x.Duration,
                                                        property => property.WithValue(4))
                                                    .WithProperty(
                                                        x => x.NoteBasePattern,
                                                        property => property.WithGenerator(PatternNoteBaseTimelineGenerator))
                                                    .WithProperty(
                                                        x => x.Notes,
                                                        notesProperty => notesProperty.WithGenerator(
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
                                                                    .WithProperty(
                                                                        x => x.Duration,
                                                                        property => property.WithGenerator(
                                                                            new RankedPositionGenerator
                                                                            {
                                                                                Min = 0,
                                                                                Max = 1,
                                                                                Offset = 0,
                                                                                Period = 1,
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
                                                                                RankProbabilityFunction =
                                                                                    new RankProbabilityFunction
                                                                                    {
                                                                                        Max = 0.5,
                                                                                        Min = 0.25,
                                                                                        Multiplier = 0.5
                                                                                    }
                                                                            }))
                                                            }))
                                            }
                                        }))
                        }));

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
