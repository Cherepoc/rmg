namespace RMG.TestsF.Generate

open Xunit
open RMG.TestsF.Assertions
open RMG.CoreF

type RankTimeline() =
    [<Fact>]
    let zeroRank () =
        let maxRank : int = 0
        let phase : float = 0.0
        let period : float = 1.0
        let duration : float = 1.0
        let expected : int TimelineItem array = [| struct (0.0, 0) |]
        let result : int Timeline = Generate.rankTimeline maxRank phase period duration
        result |> should beEquivalentTo expected

    [<Fact>]
    let zeroRankHalfPhase () =
        let maxRank : int = 0
        let phase : float = 0.5
        let period : float = 1.0
        let duration : float = 1.0
        let expected : int TimelineItem array = [| struct (0.5, 0) |]
        let result : int Timeline = Generate.rankTimeline maxRank phase period duration
        result |> should beEquivalentTo expected

    [<Fact>]
    let zeroRankBiggerPhase () =
        let maxRank : int = 0
        let phase : float = 1.5
        let period : float = 1.0
        let duration : float = 1.0
        let expected : int TimelineItem array = [| struct (0.5, 0) |]
        let result : int Timeline = Generate.rankTimeline maxRank phase period duration
        result |> should beEquivalentTo expected

    [<Fact>]
    let zeroRankOutOfRage () =
        let maxRank : int = 0
        let phase : float = 1.0
        let period : float = 2.0
        let duration : float = 1.0
        let expected : int TimelineItem array = [||]
        let result : int Timeline = Generate.rankTimeline maxRank phase period duration
        result |> should beEquivalentTo expected

    [<Fact>]
    let zeroRankMultiCycle () =
        let maxRank : int = 0
        let phase : float = 0.5
        let period : float = 1.0
        let duration : float = 2.0
        let expected : int TimelineItem array = [| struct (0.5, 0); struct (1.5, 0) |]
        let result : int Timeline = Generate.rankTimeline maxRank phase period duration
        result |> should beEquivalentTo expected

    [<Fact>]
    let firstRank () =
        let maxRank : int = 1
        let phase : float = 0.0
        let period : float = 1.0
        let duration : float = 1.0
        let expected : int TimelineItem array = [| struct (0.0, 0); struct (0.5, 1) |]
        let result : int Timeline = Generate.rankTimeline maxRank phase period duration
        result |> should beEquivalentTo expected

    [<Fact>]
    let firstRankBiggerPhase () =
        let maxRank : int = 1
        let phase : float = 4.0
        let period : float = 1.0
        let duration : float = 1.0
        let expected : int TimelineItem array = [| struct (0.0, 0); struct (0.5, 1) |]
        let result : int Timeline = Generate.rankTimeline maxRank phase period duration
        result |> should beEquivalentTo expected

    [<Fact>]
    let firstRankMultiCycle () =
        let maxRank : int = 1
        let phase : float = 0.25
        let period : float = 0.5
        let duration : float = 1.5

        let expected : int TimelineItem array =
            [|
                struct (0.0, 1)
                struct (0.25, 0)
                struct (0.5, 1)
                struct (0.75, 0)
                struct (1.0, 1)
                struct (1.25, 0)
            |]

        let result : int Timeline = Generate.rankTimeline maxRank phase period duration
        result |> should beEquivalentTo expected

    [<Fact>]
    let secondRankMultiCycle () =
        let maxRank : int = 2
        let phase : float = 0.25
        let period : float = 1.0
        let duration : float = 1.5

        let expected : int TimelineItem array =
            [|
                struct (0.0, 2)
                struct (0.25, 0)
                struct (0.5, 2)
                struct (0.75, 1)
                struct (1.0, 2)
                struct (1.25, 0)
            |]

        let result : int Timeline = Generate.rankTimeline maxRank phase period duration
        result |> should beEquivalentTo expected
