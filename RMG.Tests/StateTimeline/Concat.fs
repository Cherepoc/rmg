namespace RMG.Tests.StateTimeline

open Xunit
open RMG.Tests.Assertions
open RMG.Core

type Concat() =
    [<Fact>]
    let empty () =
        let input : int list StateTimeline Timeline = Seq.empty |> Timeline.ofSeq
        let expected : int list TimelineItem array = [| struct (0.0, []); struct (1.0, []) |]
        let result = input |> StateTimeline.concat 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleEmpty () =
        let input : int list StateTimeline Timeline =
            seq { struct (0.0, Seq.empty |> StateTimeline.ofSeq 1.0) }
            |> Timeline.ofSeq

        let expected : int list TimelineItem array = [| struct (0.0, []); struct (1.0, []) |]
        let result = input |> StateTimeline.concat 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleMultiItem () =
        let input : int StateTimeline Timeline =
            seq {
                struct (0.0,
                        seq {
                            struct (0.0, [1])
                            struct (1.0, [2])
                            struct (2.0, [3])
                        }
                        |> StateTimeline.ofSeq 3.0)
            }
            |> Timeline.ofSeq

        let expected : int list TimelineItem array = [| struct (0.0, [1]); struct (1.0, [2]); struct (2.0, []) |]
        let result = input |> StateTimeline.concat 2.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let multiMultiItem () =
        let input : int StateTimeline Timeline =
            seq {
                struct (0.0,
                        seq {
                            struct (0.0, [1])
                            struct (1.0, [2])
                            struct (2.0, [3])
                        }
                        |> StateTimeline.ofSeq 3.0)

                struct (0.5,
                        seq {
                            struct (0.0, [11])
                            struct (1.0, [12])
                            struct (2.0, [13])
                        }
                        |> StateTimeline.ofSeq 3.0)
            }
            |> Timeline.ofSeq

        let expected : int list TimelineItem array =
            [|
                struct (0.0, [1])
                struct (0.5, [1; 11])
                struct (1.0, [2; 11])
                struct (1.5, [2; 12])
                struct (2.0, [3; 12])
                struct (2.5, [])
            |]

        let result = input |> StateTimeline.concat 2.5
        result |> should beEquivalentTo expected
