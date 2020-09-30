namespace RMG.TestsF.Timeline

open RMG.CoreF
open Xunit
open RMG.TestsF.Assertions

type WithDuration() =

    [<Fact>]
    let empty () =
        let input = Seq.empty
        let expected = Seq.empty

        let result = input |> Timeline.withDuration 1.0

        result |> should beEquivalentTo expected

    [<Fact>]
    let single () =
        let input = seq { { Position = 0.0; Value = 1 } }

        let expected =
            seq { { Position = 0.0; Value = (1, 1.0) } }

        let result = input |> Timeline.withDuration 1.0

        result |> should beEquivalentTo expected

    [<Fact>]
    let double () =
        let input =
            seq {
                { Position = 0.5; Value = 2 }
                { Position = 0.0; Value = 1 }
            }

        let expected =
            seq {
                { Position = 0.0; Value = (1, 0.5) }
                { Position = 0.5; Value = (2, 0.5) }
            }

        let result = input |> Timeline.withDuration 1.0

        result |> should beEquivalentTo expected

    [<Fact>]
    let triple () =
        let input =
            seq {
                { Position = 0.5; Value = 2 }
                { Position = 0.0; Value = 1 }
                { Position = 0.75; Value = 3 }
            }

        let expected =
            seq {
                { Position = 0.0; Value = (1, 0.5) }
                { Position = 0.5; Value = (2, 0.25) }
                { Position = 0.75; Value = (3, 0.25) }
            }

        let result = input |> Timeline.withDuration 1.0

        result |> should beEquivalentTo expected

    [<Fact>]
    let cutoff () =
        let input =
            seq {
                { Position = 0.5; Value = 2 }
                { Position = 0.0; Value = 1 }
                { Position = 1.5; Value = 3 }
            }

        let expected =
            seq {
                { Position = 0.0; Value = (1, 0.5) }
                { Position = 0.5; Value = (2, 0.5) }
            }

        let result = input |> Timeline.withDuration 1.0

        result |> should beEquivalentTo expected
