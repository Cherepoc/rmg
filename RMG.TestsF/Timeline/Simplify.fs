namespace RMG.TestsF.Timeline

open RMG.CoreF
open Xunit
open RMG.TestsF.Assertions

type Simplify() =
    let blender: TimelineBlender<int> =
        { Blend = fun (x, y) -> x + y
          Default = 0 }

    [<Fact>]
    let empty () =
        let input = Seq.empty
        let expected = Seq.empty
        let result = input |> Timeline.simplify blender
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleDefault () =
        let input = seq { { Position = 0.0; Value = 0 } }
        let expected = Seq.empty
        let result = input |> Timeline.simplify blender
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleNonDefault () =
        let input = seq { { Position = 0.0; Value = 1 } }
        let expected = seq { { Position = 0.0; Value = 1 } }
        let result = input |> Timeline.simplify blender
        result |> should beEquivalentTo expected

    [<Fact>]
    let defaultStart () =
        let input =
            seq {
                { Position = 0.0; Value = 0 }
                { Position = 1.0; Value = 1 }
            }

        let expected = seq { { Position = 1.0; Value = 1 } }
        let result = input |> Timeline.simplify blender
        result |> should beEquivalentTo expected

    [<Fact>]
    let double () =
        let input =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 1.0; Value = 1 }
            }

        let expected = seq { { Position = 0.0; Value = 1 } }
        let result = input |> Timeline.simplify blender
        result |> should beEquivalentTo expected

    [<Fact>]
    let multiDouble () =
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

        let result = input |> Timeline.simplify blender
        result |> should beEquivalentTo expected

    [<Fact>]
    let blendSingle () =
        let input =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 0.0; Value = 2 }
            }

        let expected =
            seq {
                { Position = 0.0; Value = 3 }
            }

        let result = input |> Timeline.simplify blender
        result |> should beEquivalentTo expected

    [<Fact>]
    let blendIntoDefaultStart () =
        let input =
            seq {
                { Position = 0.0; Value = -1 }
                { Position = 0.0; Value = 1 }
                { Position = 1.0; Value = 1 }
            }

        let expected =
            seq {
                { Position = 1.0; Value = 1 }
            }

        let result = input |> Timeline.simplify blender
        result |> should beEquivalentTo expected

    [<Fact>]
    let blendInterleaved () =
        let input =
            seq {
                { Position = 0.0; Value = -1 }
                { Position = 0.0; Value = 1 }
                { Position = 1.0; Value = 1 }
                { Position = 2.0; Value = 1 }
                { Position = 3.0; Value = 1 }
                { Position = 3.0; Value = -1 }
            }

        let expected =
            seq {
                { Position = 1.0; Value = 1 }
                { Position = 3.0; Value = 0 }
            }

        let result = input |> Timeline.simplify blender
        result |> should beEquivalentTo expected
