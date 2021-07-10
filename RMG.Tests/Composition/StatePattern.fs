namespace RMG.TestsF

open RMG.CoreF
open RMG.CoreF.Composition
open Xunit
open RMG.TestsF.Assertions

type StatePattern() =
    let stateMerger = StateMerger.additiveInt

    [<Fact>]
    let empty () =
        let input =
            {
                StatePatternInput.TimelineInput = Seq.empty
                StatePatternInput.PatternTimelineInput = Seq.empty
            }

        let expected = Seq.empty

        let result = input |> StatePattern.fromInput 1.0 stateMerger

        result.FlatTimeline |> should beEquivalentTo expected

    [<Fact>]
    let singleItemTimeline () =
        let input =
            {
                StatePatternInput.TimelineInput = seq { (0.0, 1) }
                StatePatternInput.PatternTimelineInput = Seq.empty
            }

        let expected =
            seq {
                (0.0, 1)
                (1.0, 0)
            }

        let result = input |> StatePattern.fromInput 1.0 stateMerger

        result.FlatTimeline |> should beEquivalentTo expected

    [<Fact>]
    let singleItemPatternTimeline () =
        let input =
            {
                StatePatternInput.TimelineInput = Seq.empty
                StatePatternInput.PatternTimelineInput =
                    seq {
                        (0.0,
                         {
                             StatePatternInput.TimelineInput = seq { (0.0, 1) }
                             StatePatternInput.PatternTimelineInput = Seq.empty
                         }
                         |> StatePattern.fromInput 1.0 stateMerger)
                    }
            }

        let expected =
            seq {
                (0.0, 1)
                (1.0, 0)
            }

        let result = input |> StatePattern.fromInput 1.0 stateMerger

        result.FlatTimeline |> should beEquivalentTo expected

    [<Fact>]
    let mergesTimelines () =
        let input =
            {
                StatePatternInput.TimelineInput = seq { (0.0, 1) }
                StatePatternInput.PatternTimelineInput =
                    seq {
                        (0.5,
                         {
                             StatePatternInput.TimelineInput = seq { (0.0, 2) }
                             StatePatternInput.PatternTimelineInput = Seq.empty
                         }
                         |> StatePattern.fromInput 1.0 stateMerger)
                    }
            }

        let expected =
            seq {
                (0.0, 1)
                (0.5, 3)
                (1.0, 2)
                (1.5, 0)
            }

        let result = input |> StatePattern.fromInput 1.0 stateMerger

        result.FlatTimeline |> should beEquivalentTo expected
