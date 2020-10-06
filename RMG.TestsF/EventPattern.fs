namespace RMG.TestsF

open RMG.CoreF
open RMG.CoreF.Composition
open Xunit
open RMG.TestsF.Assertions

type EventPattern() =
    [<Fact>]
    let empty () =
        let input =
            { TimelineInput = Seq.empty
              PatternTimelineInput = Seq.empty }

        let expected = Seq.empty

        let result = input |> EventPattern.fromInput 1.0

        result.FlatTimeline
        |> should beEquivalentTo expected

    [<Fact>]
    let singleItemTimeline () =
        let input =
            { TimelineInput = seq { { Position = 0.0; Value = 1 } }
              PatternTimelineInput = Seq.empty }

        let expected = seq { { Position = 0.0; Value = 1 } }

        let result = input |> EventPattern.fromInput 1.0

        result.FlatTimeline
        |> should beEquivalentTo expected

    [<Fact>]
    let singleItemPatternTimeline () =
        let input =
            { TimelineInput = Seq.empty
              PatternTimelineInput =
                  seq {
                      { Position = 0.0
                        Value =
                            { TimelineInput = seq { { Position = 0.0; Value = 1 } }
                              PatternTimelineInput = Seq.empty }
                            |> EventPattern.fromInput 1.0 }
                  } }

        let expected = seq { { Position = 0.0; Value = 1 } }

        let result = input |> EventPattern.fromInput 1.0

        result.FlatTimeline
        |> should beEquivalentTo expected

    [<Fact>]
    let mergesTimelines () =
        let input =
            { TimelineInput = seq { { Position = 0.0; Value = 1 } }
              PatternTimelineInput =
                  seq {
                      { Position = 0.5
                        Value =
                            { TimelineInput = seq { { Position = 0.0; Value = 2 } }
                              PatternTimelineInput = Seq.empty }
                            |> EventPattern.fromInput 1.0 }
                  } }

        let expected =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 0.5; Value = 2 }
            }

        let result = input |> EventPattern.fromInput 1.0

        result.FlatTimeline
        |> should beEquivalentTo expected
