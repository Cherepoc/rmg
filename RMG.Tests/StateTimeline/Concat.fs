namespace RMG.TestsF.StateTimeline

open Xunit
open RMG.TestsF.Assertions
open RMG.CoreF

type Concat() =
    let merger : int -> int -> int = fun a b -> a + b
    let defaultValue : int = 0

    [<Fact>]
    let empty () =
        let input : int StateTimeline Timeline = Seq.empty |> Timeline.ofSeq
        let expected : int TimelineItem array = [| struct (0.0, 0); struct (1.0, 0) |]
        let result = input |> StateTimeline.concat 1.0 merger defaultValue
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleEmpty () =
        let input : int StateTimeline Timeline =
            seq { struct (0.0, Seq.empty |> StateTimeline.ofSeq 1.0 merger defaultValue) }
            |> Timeline.ofSeq

        let expected : int TimelineItem array = [| struct (0.0, 0); struct (1.0, 0) |]
        let result = input |> StateTimeline.concat 1.0 merger defaultValue
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleMultiItem () =
        let input : int StateTimeline Timeline =
            seq {
                struct (0.0,
                        seq {
                            struct (0.0, 1)
                            struct (1.0, 2)
                            struct (2.0, 3)
                        }
                        |> StateTimeline.ofSeq 3.0 merger defaultValue)
            }
            |> Timeline.ofSeq

        let expected : int TimelineItem array = [| struct (0.0, 1); struct (1.0, 2); struct (2.0, 0) |]
        let result = input |> StateTimeline.concat 2.0 merger defaultValue
        result |> should beEquivalentTo expected

    [<Fact>]
    let multiMultiItem () =
        let input : int StateTimeline Timeline =
            seq {
                struct (0.0,
                        seq {
                            struct (0.0, 1)
                            struct (1.0, 2)
                            struct (2.0, 3)
                        }
                        |> StateTimeline.ofSeq 3.0 merger defaultValue)

                struct (0.5,
                        seq {
                            struct (0.0, 11)
                            struct (1.0, 12)
                            struct (2.0, 13)
                        }
                        |> StateTimeline.ofSeq 3.0 merger defaultValue)
            }
            |> Timeline.ofSeq

        let expected : int TimelineItem array =
            [|
                struct (0.0, 1)
                struct (0.5, 12)
                struct (1.0, 13)
                struct (1.5, 14)
                struct (2.0, 15)
                struct (2.5, 0)
            |]

        let result = input |> StateTimeline.concat 2.5 merger defaultValue
        result |> should beEquivalentTo expected
