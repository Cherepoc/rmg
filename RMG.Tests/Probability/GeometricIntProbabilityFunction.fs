namespace RMG.Tests.Probability

open Xunit
open RMG.Tests.Assertions
open RMG.Core.Probability

type GeometricIntProbabilityFunction() =
    [<Theory>]
    [<InlineData(-2, 0.25)>]
    [<InlineData(-1, 0.5)>]
    [<InlineData(0, 1.0)>]
    [<InlineData(1, 0.5)>]
    [<InlineData(2, 0.25)>]
    let simple (arg: int, expected: float) =
        let func = geometricIntProbabilityFunction (0.0, 1.0, 0.5, 0)

        let result = func arg
        result |> should beEquivalentTo expected

    [<Theory>]
    [<InlineData(-2, 0.375)>]
    [<InlineData(-1, 0.5)>]
    [<InlineData(0, 0.75)>]
    [<InlineData(1, 0.5)>]
    [<InlineData(2, 0.375)>]
    let minMax (arg: int, expected: float) =
        let func = geometricIntProbabilityFunction (0.25, 0.75, 0.5, 0)

        let result = func arg
        result |> should beEquivalentTo expected

    [<Theory>]
    [<InlineData(-2, 0.125)>]
    [<InlineData(-1, 0.25)>]
    [<InlineData(0, 0.5)>]
    [<InlineData(1, 1.0)>]
    [<InlineData(2, 0.5)>]
    let offset (arg: int, expected: float) =
        let func = geometricIntProbabilityFunction (0.0, 1.0, 0.5, 1)

        let result = func arg
        result |> should beEquivalentTo expected
