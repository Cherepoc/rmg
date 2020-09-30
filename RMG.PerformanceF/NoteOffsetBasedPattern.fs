namespace RMG.PerformanceF

open BenchmarkDotNet.Diagnosers
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Jobs

open RMG.CoreF
open RMG.CoreF.Composition

type NoteOffsetBasedPatternPerfConfig() =
    inherit ManualConfig()

    do
        base.AddJob Job.RyuJitX64 |> ignore
        base.AddDiagnoser MemoryDiagnoser.Default
        |> ignore

[<Config(typeof<NoteOffsetBasedPatternPerfConfig>)>]
type NoteOffsetBasedPatternBenchmark() =
    let mutable input: NoteOffsetBasedPattern<int> = NoteOffsetBasedPattern.empty

    [<GlobalSetup>]
    member self.SetupData() =
        input <-
            { Duration = 1.0
              PatternTimeline =
                  seq {
                      { Position = 0.0
                        Value =
                            { Duration = 1.0
                              Timeline =
                                  seq { { Position = 0.0; Value = 1 } }
                                  |> Timeline.fromSequence
                              PatternTimeline = Timeline.empty } }
                  }
                  |> Timeline.fromSequence
              NoteOffsetPatternMapPatternTimeline =
                  seq {
                      { Position = 0.0
                        Value =
                            { Duration = 1.0
                              Timeline =
                                  seq {
                                      { Position = 0.0
                                        Value =
                                            { Duration =
                                                  { Duration = 1.0
                                                    Timeline =
                                                        seq { { Position = 0.0; Value = 1.0 } }
                                                        |> Timeline.fromSequence
                                                    PatternTimeline = Timeline.empty }
                                              KeyOffset =
                                                  { Duration = 1.0
                                                    Timeline =
                                                        seq { { Position = 0.0; Value = 0 } }
                                                        |> Timeline.fromSequence
                                                    PatternTimeline = Timeline.empty }
                                              OctaveOffset =
                                                  { Duration = 1.0
                                                    Timeline =
                                                        seq { { Position = 0.0; Value = 0 } }
                                                        |> Timeline.fromSequence
                                                    PatternTimeline = Timeline.empty }
                                              ScaleOffset =
                                                  { Duration = 1.0
                                                    Timeline =
                                                        seq { { Position = 0.0; Value = 0 } }
                                                        |> Timeline.fromSequence
                                                    PatternTimeline = Timeline.empty }
                                              Volume =
                                                  { Duration = 1.0
                                                    Timeline =
                                                        seq { { Position = 0.0; Value = 1.0 } }
                                                        |> Timeline.fromSequence
                                                    PatternTimeline = Timeline.empty } } }
                                  }
                                  |> Timeline.fromSequence
                              PatternTimeline = Timeline.empty } }
                  }
                  |> Timeline.fromSequence
              NoteOffsetBasedPatternTimeline =
                  seq {
                      { Position = 0.5
                        Value =
                            { Duration = 1.0
                              PatternTimeline =
                                  seq {
                                      { Position = 0.0
                                        Value =
                                            { Duration = 1.0
                                              Timeline =
                                                  seq { { Position = 0.0; Value = 1 } }
                                                  |> Timeline.fromSequence
                                              PatternTimeline = Timeline.empty } }
                                  }
                                  |> Timeline.fromSequence
                              NoteOffsetPatternMapPatternTimeline =
                                  seq {
                                      { Position = 0.0
                                        Value =
                                            { Duration = 1.0
                                              Timeline =
                                                  seq {
                                                      { Position = 0.0
                                                        Value =
                                                            { Duration =
                                                                  { Duration = 1.0
                                                                    Timeline =
                                                                        seq { { Position = 0.0; Value = 2.0 } }
                                                                        |> Timeline.fromSequence
                                                                    PatternTimeline = Timeline.empty }
                                                              KeyOffset =
                                                                  { Duration = 1.0
                                                                    Timeline =
                                                                        seq { { Position = 0.0; Value = 2 } }
                                                                        |> Timeline.fromSequence
                                                                    PatternTimeline = Timeline.empty }
                                                              OctaveOffset =
                                                                  { Duration = 1.0
                                                                    Timeline =
                                                                        seq { { Position = 0.0; Value = 2 } }
                                                                        |> Timeline.fromSequence
                                                                    PatternTimeline = Timeline.empty }
                                                              ScaleOffset =
                                                                  { Duration = 1.0
                                                                    Timeline =
                                                                        seq { { Position = 0.0; Value = 2 } }
                                                                        |> Timeline.fromSequence
                                                                    PatternTimeline = Timeline.empty }
                                                              Volume =
                                                                  { Duration = 1.0
                                                                    Timeline =
                                                                        seq { { Position = 0.0; Value = 2.0 } }
                                                                        |> Timeline.fromSequence
                                                                    PatternTimeline = Timeline.empty } } }
                                                  }
                                                  |> Timeline.fromSequence
                                              PatternTimeline = Timeline.empty } }
                                  }
                                  |> Timeline.fromSequence
                              NoteOffsetBasedPatternTimeline = Timeline.empty } }
                  }
                  |> Timeline.fromSequence }

    [<Benchmark>]
    member self.Flatten() =
        input
        |> NoteOffsetBasedPattern.flatten 1.0 Timeline.insertInto
