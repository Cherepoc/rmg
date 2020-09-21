namespace RMG.TestsF.Music

open RMG.CoreF
open RMG.TestsF.Assertions
open Xunit

type MergeNoteOffsetTimelineMaps() =
    [<Fact>]
    let works () =
        let dest: NoteOffsetTimelineMap =
            { Duration = seq { { Position = 0.0; Value = 0.0 } }
              KeyOffset = seq { { Position = 0.0; Value = 0 } }
              OctaveOffset = seq { { Position = 0.0; Value = 1 } }
              ScaleOffset = seq { { Position = 0.0; Value = 2 } }
              Volume = seq { { Position = 0.0; Value = 2.0 } } }

        let source: NoteOffsetTimelineMap =
            { Duration = seq { { Position = 0.0; Value = 2.0 } }
              KeyOffset = seq { { Position = 0.0; Value = 1 } }
              OctaveOffset = seq { { Position = 0.0; Value = 2 } }
              ScaleOffset = seq { { Position = 0.0; Value = 3 } }
              Volume = seq { { Position = 0.0; Value = 3.0 } } }

        let expected: NoteOffsetTimelineMap =
            { Duration = seq { { Position = 0.0; Value = 0.0 } }
              KeyOffset =
                  seq {
                      { Position = 1.0; Value = 1 }
                      { Position = 2.0; Value = 0 }
                  }
              OctaveOffset =
                  seq {
                      { Position = 0.0; Value = 1 }
                      { Position = 1.0; Value = 3 }
                      { Position = 2.0; Value = 1 }
                  }
              ScaleOffset =
                  seq {
                      { Position = 0.0; Value = 2 }
                      { Position = 1.0; Value = 5 }
                      { Position = 2.0; Value = 2 }
                  }
              Volume =
                  seq {
                      { Position = 0.0; Value = 2.0 }
                      { Position = 1.0; Value = 6.0 }
                      { Position = 2.0; Value = 2.0 }
                  } }

        let result =
            dest
            |> NoteOffsetTimelineMap.merge (source, 1.0, 1.0)

        result |> should beEquivalentTo expected
