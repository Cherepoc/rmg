namespace RMG.TestsF.Timeline

open RMG.CoreF
open Xunit
open RMG.TestsF.Assertions

type private TestRecord =
    { Position: Position
      Duration: Duration }

type Aggregate() =
    let combine (value: TestRecord, position: Position, duration: Duration) (initialValue: TestRecord): TestRecord =
        { Position = initialValue.Position + value.Position + position
          Duration = initialValue.Duration + value.Duration + duration }

    [<Fact>]
    let empty () =
        let input = Seq.empty
        let expected = { Position = 0.0; Duration = 0.0 }

        let result =
            input
            |> Timeline.aggregate (1.0, combine) { Position = 0.0; Duration = 0.0 }

        result |> should beEquivalentTo expected

    [<Fact>]
    let single () =
        let input =
            seq {
                { Position = 1.0
                  Value = { Position = 0.0; Duration = 0.0 } }
            }

        let expected = { Position = 4.0; Duration = 4.0 }

        let result =
            input
            |> Timeline.aggregate (2.0, combine) { Position = 3.0; Duration = 3.0 }

        result |> should beEquivalentTo expected

    [<Fact>]
    let multi () =
        let input =
            seq {
                { Position = 0.0
                  Value = { Position = 1.0; Duration = 1.0 } }
                { Position = 1.0
                  Value = { Position = 2.0; Duration = 2.0 } }
            }

        let expected = { Position = 7.0; Duration = 8.0 }

        let result =
            input
            |> Timeline.aggregate (2.0, combine) { Position = 3.0; Duration = 3.0 }

        result |> should beEquivalentTo expected

    [<Fact>]
    let cutoff () =
        let input =
            seq {
                { Position = 0.0
                  Value = { Position = 1.0; Duration = 1.0 } }
                { Position = 1.0
                  Value = { Position = 2.0; Duration = 2.0 } }
                { Position = 2.5
                  Value = { Position = 3.0; Duration = 3.0 } }
            }

        let expected = { Position = 7.0; Duration = 8.0 }

        let result =
            input
            |> Timeline.aggregate (2.0, combine) { Position = 3.0; Duration = 3.0 }

        result |> should beEquivalentTo expected
