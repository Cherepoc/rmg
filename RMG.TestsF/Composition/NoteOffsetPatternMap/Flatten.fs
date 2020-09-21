namespace RMG.TestsF.Composition.NoteOffsetPatternMap

open RMG.CoreF
open RMG.CoreF.Composition
open Xunit
open RMG.TestsF.Assertions

type FlattenAggregate() =

    [<Fact>]
    let empty () =
        let input = NoteOffsetPatternMap.empty
        let expected = NoteOffsetTimelineMap.empty

        let result =
            input |> NoteOffsetPatternMap.flatten 1.0

        result |> should beEquivalentTo expected

    [<Fact>]
    let recursive () =
        let input: NoteOffsetPatternMap =
            { Duration =
                  { Duration = 1.0
                    Timeline = seq {
                        { Position = 0.0; Value = 1.0 }
                    }
                    PatternTimeline =
                        seq {
                            { Position = 0.5
                              Value =
                                  { Duration = 1.0
                                    Timeline = seq { { Position = 0.0; Value = 2.0 } }
                                    PatternTimeline = Seq.empty } }
                        } }
              KeyOffset =
                  { Duration = 1.0
                    Timeline = seq {
                        { Position = 0.0; Value = 1 }
                        { Position = 1.0; Value = 0 }
                    }
                    PatternTimeline =
                        seq {
                            { Position = 0.5
                              Value =
                                  { Duration = 1.0
                                    Timeline = seq { { Position = 0.0; Value = 2 } }
                                    PatternTimeline = Seq.empty } }
                        } }
              OctaveOffset =
                  { Duration = 1.0
                    Timeline = seq {
                        { Position = 0.0; Value = 1 }
                        { Position = 1.0; Value = 0 }
                    }
                    PatternTimeline =
                        seq {
                            { Position = 0.5
                              Value =
                                  { Duration = 1.0
                                    Timeline = seq { { Position = 0.0; Value = 2 } }
                                    PatternTimeline = Seq.empty } }
                        } }
              ScaleOffset =
                  { Duration = 1.0
                    Timeline = seq {
                        { Position = 0.0; Value = 1 }
                        { Position = 1.0; Value = 0 }
                    }
                    PatternTimeline =
                        seq {
                            { Position = 0.5
                              Value =
                                  { Duration = 1.0
                                    Timeline = seq { { Position = 0.0; Value = 2 } }
                                    PatternTimeline = Seq.empty } }
                        } }
              Volume =
                  { Duration = 1.0
                    Timeline = seq {
                        { Position = 0.0; Value = 1.0 }
                    }
                    PatternTimeline =
                        seq {
                            { Position = 0.5
                              Value =
                                  { Duration = 1.0
                                    Timeline = seq { { Position = 0.0; Value = 2.0 } }
                                    PatternTimeline = Seq.empty } }
                        } } }

        let expected: NoteOffsetTimelineMap =
            { Duration =
                  seq {
                      { Position = 0.5; Value = 2.0 }
                      { Position = 1.0; Value = 1.0 }
                  }
              KeyOffset =
                  seq {
                      { Position = 0.0; Value = 1 }
                      { Position = 0.5; Value = 3 }
                      { Position = 1.0; Value = 0 }
                  }
              OctaveOffset =
                  seq {
                      { Position = 0.0; Value = 1 }
                      { Position = 0.5; Value = 3 }
                      { Position = 1.0; Value = 0 }
                  }
              ScaleOffset =
                  seq {
                      { Position = 0.0; Value = 1 }
                      { Position = 0.5; Value = 3 }
                      { Position = 1.0; Value = 0 }
                  }
              Volume =
                  seq {
                      { Position = 0.5; Value = 2.0 }
                      { Position = 1.0; Value = 1.0 }
                  } }

        let result =
            input |> NoteOffsetPatternMap.flatten 1.0

        result |> should beEquivalentTo expected
