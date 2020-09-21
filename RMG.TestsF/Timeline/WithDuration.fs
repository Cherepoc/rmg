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
            seq {
                { Position = 0.0
                  Value = { Duration = 1.0; Value = 1 } }
            }

        let result = input |> Timeline.withDuration 1.0

        result |> should beEquivalentTo expected

    [<Fact>]
    let multi () =
        let input =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 0.5; Value = 2 }
            }

        let expected =
            seq {
                { Position = 0.0
                  Value = { Duration = 0.5; Value = 1 } }
                { Position = 0.5
                  Value = { Duration = 0.5; Value = 2 } }
            }

        let result = input |> Timeline.withDuration 1.0

        result |> should beEquivalentTo expected

    [<Fact>]
    let cutoff () =
        let input =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 0.5; Value = 2 }
                { Position = 1.5; Value = 3 }
            }

        let expected =
            seq {
                { Position = 0.0
                  Value = { Duration = 0.5; Value = 1 } }
                { Position = 0.5
                  Value = { Duration = 0.5; Value = 2 } }
            }

        let result = input |> Timeline.withDuration 1.0

        result |> should beEquivalentTo expected
