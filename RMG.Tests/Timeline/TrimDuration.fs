namespace RMG.TestsF.Timeline

open Xunit
open RMG.TestsF.Assertions
open RMG.CoreF

type TrimDuration() =
    [<Fact>]
    let empty () =
        let input : int Timeline = Seq.empty |> Timeline.ofSeq
        let expected : int TimelineItem array = [||]
        let result : int Timeline = input |> Timeline.trimDuration 1.0
        result |> should beEquivalentTo expected

    [<Fact>]
    let singleExact () =
        let input : int Timeline = seq {struct(1.0, 1)} |> Timeline.ofSeq
        let expected : int TimelineItem array = [||]
        let result : int Timeline = input |> Timeline.trimDuration 1.0
        result |> should beEquivalentTo expected
