namespace RMG.TestsF

open RMG.CoreF
open RMG.CoreF.Composition
open Xunit
open RMG.TestsF.Assertions

type EventPattern() =
    [<Fact>]
    let empty () =
        let input =
            {
                TimelineInput = Seq.empty
                PatternTimelineInput = Seq.empty
            }

        let expected = Seq.empty

        let result = input |> EventPattern.fromInput 1.0

        result.FlatTimeline |> should beEquivalentTo expected

    [<Fact>]
    let singleItemTimeline () =
        let input =
            {
                TimelineInput = seq { (0.0, 1) }
                PatternTimelineInput = Seq.empty
            }

        let expected = seq { (0.0, 1) }

        let result = input |> EventPattern.fromInput 1.0

        result.FlatTimeline |> should beEquivalentTo expected

    [<Fact>]
    let singleItemPatternTimeline () =
        let input =
            {
                TimelineInput = Seq.empty
                PatternTimelineInput =
                    {
                        TimelineInput = seq { (0.0, 1) }
                        PatternTimelineInput = Seq.empty
                    }
                    |> EventPattern.fromInput 1.0
                    |> Timeline.fromSingle
            }

        let expected = seq { (0.0, 1) }

        let result = input |> EventPattern.fromInput 1.0

        result.FlatTimeline |> should beEquivalentTo expected

    [<Fact>]
    let mergesTimelines () =
        let input =
            {
                TimelineInput = seq { (0.0, 1) }
                PatternTimelineInput =
                    seq {
                        (0.5,
                         {
                             TimelineInput = seq { (0.0, 2) }
                             PatternTimelineInput = Seq.empty
                         }
                         |> EventPattern.fromInput 1.0)
                    }
            }

        let expected =
            seq {
                (0.0, 1)
                (0.5, 2)
            }

        let result = input |> EventPattern.fromInput 1.0

        result.FlatTimeline |> should beEquivalentTo expected
