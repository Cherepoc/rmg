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

        let result =
            input
            |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected

    [<Fact>]
    let fromDefaultElementSource () =
        let input = seq { { Position = 0.0; Value = 0 } }
        let expected = Seq.empty

        let result =
            input
            |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected

    [<Fact>]
    let fromSingleElementSource () =
        let input = seq { { Position = 0.0; Value = 1 } }
        let expected = seq {
            { Position = 0.0; Value = 1 }
            { Position = 1.0; Value = 0 }
        }

        let result =
            input
            |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected

    [<Fact>]
    let fromMultiElementSource () =
        let input = seq {
            { Position = 0.5; Value = 2 }
            { Position = 0.0; Value = 1 }
        }
        let expected = seq {
            { Position = 0.0; Value = 1 }
            { Position = 0.5; Value = 2 }
            { Position = 1.0; Value = 0 }
        }

        let result =
            input
            |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected

    [<Fact>]
    let fromMultiElementEndsDefaultSource () =
        let input = seq {
            { Position = 0.5; Value = 0 }
            { Position = 0.0; Value = 1 }
        }
        let expected = seq {
            { Position = 0.0; Value = 1 }
            { Position = 0.5; Value = 0 }
        }

        let result =
            input
            |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected

    [<Fact>]
    let fromMultiElementSinglePositionSource () =
        let input = seq {
            { Position = 0.5; Value = 3 }
            { Position = 0.0; Value = 1 }
            { Position = 0.5; Value = 2 }
        }
        let expected = seq {
            { Position = 0.0; Value = 1 }
            { Position = 0.5; Value = 5 }
            { Position = 1.0; Value = 0 }
        }

        let result =
            input
            |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected

    [<Fact>]
    let fromSourceTrimsDuration () =
        let input = seq {
            { Position = 1.0; Value = 3 }
            { Position = 0.0; Value = 1 }
            { Position = 0.5; Value = 2 }
        }
        let expected = seq {
            { Position = 0.0; Value = 1 }
            { Position = 0.5; Value = 2 }
            { Position = 1.0; Value = 0 }
        }

        let result =
            input
            |> StateTimeline.fromSequence 1.0 stateMerger

        result |> should beEquivalentTo expected
