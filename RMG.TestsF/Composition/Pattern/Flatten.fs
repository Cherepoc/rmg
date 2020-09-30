namespace RMG.TestsF.Composition.Pattern

open RMG.CoreF
open RMG.CoreF.Composition
open Xunit
open RMG.TestsF.Assertions

type Flatten() =
    let combineInto = Timeline.insertInto

    [<Fact>]
    let empty () =
        let input = Pattern.empty
        let expected = Seq.empty
        let result = input |> Pattern.flatten 1.0 combineInto
        result |> should beEquivalentTo expected

    [<Fact>]
    let simpleTimeline () =
        let input =
            { Duration = 1.0
              Timeline =
                  seq { { Position = 0.0; Value = 1 } }
                  |> Timeline.fromSequence
              PatternTimeline = Timeline.empty }

        let expected = seq { { Position = 0.0; Value = 1 } }
        let result = input |> Pattern.flatten 1.0 combineInto
        result |> should beEquivalentTo expected

    [<Fact>]
    let simpleInnerPattern () =
        let input =
            { Duration = 1.0
              Timeline = Timeline.empty
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
                  |> Timeline.fromSequence }

        let expected = seq { { Position = 0.0; Value = 1 } }
        let result = input |> Pattern.flatten 1.0 combineInto
        result |> should beEquivalentTo expected

    [<Fact>]
    let multiInnerPattern () =
        let input =
            { Duration = 1.0
              Timeline =
                  seq { { Position = 0.0; Value = 1 } }
                  |> Timeline.fromSequence
              PatternTimeline =
                  seq {
                      { Position = 0.25
                        Value =
                            { Duration = 1.0
                              Timeline =
                                  seq { { Position = 0.0; Value = 2 } }
                                  |> Timeline.fromSequence
                              PatternTimeline =
                                  seq {
                                      { Position = 0.25
                                        Value =
                                            { Duration = 1.0
                                              Timeline =
                                                  seq { { Position = 0.0; Value = 3 } }
                                                  |> Timeline.fromSequence
                                              PatternTimeline = Timeline.empty } }
                                      { Position = 0.5
                                        Value =
                                            { Duration = 1.0
                                              Timeline =
                                                  seq { { Position = 0.0; Value = 4 } }
                                                  |> Timeline.fromSequence
                                              PatternTimeline = Timeline.empty } }
                                  }
                                  |> Timeline.fromSequence } }
                      { Position = 0.75
                        Value =
                            { Duration = 1.0
                              Timeline =
                                  seq {
                                      { Position = 0.0; Value = 5 }
                                      { Position = 0.25; Value = 6 }
                                  }
                                  |> Timeline.fromSequence
                              PatternTimeline = Timeline.empty } }
                  }
                  |> Timeline.fromSequence }

        let expected =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 0.25; Value = 2 }
                { Position = 0.5; Value = 3 }
                { Position = 0.75; Value = 5 }
            }

        let result = input |> Pattern.flatten 1.0 combineInto
        result |> should beEquivalentTo expected
