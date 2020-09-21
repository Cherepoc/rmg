namespace RMG.TestsF.Composition.Pattern

open RMG.CoreF
open RMG.CoreF.Composition
open Xunit
open RMG.TestsF.Assertions

type Map() =
    let map x = x * 2

    [<Fact>]
    let empty () =
        let input = Pattern.empty
        let expected = Pattern.empty
        let result = input |> Pattern.map map
        result |> should beEquivalentTo expected

    [<Fact>]
    let recursive () =
        let input =
            { Duration = 1.0
              Timeline = seq { { Position = 0.0; Value = 1 } }
              PatternTimeline =
                  seq {
                      { Position = 0.0
                        Value =
                            { Duration = 1.0
                              Timeline = seq { { Position = 0.0; Value = 2 } }
                              PatternTimeline = Seq.empty } }
                  } }

        let expected =
            { Duration = 1.0
              Timeline = seq { { Position = 0.0; Value = 2 } }
              PatternTimeline =
                  seq {
                      { Position = 0.0
                        Value =
                            { Duration = 1.0
                              Timeline = seq { { Position = 0.0; Value = 4 } }
                              PatternTimeline = Seq.empty } }
                  } }

        let result = input |> Pattern.map map
        result |> should beEquivalentTo expected
