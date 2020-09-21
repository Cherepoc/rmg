namespace RMG.TestsF.Composition.NoteOffsetBasedPattern

open RMG.CoreF
open RMG.CoreF.Composition
open Xunit
open RMG.TestsF.Assertions

type Flatten() =
    let combine = Timeline.union

    [<Fact>]
    let empty () =
        let input = NoteOffsetBasedPattern.empty
        let expected = NoteOffsetBasedTimeline.empty

        let result =
            input
            |> NoteOffsetBasedPattern.flatten (1.0, combine)

        result |> should beEquivalentTo expected

    [<Fact>]
    let timeline () =
        let input: NoteOffsetBasedPattern<int> =
            { Duration = 1.0
              PatternTimeline =
                  seq {
                      { Position = 0.0
                        Value =
                            { Duration = 1.0
                              Timeline = seq { { Position = 0.0; Value = 1 } }
                              PatternTimeline = Seq.empty } }
                  }
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
                                                    Timeline = seq { { Position = 0.0; Value = 1.0 } }
                                                    PatternTimeline = Seq.empty }
                                              KeyOffset =
                                                  { Duration = 1.0
                                                    Timeline = seq { { Position = 0.0; Value = 0 } }
                                                    PatternTimeline = Seq.empty }
                                              OctaveOffset =
                                                  { Duration = 1.0
                                                    Timeline = seq { { Position = 0.0; Value = 0 } }
                                                    PatternTimeline = Seq.empty }
                                              ScaleOffset =
                                                  { Duration = 1.0
                                                    Timeline = seq { { Position = 0.0; Value = 0 } }
                                                    PatternTimeline = Seq.empty }
                                              Volume =
                                                  { Duration = 1.0
                                                    Timeline = seq { { Position = 0.0; Value = 1.0 } }
                                                    PatternTimeline = Seq.empty } } }
                                  }
                              PatternTimeline = Seq.empty } }
                  }
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
                                              Timeline = seq { { Position = 0.0; Value = 1 } }
                                              PatternTimeline = Seq.empty } }
                                  }
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
                                                                    Timeline = seq { { Position = 0.0; Value = 2.0 } }
                                                                    PatternTimeline = Seq.empty }
                                                              KeyOffset =
                                                                  { Duration = 1.0
                                                                    Timeline = seq { { Position = 0.0; Value = 2 } }
                                                                    PatternTimeline = Seq.empty }
                                                              OctaveOffset =
                                                                  { Duration = 1.0
                                                                    Timeline = seq { { Position = 0.0; Value = 2 } }
                                                                    PatternTimeline = Seq.empty }
                                                              ScaleOffset =
                                                                  { Duration = 1.0
                                                                    Timeline = seq { { Position = 0.0; Value = 2 } }
                                                                    PatternTimeline = Seq.empty }
                                                              Volume =
                                                                  { Duration = 1.0
                                                                    Timeline = seq { { Position = 0.0; Value = 2.0 } }
                                                                    PatternTimeline = Seq.empty } } }
                                                  }
                                              PatternTimeline = Seq.empty } }
                                  }
                              NoteOffsetBasedPatternTimeline = Seq.empty } }
                  } }

        let expected: NoteOffsetBasedTimeline<int> =
            { Timeline =
                  seq {
                      { Position = 0.0; Value = 1 }
                      { Position = 0.5; Value = 1 }
                  }
              NoteOffsetTimelineMap =
                  { Duration =
                        seq {
                            { Position = 0.5; Value = 2.0 }
                            { Position = 1.0; Value = 1.0 }
                        }
                    KeyOffset =
                        seq {
                            { Position = 0.5; Value = 2 }
                            { Position = 1.0; Value = 0 }
                        }
                    OctaveOffset =
                        seq {
                            { Position = 0.5; Value = 2 }
                            { Position = 1.0; Value = 0 }
                        }
                    ScaleOffset =
                        seq {
                            { Position = 0.5; Value = 2 }
                            { Position = 1.0; Value = 0 }
                        }
                    Volume =
                        seq {
                            { Position = 0.5; Value = 2.0 }
                            { Position = 1.0; Value = 1.0 }
                        } } }

        let result =
            input
            |> NoteOffsetBasedPattern.flatten (1.0, combine)

        result |> should beEquivalentTo expected
