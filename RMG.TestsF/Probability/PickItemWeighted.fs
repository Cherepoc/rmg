namespace RMG.TestsF.Probability

open Xunit
open RMG.TestsF.Assertions
open RMG.CoreF.Probability


type PickItemWeighted() =
    let probabilityFunction: IntProbabilityFunction = fun value -> 1.0 / (2.0 ** float value)

    [<Theory>]
    [<InlineData(0, 1)>]
    [<InlineData(0.25, 1)>]
    [<InlineData(0.5, 1)>]
    [<InlineData(0.75, 1)>]
    [<InlineData(1, 1)>]
    let singleElement (value: float, expected: int) =
        let items = seq { 1 }

        let result =
            items
            |> pickItemWeighted probabilityFunction value

        result |> should beEquivalentTo expected

    [<Theory>]
    [<InlineData(0, 1)>]
    [<InlineData(0.25, 1)>]
    [<InlineData(0.5, 1)>]
    [<InlineData(0.75, 2)>]
    [<InlineData(1, 2)>]
    let twoElement (value: float, expected: int) =
        let items =
            seq {
                1
                2
            }

        let result =
            items
            |> pickItemWeighted probabilityFunction value

        result |> should beEquivalentTo expected

    [<Theory>]
    [<InlineData(0, 1)>]
    [<InlineData(0.25, 1)>]
    [<InlineData(0.5, 1)>]
    [<InlineData(0.75, 2)>]
    [<InlineData(1, 5)>]
    let multiElement (value: float, expected: int) =
        let items =
            seq {
                1
                2
                3
                4
                5
            }

        let result =
            items
            |> pickItemWeighted probabilityFunction value

        result |> should beEquivalentTo expected
