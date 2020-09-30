namespace RMG.TestsF.Timeline

open RMG.CoreF
open Xunit
open RMG.TestsF.Assertions

type private TestRecord =
    { Position: Position
      Duration: Duration }

type Aggregate() =
    let combineInto (dest: TimelineLike<'T>) (vector: Vector) (source: TimelineLike<'T>): TimelineLike<'T> =
            source |> Timeline.insertInto dest vector :> TimelineLike<'T>

    [<Fact>]
    let empty () =
        let input = Seq.empty
        let expected = Seq.empty

        let result =
            input
            |> Timeline.aggregate 1.0 combineInto Seq.empty

        result |> should beEquivalentTo expected

    [<Fact>]
    let single () =
        let input = seq {
            {Position = 0.0; Value = seq {{ Position = 0.0; Value = 1 }}}
        }
        let expected = seq {{ Position = 0.0; Value = 1 }}

        let result =
            input
            |> Timeline.aggregate 2.0 combineInto Seq.empty

        result |> should beEquivalentTo expected

    [<Fact>]
    let multi () =
        let input = seq {
            {Position = 0.0; Value = seq {
                { Position = 0.5; Value = 2 }
                { Position = 0.0; Value = 1 }
            }}
            {Position = 1.0; Value = seq {{ Position = 0.0; Value = 3 }}}
        }
        let expected = seq {
            { Position = 0.0; Value = 1 }
            { Position = 0.5; Value = 2 }
            { Position = 1.0; Value = 3 }
        }

        let result =
            input
            |> Timeline.aggregate 2.0 combineInto Seq.empty

        result |> should beEquivalentTo expected

    [<Fact>]
    let cutoff () =
        let input = seq {
            {Position = 0.0; Value = seq {
                { Position = 1.0; Value = 2 }
                { Position = 0.0; Value = 1 }
            }}
            {Position = 0.5; Value = seq {
                { Position = 0.5; Value = 4 }
                { Position = 0.0; Value = 3 }
            }}
            {Position = 1.0; Value = seq {
                { Position = 0.0; Value = 5 }
            }}
        }
        let expected = seq {
            { Position = 0.0; Value = 1 }
            { Position = 0.5; Value = 3 }
        }

        let result =
            input
            |> Timeline.aggregate 1.0 combineInto Seq.empty

        result |> should beEquivalentTo expected
