namespace RMG.TestsF.Timeline

open RMG.CoreF
open Xunit
open RMG.TestsF.Assertions

type Main() =
    [<Fact>]
    let empty () =
        let input = Seq.empty
        let expected = Timeline.empty
        let result = input |> Timeline.fromSequence
        result |> should beEquivalentTo expected

    [<Fact>]
    let single () =
        let input = seq { { Position = 0.0; Value = 1 } }
        let expected = seq { { Position = 0.0; Value = 1 } }
        let result = input |> Timeline.fromSequence
        result |> should beEquivalentTo expected

    [<Fact>]
    let order () =
        let input = seq {
            { Position = 0.1; Value = 1 }
            { Position = 0.0; Value = 2 }
        }
        let expected = seq {
            { Position = 0.0; Value = 2 }
            { Position = 0.1; Value = 1 }
        }
        let result = input |> Timeline.fromSequence
        result |> should beEquivalentTo expected
