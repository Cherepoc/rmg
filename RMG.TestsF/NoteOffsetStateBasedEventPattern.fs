namespace RMG.TestsF

open RMG.CoreF
open RMG.CoreF.Composition
open Xunit
open RMG.TestsF.Assertions

type NoteOffsetStateBasedEventPattern() =
    [<Fact>]
    let empty () =
        let input =
            { PatternTimelineInput = Seq.empty
              NoteOffsetStatePatternMapPatternTimelineInput = Seq.empty
              NoteOffsetBasedPatternTimelineInput = Seq.empty }

        let expected =
            {| Timeline = Seq.empty
               NoteOffsetStateTimelineMap =
                   {| DurationTimeline = Seq.empty
                      KeyOffsetTimeline = Seq.empty
                      OctaveOffsetTimeline = Seq.empty
                      ScaleOffsetTimeline = Seq.empty
                      VolumeTimeline = Seq.empty |} |}

        let result =
            input
            |> NoteOffsetStateBasedEventPattern.fromInput 1.0

        result.FlatTimeline
        |> should beEquivalentTo expected

    [<Fact>]
    let recursive () =
        let noteOffsetStatePatternMap =
            { NoteOffsetStatePatternMapInput.DurationPattern =
                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1.0 } }
                    StatePatternInput.PatternTimelineInput = Seq.empty }
                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.duration
              NoteOffsetStatePatternMapInput.KeyOffsetPattern =
                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1 } }
                    StatePatternInput.PatternTimelineInput = Seq.empty }
                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.keyOffset
              NoteOffsetStatePatternMapInput.OctaveOffsetPattern =
                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1 } }
                    StatePatternInput.PatternTimelineInput = Seq.empty }
                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.octaveOffset
              NoteOffsetStatePatternMapInput.ScaleOffsetPattern =
                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1 } }
                    StatePatternInput.PatternTimelineInput = Seq.empty }
                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.scaleOffset
              NoteOffsetStatePatternMapInput.VolumePattern =
                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1.0 } }
                    StatePatternInput.PatternTimelineInput = Seq.empty }
                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.volume }
            |> NoteOffsetStatePatternMap.fromInput 1.0

        let innerPattern =
            { PatternTimelineInput =
                  seq {
                      { Position = 0.0
                        Value =
                            { TimelineInput = seq { { Position = 0.0; Value = 1 } }
                              PatternTimelineInput = Seq.empty }
                            |> EventPattern.fromInput 1.0 }
                  }
              NoteOffsetStatePatternMapPatternTimelineInput =
                  seq {
                      { Position = 0.0
                        Value =
                            { TimelineInput =
                                  seq {
                                      { Position = 0.0
                                        Value = noteOffsetStatePatternMap }
                                  }
                              PatternTimelineInput = Seq.empty }
                            |> EventPattern.fromInput 1.0 }
                  }
              NoteOffsetBasedPatternTimelineInput = Seq.empty }
            |> NoteOffsetStateBasedEventPattern.fromInput 1.0

        let result =
            { PatternTimelineInput =
                  seq {
                      { Position = 0.0
                        Value =
                            { TimelineInput = seq { { Position = 0.0; Value = 2 } }
                              PatternTimelineInput = Seq.empty }
                            |> EventPattern.fromInput 1.0 }
                  }
              NoteOffsetStatePatternMapPatternTimelineInput =
                  seq {
                      { Position = 0.0
                        Value =
                            { TimelineInput =
                                  seq {
                                      { Position = 0.0
                                        Value = noteOffsetStatePatternMap }
                                  }
                              PatternTimelineInput = Seq.empty }
                            |> EventPattern.fromInput 1.0 }
                  }
              NoteOffsetBasedPatternTimelineInput = seq { { Position = 0.5; Value = innerPattern } } }
            |> NoteOffsetStateBasedEventPattern.fromInput 1.0

        let expected =
            {| Timeline =
                   seq {
                       { Position = 0.0; Value = 2 }
                       { Position = 0.5; Value = 1 }
                   }
               NoteOffsetStateTimelineMap =
                   {| DurationTimeline = Seq.empty
                      KeyOffsetTimeline =
                          seq {
                              { Position = 0.0; Value = 1 }
                              { Position = 0.5; Value = 2 }
                              { Position = 1.0; Value = 1 }
                              { Position = 1.5; Value = 0 }
                          }
                      OctaveOffsetTimeline =
                          seq {
                              { Position = 0.0; Value = 1 }
                              { Position = 0.5; Value = 2 }
                              { Position = 1.0; Value = 1 }
                              { Position = 1.5; Value = 0 }
                          }
                      ScaleOffsetTimeline =
                          seq {
                              { Position = 0.0; Value = 1 }
                              { Position = 0.5; Value = 2 }
                              { Position = 1.0; Value = 1 }
                              { Position = 1.5; Value = 0 }
                          }
                      VolumeTimeline = Seq.empty |} |}

        result.FlatTimeline
        |> should beEquivalentTo expected
