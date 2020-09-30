namespace RMG.TestsF.Music

open RMG.CoreF
open RMG.TestsF.Assertions
open Xunit

type MergeNoteOffsetTimelineMaps() =
    [<Fact>]
    let works () =
        let dest: NoteOffsetTimelineMap =
            { Duration =
                  seq { { Position = 0.0; Value = 0.0 } }
                  |> Timeline.fromSequence
              KeyOffset =
                  seq { { Position = 0.0; Value = 0 } }
                  |> Timeline.fromSequence
              OctaveOffset =
                  seq { { Position = 0.0; Value = 1 } }
                  |> Timeline.fromSequence
              ScaleOffset =
                  seq { { Position = 0.0; Value = 2 } }
                  |> Timeline.fromSequence
              Volume =
                  seq { { Position = 0.0; Value = 2.0 } }
                  |> Timeline.fromSequence }

        let source: NoteOffsetTimelineMap =
            { Duration =
                  seq { { Position = 0.0; Value = 2.0 } }
                  |> Timeline.fromSequence
              KeyOffset =
                  seq { { Position = 0.0; Value = 1 } }
                  |> Timeline.fromSequence
              OctaveOffset =
                  seq { { Position = 0.0; Value = 2 } }
                  |> Timeline.fromSequence
              ScaleOffset =
                  seq { { Position = 0.0; Value = 3 } }
                  |> Timeline.fromSequence
              Volume =
                  seq { { Position = 0.0; Value = 3.0 } }
                  |> Timeline.fromSequence }

        let expected: NoteOffsetTimelineMap =
            { Duration =
                  seq { { Position = 0.0; Value = 0.0 } }
                  |> Timeline.fromSequence
              KeyOffset =
                  seq {
                      { Position = 1.0; Value = 1 }
                      { Position = 2.0; Value = 0 }
                  }
                  |> Timeline.fromSequence
              OctaveOffset =
                  seq {
                      { Position = 0.0; Value = 1 }
                      { Position = 1.0; Value = 3 }
                      { Position = 2.0; Value = 1 }
                  }
                  |> Timeline.fromSequence
              ScaleOffset =
                  seq {
                      { Position = 0.0; Value = 2 }
                      { Position = 1.0; Value = 5 }
                      { Position = 2.0; Value = 2 }
                  }
                  |> Timeline.fromSequence
              Volume =
                  seq {
                      { Position = 0.0; Value = 2.0 }
                      { Position = 1.0; Value = 6.0 }
                      { Position = 2.0; Value = 2.0 }
                  }
                  |> Timeline.fromSequence }

        let result =
            source
            |> NoteOffsetTimelineMap.blendInto dest { Position = 1.0; Duration = 1.0 }

        result |> should beEquivalentTo expected
