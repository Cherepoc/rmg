namespace RMG.TestsF

open RMG.CoreF
open RMG.CoreF.Composition
open Xunit
open RMG.TestsF.Assertions

type NoteOffsetStatePatternMap() =
    [<Fact>]
    let empty () =
        let input =
            { NoteOffsetStatePatternMapInput.DurationPattern = StatePattern.empty
              NoteOffsetStatePatternMapInput.KeyOffsetPattern = StatePattern.empty
              NoteOffsetStatePatternMapInput.OctaveOffsetPattern = StatePattern.empty
              NoteOffsetStatePatternMapInput.ScaleOffsetPattern = StatePattern.empty
              NoteOffsetStatePatternMapInput.VolumePattern = StatePattern.empty }

        let expected =
            {| DurationTimeline = Seq.empty
               KeyOffsetTimeline = Seq.empty
               OctaveOffsetTimeline = Seq.empty
               ScaleOffsetTimeline = Seq.empty
               VolumeTimeline = Seq.empty |}

        let result =
            input |> NoteOffsetStatePatternMap.fromInput 1.0

        result.FlatTimelineMap
        |> should beEquivalentTo expected

    [<Fact>]
    let recursive () =
        let input =
            { NoteOffsetStatePatternMapInput.DurationPattern =
                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1.0 } }
                    StatePatternInput.PatternTimelineInput =
                        seq {
                            { Position = 0.5
                              Value =
                                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 2.0 } }
                                    StatePatternInput.PatternTimelineInput = Seq.empty }
                                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.duration }
                        } }
                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.duration
              NoteOffsetStatePatternMapInput.KeyOffsetPattern =
                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1 } }
                    StatePatternInput.PatternTimelineInput =
                        seq {
                            { Position = 0.5
                              Value =
                                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 2 } }
                                    StatePatternInput.PatternTimelineInput = Seq.empty }
                                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.keyOffset }
                        } }
                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.keyOffset
              NoteOffsetStatePatternMapInput.OctaveOffsetPattern =
                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1 } }
                    StatePatternInput.PatternTimelineInput =
                        seq {
                            { Position = 0.5
                              Value =
                                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 2 } }
                                    StatePatternInput.PatternTimelineInput = Seq.empty }
                                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.octaveOffset }
                        } }
                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.octaveOffset
              NoteOffsetStatePatternMapInput.ScaleOffsetPattern =
                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1.0 } }
                    StatePatternInput.PatternTimelineInput =
                        seq {
                            { Position = 0.5
                              Value =
                                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 2.0 } }
                                    StatePatternInput.PatternTimelineInput = Seq.empty }
                                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.scaleOffset }
                        } }
                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.scaleOffset
              NoteOffsetStatePatternMapInput.VolumePattern =
                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 1.0 } }
                    StatePatternInput.PatternTimelineInput =
                        seq {
                            { Position = 0.5
                              Value =
                                  { StatePatternInput.TimelineInput = seq { { Position = 0.0; Value = 2.0 } }
                                    StatePatternInput.PatternTimelineInput = Seq.empty }
                                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.volume }
                        } }
                  |> StatePattern.fromInput 1.0 NoteOffsetStateTimelineMap.Merger.volume }

        let expected =
            {| DurationTimeline =
                   seq {
                       { Position = 0.5; Value = 2.0 }
                       { Position = 1.5; Value = 1.0 }
                   }
               KeyOffsetTimeline =
                   seq {
                       { Position = 0.0; Value = 1 }
                       { Position = 0.5; Value = 3 }
                       { Position = 1.0; Value = 2 }
                       { Position = 1.5; Value = 0 }
                   }
               OctaveOffsetTimeline =
                   seq {
                       { Position = 0.0; Value = 1 }
                       { Position = 0.5; Value = 3 }
                       { Position = 1.0; Value = 2 }
                       { Position = 1.5; Value = 0 }
                   }
               ScaleOffsetTimeline =
                   seq {
                       { Position = 0.0; Value = 1 }
                       { Position = 0.5; Value = 3 }
                       { Position = 1.0; Value = 2 }
                       { Position = 1.5; Value = 0 }
                   }
               VolumeTimeline =
                   seq {
                       { Position = 0.5; Value = 2.0 }
                       { Position = 1.5; Value = 1.0 }
                   } |}

        let result =
            input |> NoteOffsetStatePatternMap.fromInput 1.0

        result.FlatTimelineMap
        |> should beEquivalentTo expected
