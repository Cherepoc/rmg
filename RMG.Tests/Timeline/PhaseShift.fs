namespace RMG.TestsF.Timeline

open Xunit
open RMG.TestsF.Assertions
open RMG.CoreF

type PhaseShift() =
    [<Fact>]
    let empty () =
        let input : int Timeline = Seq.empty |> Timeline.ofSeq
        let expected : int TimelineItem array = [||]
        let result : int Timeline = input |> Timeline.phaseShift 0.0 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleZeroPhase () =
        let input : int Timeline = seq { struct (0.0, 1) } |> Timeline.ofSeq
        let expected : int TimelineItem array = [| struct (0.0, 1) |]
        let result : int Timeline = input |> Timeline.phaseShift 0.0 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleBeforeBreak () =
        let input : int Timeline = seq { struct (0.0, 1) } |> Timeline.ofSeq
        let expected : int TimelineItem array = [| struct (0.5, 1) |]
        let result : int Timeline = input |> Timeline.phaseShift 0.5 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleAtBreak () =
        let input : int Timeline = seq { struct (0.5, 1) } |> Timeline.ofSeq
        let expected : int TimelineItem array = [| struct (0.0, 1) |]
        let result : int Timeline = input |> Timeline.phaseShift 0.5 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleAfterBreak () =
        let input : int Timeline = seq { struct (0.75, 1) } |> Timeline.ofSeq
        let expected : int TimelineItem array = [| struct (0.25, 1) |]
        let result : int Timeline = input |> Timeline.phaseShift 0.5 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let multiple () =
        let input : int Timeline =
            seq {
                struct (0.25, 1)
                struct (0.5, 2)
                struct (0.75, 3)
            }
            |> Timeline.ofSeq
        let expected : int TimelineItem array =
            [|
                struct (0.0, 2)
                struct (0.25, 3)
                struct (0.75, 1)
            |]
        let result : int Timeline = input |> Timeline.phaseShift 0.5 1.0
        result |> should beEquivalentTo expected
