namespace RMG.TestsF.StateTimeline

open Xunit
open RMG.TestsF.Assertions
open RMG.CoreF

type TryFindEffectiveIndex() =
    let ofSeq (duration: Duration) (input: int TimelineItem seq): int StateTimeline =
        input |> StateTimeline.ofSeq duration (fun a b -> a + b) 0

    [<Fact>]
    let empty () =
        let input : int StateTimeline = Seq.empty |> ofSeq 1.0
        let expected : int option = Some 0
        let result = input |> StateTimeline.tryFindEffectiveIndex 0.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let exact () =
        let input : int StateTimeline =
            seq {
                struct (0.0, 10)
                struct (1.0, 11)
                struct (2.0, 12)
            }
            |> ofSeq 3.0
        let expected : int option = Some 1
        let result = input |> StateTimeline.tryFindEffectiveIndex 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let between () =
        let input : int StateTimeline =
            seq {
                struct (0.0, 10)
                struct (1.0, 11)
                struct (2.0, 12)
            }
            |> ofSeq 3.0
        let expected : int option = Some 1
        let result = input |> StateTimeline.tryFindEffectiveIndex 1.5
        result |> should beEquivalentTo expected

    [<Fact>]
    let afterAll () =
        let input : int StateTimeline =
            seq {
                struct (0.0, 10)
                struct (1.0, 11)
                struct (2.0, 12)
            }
            |> ofSeq 3.0
        let expected : int option = Some 3
        let result = input |> StateTimeline.tryFindEffectiveIndex 4.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let beforeAll () =
        let input : int StateTimeline =
            seq {
                struct (0.0, 10)
                struct (1.0, 11)
                struct (2.0, 12)
            }
            |> ofSeq 3.0
        let expected : int option = None
        let result = input |> StateTimeline.tryFindEffectiveIndex -1.0
        result |> should beEquivalentTo expected
