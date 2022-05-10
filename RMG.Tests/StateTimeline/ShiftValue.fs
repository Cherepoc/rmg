namespace RMG.Tests.StateTimeline

open Xunit
open RMG.Tests.Assertions
open RMG.Core

type ShiftValue() =
    [<Fact>]
    let empty () =
        let input : int StateTimeline = Seq.empty |> StateTimeline.ofSeq 1.0
        let expected : int list TimelineItem array = [| struct (0.0, [1]); struct (1.0, []) |]
        let result : int StateTimeline = input |> StateTimeline.shiftValue [1]
        result |> should beEquivalentTo expected

    [<Fact>]
    let single () =
        let input : int StateTimeline = seq {struct(0.5, [1])} |> StateTimeline.ofSeq 1.0
        let expected : int list TimelineItem array = [| struct (0.0, [1]); struct (0.5, [1; 1]); struct (1.0, []) |]
        let result : int StateTimeline = input |> StateTimeline.shiftValue [1]
        result |> should beEquivalentTo expected
