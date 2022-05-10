namespace RMG.Tests.StateTimeline

open Xunit
open RMG.Tests.Assertions
open RMG.Core

type OfSeq() =
    [<Fact>]
    let empty () =
        let input : int list TimelineItem seq = Seq.empty
        let expected : int list TimelineItem array = [| struct (0.0, []); struct (1.0, []) |]
        let result : int StateTimeline = input |> StateTimeline.ofSeq 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleBeginning () =
        let input : int list TimelineItem seq = seq { struct (0.0, [1]) }
        let expected : int list TimelineItem array = [| struct (0.0, [1]); struct (1.0, []) |]
        let result : int StateTimeline = input |> StateTimeline.ofSeq 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleMiddle () =
        let input : int list TimelineItem seq = seq { struct (0.5, [1]) }
        let expected : int list TimelineItem array = [| struct (0.0, []); struct (0.5, [1]); struct (1.0, []) |]
        let result : int StateTimeline = input |> StateTimeline.ofSeq 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleEnd () =
        let input : int list TimelineItem seq = seq { struct (1.0, [1]) }
        let expected : int list TimelineItem array = [| struct (0.0, []); struct (1.0, []) |]
        let result : int StateTimeline = input |> StateTimeline.ofSeq 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let multiple () =
        let input : int list TimelineItem seq = seq {
            struct (0.0, [])
            struct (1.0, [1])
            struct (2.0, [2])
        }
        let expected : int list TimelineItem array = [|
            struct (0.0, [])
            struct (1.0, [1])
            struct (2.0, [2])
            struct (3.0, [])
        |]
        let result : int StateTimeline = input |> StateTimeline.ofSeq 3.0
        result |> should beEquivalentTo expected
