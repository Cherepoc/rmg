namespace RMG.TestsF.Timeline

open RMG.CoreF
open Xunit
open RMG.TestsF.Assertions

type Trim() =
    let def = 0

    [<Fact>]
    let empty () =
        let input = Seq.empty
        let expected = Seq.empty
        let result = input |> Timeline.trim def
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleDefault () =
        let input = seq { { Position = 0.0; Value = def } }
        let expected = Seq.empty
        let result = input |> Timeline.trim def
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleNonDefault () =
        let input = seq { { Position = 0.0; Value = 1 } }
        let expected = seq { { Position = 0.0; Value = 1 } }
        let result = input |> Timeline.trim def
        result |> should beEquivalentTo expected

    [<Fact>]
    let trimDefaultStart () =
        let input =
            seq {
                { Position = 0.0; Value = 0 }
                { Position = 1.0; Value = 1 }
            }

        let expected = seq { { Position = 1.0; Value = 1 } }
        let result = input |> Timeline.trim def
        result |> should beEquivalentTo expected

    [<Fact>]
    let trimDouble () =
        let input =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 1.0; Value = 1 }
            }

        let expected = seq { { Position = 0.0; Value = 1 } }
        let result = input |> Timeline.trim def
        result |> should beEquivalentTo expected

    [<Fact>]
    let trimMultiDouble () =
        let input =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 1.0; Value = 1 }
                { Position = 2.0; Value = 0 }
                { Position = 3.0; Value = 0 }
            }

        let expected =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 2.0; Value = 0 }
            }

        let result = input |> Timeline.trim def
        result |> should beEquivalentTo expected
