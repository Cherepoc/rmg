namespace RMG.TestsF.Timeline

open RMG.CoreF
open Xunit
open RMG.TestsF.Assertions

type Merge() =
    let timelineMerger: TimelineMerger<int> =
        { Merge = (fun (i1, i2) -> i1 + i2)
          Default = 0 }

    [<Fact>]
    let emptyBoth () =
        let dest = Seq.empty
        let source = Seq.empty
        let expected = Seq.empty

        let result =
            dest
            |> Timeline.merge (source, 0.0, 1.0, timelineMerger)

        result |> should beEquivalentTo expected

    [<Fact>]
    let emptySource () =
        let dest = Seq.empty
        let source = seq { { Position = 0.0; Value = 1 } }

        let expected =
            [| { Position = 0.0; Value = 1 }
               { Position = 1.0; Value = 0 } |]

        let result =
            dest
            |> Timeline.merge (source, 0.0, 1.0, timelineMerger)

        result |> should beEquivalentTo expected

    [<Fact>]
    let emptySourceShift () =
        let dest = Seq.empty
        let source = seq { { Position = 0.0; Value = 1 } }

        let expected =
            [| { Position = 0.5; Value = 1 }
               { Position = 1.5; Value = 0 } |]

        let result =
            dest
            |> Timeline.merge (source, 0.5, 1.0, timelineMerger)

        result |> should beEquivalentTo expected

    [<Fact>]
    let emptyTarget () =
        let dest = seq { { Position = 0.0; Value = 1 } }
        let source = Seq.empty
        let expected = [| { Position = 0.0; Value = 1 } |]

        let result =
            dest
            |> Timeline.merge (source, 0.0, 1.0, timelineMerger)

        result |> should beEquivalentTo expected

    [<Fact>]
    let simpleCombine () =
        let dest = seq { { Position = 0.0; Value = 1 } }
        let source = seq { { Position = 0.0; Value = 1 } }

        let expected =
            seq {
                { Position = 0.0; Value = 2 }
                { Position = 1.0; Value = 1 }
            }

        let result =
            dest
            |> Timeline.merge (source, 0.0, 1.0, timelineMerger)

        result |> should beEquivalentTo expected

    [<Fact>]
    let combineSourceLarger () =
        let dest = seq { { Position = 0.0; Value = 1 } }
        let source = seq { { Position = 0.0; Value = 1 } }

        let expected =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 0.25; Value = 2 }
                { Position = 0.75; Value = 1 }
            }

        let result =
            dest
            |> Timeline.merge (source, 0.25, 0.5, timelineMerger)

        result |> should beEquivalentTo expected

    [<Fact>]
    let combineTargetLarger () =
        let dest = seq { { Position = 1.0; Value = 1 } }
        let source = seq { { Position = 0.0; Value = 1 } }

        let expected =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 1.0; Value = 2 }
                { Position = 2.0; Value = 1 }
            }

        let result =
            dest
            |> Timeline.merge (source, 0.0, 2.0, timelineMerger)

        result |> should beEquivalentTo expected

    [<Fact>]
    let combineComplex () =
        let dest =
            seq {
                { Position = 1.0; Value = 1 }
                { Position = 2.0; Value = 2 }
                { Position = 3.0; Value = 0 }
            }

        let source =
            seq {
                { Position = 0.5; Value = 1 }
                { Position = 1.5; Value = 2 }
            }

        let expected =
            seq {
                { Position = 1.0; Value = 1 }
                { Position = 1.5; Value = 2 }
                { Position = 2.0; Value = 3 }
                { Position = 2.5; Value = 4 }
                { Position = 3.0; Value = 0 }
            }

        let result =
            dest
            |> Timeline.merge (source, 1.0, 2.0, timelineMerger)

        result |> should beEquivalentTo expected
