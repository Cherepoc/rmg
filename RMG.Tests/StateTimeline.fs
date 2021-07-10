namespace RMG.TestsF

open RMG.CoreF
open Xunit
open RMG.TestsF.Assertions

type StateTimeline() =
    let stateMerger = StateMerger.additiveInt

    [<Fact>]
    let fromEmptySource () =
        let input = Seq.empty
        let expected = Seq.empty

        let result = input |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected

    [<Fact>]
    let fromDefaultElementSource () =
        let input = seq { (0.0, 0) }
        let expected = Seq.empty

        let result = input |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected

    [<Fact>]
    let fromSingleElementSource () =
        let input = seq { (0.0, 1) }

        let expected =
            seq {
                (0.0, 1)
                (1.0, 0)
            }

        let result = input |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected

    [<Fact>]
    let fromMultiElementSource () =
        let input =
            seq {
                (0.5, 2)
                (0.0, 1)
            }

        let expected =
            seq {
                (0.0, 1)
                (0.5, 2)
                (1.0, 0)
            }

        let result = input |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected

    [<Fact>]
    let fromMultiElementEndsDefaultSource () =
        let input =
            seq {
                (0.5, 0)
                (0.0, 1)
            }

        let expected =
            seq {
                (0.0, 1)
                (0.5, 0)
            }

        let result = input |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected

    [<Fact>]
    let fromMultiElementSinglePositionSource () =
        let input =
            seq {
                (0.5, 3)
                (0.0, 1)
                (0.5, 2)
            }

        let expected =
            seq {
                (0.0, 1)
                (0.5, 5)
                (1.0, 0)
            }

        let result = input |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected

    [<Fact>]
    let fromSourceTrimsDuration () =
        let input =
            seq {
                (1.0, 3)
                (0.0, 1)
                (0.5, 2)
            }

        let expected =
            seq {
                (0.0, 1)
                (0.5, 2)
                (1.0, 0)
            }

        let result = input |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected
