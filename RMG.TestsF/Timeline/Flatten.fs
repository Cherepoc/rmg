namespace RMG.TestsF.Timeline

open RMG.CoreF
open Xunit
open RMG.TestsF.Assertions

type Flatten() =
    let combine = Timeline.union

    [<Fact>]
    let empty () =
        let input = Seq.empty
        let expected = Seq.empty
        let result = input |> Timeline.flatten (1.0, combine)
        result |> should beEquivalentTo expected

    [<Fact>]
    let emptyInnerItem () =
        let input =
            seq { { Position = 0.0; Value = Seq.empty } }

        let expected = Seq.empty
        let result = input |> Timeline.flatten (1.0, combine)
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleElement () =
        let input =
            seq {
                { Position = 0.0
                  Value = seq { { Position = 0.0; Value = 1 } } }
            }

        let expected = seq { { Position = 0.0; Value = 1 } }
        let result = input |> Timeline.flatten (1.0, combine)
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleAndEmpty () =
        let input =
            seq {
                { Position = 0.0
                  Value = seq { { Position = 0.0; Value = 1 } } }
                { Position = 0.5
                  Value = Seq.empty }
            }

        let expected = seq { { Position = 0.0; Value = 1 } }
        let result = input |> Timeline.flatten (1.0, combine)
        result |> should beEquivalentTo expected

    [<Fact>]
    let singles () =
        let input =
            seq {
                { Position = 0.0
                  Value = seq { { Position = 0.0; Value = 1 } } }
                { Position = 0.5
                  Value = seq { { Position = 0.25; Value = 2 } } }
            }

        let expected = seq {
            { Position = 0.0; Value = 1 }
            { Position = 0.75; Value = 2 }
        }
        let result = input |> Timeline.flatten (1.0, combine)
        result |> should beEquivalentTo expected

    [<Fact>]
    let multiCutoff () =
        let input =
            seq {
                { Position = 0.0
                  Value = seq {
                      { Position = 0.0; Value = 1 }
                      { Position = 0.25; Value = 2 }
                      { Position = 0.5; Value = 3 }
                  } }
                { Position = 0.5
                  Value = seq {
                      { Position = 0.0; Value = 4 }
                      { Position = 0.5; Value = 5 }
                  } }
                { Position = 0.75
                  Value = seq {
                      { Position = 0.25; Value = 6 }
                  } }
                { Position = 1.0
                  Value = seq {
                      { Position = 0.0; Value = 7 }
                  } }
            }

        let expected = seq {
            { Position = 0.0; Value = 1 }
            { Position = 0.25; Value = 2 }
            { Position = 0.5; Value = 4 }
        }
        let result = input |> Timeline.flatten (1.0, combine)
        result |> should beEquivalentTo expected

    [<Fact>]
    let unordered () =
        let input =
            seq {
                { Position = 0.5
                  Value = seq {
                      { Position = 0.0; Value = 1 }
                      { Position = 0.5; Value = 2 }
                  } }
                { Position = 0.0
                  Value = seq {
                      { Position = 0.0; Value = 3 }
                      { Position = 0.5; Value = 4 }
                  } }
            }

        let expected = seq {
            { Position = 0.0; Value = 3 }
            { Position = 0.5; Value = 1 }
        }
        let result = input |> Timeline.flatten (1.0, combine)
        result |> should beEquivalentTo expected
