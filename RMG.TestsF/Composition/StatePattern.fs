namespace RMG.TestsF

open RMG.CoreF
open RMG.CoreF.Composition
open Xunit
open RMG.TestsF.Assertions

type StatePattern() =
    let stateMerger = StateMerger.additive

    [<Fact>]
    let empty () =
        let input =
            { StatePatternInput.TimelineInput = Seq.empty
              StatePatternInput.PatternTimelineInput = Seq.empty }

        let expected = Seq.empty

        let result =
            input |> StatePattern.fromInput 1.0 stateMerger

        result.FlatTimeline
        |> should beEquivalentTo expected

    [<Fact>]
    let singleItemTimeline () =
        let input =
            { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1 } }
              StatePatternInput.PatternTimelineInput = Seq.empty }

        let expected =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 1.0; Value = 0 }
            }

        let result =
            input |> StatePattern.fromInput 1.0 stateMerger

        result.FlatTimeline
        |> should beEquivalentTo expected

    [<Fact>]
    let singleItemPatternTimeline () =
        let input =
            { StatePatternInput.TimelineInput = Seq.empty
              StatePatternInput.PatternTimelineInput =
                  seq {
                      { Position = 0.0
                        Value =
                            { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1 } }
                              StatePatternInput.PatternTimelineInput = Seq.empty }
                            |> StatePattern.fromInput 1.0 stateMerger }
                  } }

        let expected =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 1.0; Value = 0 }
            }

        let result =
            input |> StatePattern.fromInput 1.0 stateMerger

        result.FlatTimeline
        |> should beEquivalentTo expected

    [<Fact>]
    let mergesTimelines () =
        let input =
            { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1 } }
              StatePatternInput.PatternTimelineInput =
                  seq {
                      { Position = 0.5
                        Value =
                            { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 2 } }
                              StatePatternInput.PatternTimelineInput = Seq.empty }
                            |> StatePattern.fromInput 1.0 stateMerger }
                  } }

        let expected =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 0.5; Value = 3 }
                { Position = 1.0; Value = 2 }
                { Position = 1.5; Value = 0 }
            }

        let result =
            input |> StatePattern.fromInput 1.0 stateMerger

        result.FlatTimeline
        |> should beEquivalentTo expected
