namespace RMG.TestsF.Timeline

open RMG.CoreF
open Xunit
open RMG.TestsF.Assertions

type Blend() =
    let blender: TimelineBlender<int> =
        { Blend = (fun (i1, i2) -> i1 + i2)
          Default = 0 }

    [<Fact>]
    let emptyBoth () =
        let source = Seq.empty
        let dest = Seq.empty
        let expected = Seq.empty

        let result =
            dest
            |> Timeline.blendInto source { Position = 0.0; Duration = 1.0 } blender

        result |> should beEquivalentTo expected

    [<Fact>]
    let emptySource () =
        let source = seq { { Position = 0.0; Value = 1 } }
        let dest = Seq.empty

        let expected =
            [| { Position = 0.0; Value = 1 }
               { Position = 1.0; Value = 0 } |]

        let result =
            source
            |> Timeline.blendInto dest { Position = 0.0; Duration = 1.0 } blender

        result |> should beEquivalentTo expected

    [<Fact>]
    let emptySourceShift () =
        let source = seq { { Position = 0.0; Value = 1 } }
        let dest = Seq.empty

        let expected =
            [| { Position = 0.5; Value = 1 }
               { Position = 1.5; Value = 0 } |]

        let result =
            source
            |> Timeline.blendInto dest { Position = 0.5; Duration = 1.0 } blender

        result |> should beEquivalentTo expected

    [<Fact>]
    let emptyTarget () =
        let source = Seq.empty
        let dest = seq { { Position = 0.0; Value = 1 } }
        let expected = [| { Position = 0.0; Value = 1 } |]

        let result =
            source
            |> Timeline.blendInto dest { Position = 0.0; Duration = 1.0 } blender

        result |> should beEquivalentTo expected

    [<Fact>]
    let simpleCombine () =
        let source = seq { { Position = 0.0; Value = 1 } }
        let dest = seq { { Position = 0.0; Value = 1 } }

        let expected =
            seq {
                { Position = 0.0; Value = 2 }
                { Position = 1.0; Value = 1 }
            }

        let result =
            source
            |> Timeline.blendInto dest { Position = 0.0; Duration = 1.0 } blender

        result |> should beEquivalentTo expected

    [<Fact>]
    let combineSourceLarger () =
        let source = seq { { Position = 0.0; Value = 1 } }
        let dest = seq { { Position = 0.0; Value = 1 } }

        let expected =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 0.25; Value = 2 }
                { Position = 0.75; Value = 1 }
            }

        let result =
            source
            |> Timeline.blendInto dest { Position = 0.25; Duration = 0.5 } blender

        result |> should beEquivalentTo expected

    [<Fact>]
    let combineTargetLarger () =
        let source = seq { { Position = 0.0; Value = 1 } }
        let dest = seq { { Position = 1.0; Value = 1 } }

        let expected =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 1.0; Value = 2 }
                { Position = 2.0; Value = 1 }
            }

        let result =
            source
            |> Timeline.blendInto dest { Position = 0.0; Duration = 2.0 } blender

        result |> should beEquivalentTo expected

    [<Fact>]
    let combineComplex () =
        let source =
            seq {
                { Position = 0.5; Value = 1 }
                { Position = 1.5; Value = 2 }
            }

        let dest =
            seq {
                { Position = 1.0; Value = 1 }
                { Position = 2.0; Value = 2 }
                { Position = 3.0; Value = 0 }
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
            source
            |> Timeline.blendInto dest { Position = 1.0; Duration = 2.0 } blender

        result |> should beEquivalentTo expected


    [<Fact>]
    let combineComplexMultiBlend () =
        let source =
            seq {
                { Position = 0.5; Value = 1 }
                { Position = 0.0; Value = 1 }
                { Position = 0.0; Value = 1 }
                { Position = 1.5; Value = 2 }
            }

        let dest =
            seq {
                { Position = 1.0; Value = 1 }
                { Position = 1.0; Value = 1 }
                { Position = 2.0; Value = 2 }
                { Position = 3.0; Value = 0 }
            }
        let expected =
            seq {
                { Position = 1.0; Value = 4 }
                { Position = 1.5; Value = 3 }
                { Position = 2.5; Value = 4 }
                { Position = 3.0; Value = 0 }
            }

        let result =
            source
            |> Timeline.blendInto dest { Position = 1.0; Duration = 2.0 } blender

        result |> should beEquivalentTo expected
