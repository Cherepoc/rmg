namespace RMG.TestsF.Composition.Pattern

open RMG.CoreF
open RMG.CoreF.Composition
open Xunit
open RMG.TestsF.Assertions

type FlattenAggregate() =
    let combine (source: Timeline<int>, position: Position, duration: Duration): Timeline<int> -> Timeline<int> =
        Timeline.merge (source, position, duration, Timeline.Merger.additive)

    [<Fact>]
    let empty () =
        let input = Pattern.empty
        let expected = Seq.empty

        let result =
            input
            |> Pattern.flattenAggregate (1.0, combine) Seq.empty

        result |> should beEquivalentTo expected

    [<Fact>]
    let timeline () =
        let input =
            { Duration = 1.0
              Timeline =
                  seq {
                      { Position = 0.0
                        Value = seq { { Position = 0.0; Value = 1 } } }
                      { Position = 0.5
                        Value = seq { { Position = 0.0; Value = 2 } } }
                  }
              PatternTimeline = Seq.empty }

        let expected = seq {
            { Position = 0.0; Value = 1 }
            { Position = 0.5; Value = 2 }
            { Position = 1.0; Value = 0 }
        }

        let result =
            input
            |> Pattern.flattenAggregate (1.0, combine) Seq.empty

        result |> should beEquivalentTo expected

    [<Fact>]
    let recursive () =
        let input =
            { Duration = 1.0
              Timeline =
                  seq {
                      { Position = 0.0
                        Value = seq { { Position = 0.0; Value = 1 } } }
                  }
              PatternTimeline =
                  seq {
                      { Position = 0.5
                        Value =
                            { Duration = 1.0
                              Timeline =
                                  seq {
                                      { Position = 0.0
                                        Value = seq { { Position = 0.0; Value = 2 } } }
                                  }
                              PatternTimeline = Seq.empty } }
                  } }

        let expected = seq {
            { Position = 0.0; Value = 1 }
            { Position = 0.5; Value = 3 }
            { Position = 1.0; Value = 0 }
        }

        let result =
            input
            |> Pattern.flattenAggregate (1.0, combine) Seq.empty

        result |> should beEquivalentTo expected
