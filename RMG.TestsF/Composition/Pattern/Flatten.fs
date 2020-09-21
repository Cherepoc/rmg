namespace RMG.TestsF.Composition.Pattern

open RMG.CoreF
open RMG.CoreF.Composition
open Xunit
open RMG.TestsF.Assertions

type Flatten() =
    let combine = Timeline.union

    [<Fact>]
    let empty () =
        let input = Pattern.empty
        let expected = Seq.empty
        let result = input |> Pattern.flatten (1.0, combine)
        result |> should beEquivalentTo expected

    [<Fact>]
    let simpleTimeline () =
        let input =
            { Duration = 1.0
              Timeline = seq { { Position = 0.0; Value = 1 } }
              PatternTimeline = Seq.empty }

        let expected = seq { { Position = 0.0; Value = 1 } }
        let result = input |> Pattern.flatten (1.0, combine)
        result |> should beEquivalentTo expected

    [<Fact>]
    let simpleInnerPattern () =
        let input =
            { Duration = 1.0
              Timeline = Seq.empty
              PatternTimeline =
                  seq {
                      { Position = 0.0
                        Value =
                            { Duration = 1.0
                              Timeline = seq { { Position = 0.0; Value = 1 } }
                              PatternTimeline = Seq.empty } }
                  } }

        let expected = seq { { Position = 0.0; Value = 1 } }
        let result = input |> Pattern.flatten (1.0, combine)
        result |> should beEquivalentTo expected

    [<Fact>]
    let multiInnerPattern () =
        let input =
            { Duration = 1.0
              Timeline = seq { { Position = 0.0; Value = 1 } }
              PatternTimeline =
                  seq {
                      { Position = 0.25
                        Value =
                            { Duration = 1.0
                              Timeline = seq { { Position = 0.0; Value = 2 } }
                              PatternTimeline =
                                  seq {
                                      { Position = 0.25
                                        Value =
                                            { Duration = 1.0
                                              Timeline = seq { { Position = 0.0; Value = 3 } }
                                              PatternTimeline = Seq.empty } }
                                      { Position = 0.5
                                        Value =
                                            { Duration = 1.0
                                              Timeline = seq { { Position = 0.0; Value = 4 } }
                                              PatternTimeline = Seq.empty } }
                                  } } }
                      { Position = 0.75
                        Value =
                            { Duration = 1.0
                              Timeline =
                                  seq {
                                      { Position = 0.0; Value = 5 }
                                      { Position = 0.25; Value = 6 }
                                  }
                              PatternTimeline = Seq.empty } }
                  } }

        let expected =
            seq {
                { Position = 0.0; Value = 1 }
                { Position = 0.25; Value = 2 }
                { Position = 0.5; Value = 3 }
                { Position = 0.75; Value = 5 }
            }

        let result = input |> Pattern.flatten (1.0, combine)
        result |> should beEquivalentTo expected
