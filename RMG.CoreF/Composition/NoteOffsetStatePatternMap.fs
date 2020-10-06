namespace RMG.CoreF.Composition

open RMG.CoreF

[<Sealed>]
type NoteOffsetStatePatternMap private (duration: Duration,
                                        durationPattern: StatePattern<Duration>,
                                        keyOffsetPattern: StatePattern<KeyOffset>,
                                        octavePattern: StatePattern<OctaveOffset>,
                                        scalePattern: StatePattern<ScaleOffset>,
                                        volumePattern: StatePattern<Volume>,
                                        flatTimelineMap: NoteOffsetStateTimelineMap) =
    member this.Duration = duration
    member this.DurationPattern = durationPattern
    member this.KeyOffsetPattern = keyOffsetPattern
    member this.OctaveOffsetPattern = octavePattern
    member this.ScaleOffsetPattern = scalePattern
    member this.VolumePattern = volumePattern

    member this.FlatTimelineMap = flatTimelineMap

    new(duration: Duration,
        durationPattern: StatePattern<Duration>,
        keyOffsetPattern: StatePattern<KeyOffset>,
        octavePattern: StatePattern<OctaveOffset>,
        scalePattern: StatePattern<ScaleOffset>,
        volumePattern: StatePattern<Volume>) =
        let flatTimelineMap =
            { DurationTimeline = durationPattern.FlatTimeline
              KeyOffsetTimeline = keyOffsetPattern.FlatTimeline
              OctaveOffsetTimeline = octavePattern.FlatTimeline
              ScaleOffsetTimeline = scalePattern.FlatTimeline
              VolumeTimeline = volumePattern.FlatTimeline }

        NoteOffsetStatePatternMap
            (duration, durationPattern, keyOffsetPattern, octavePattern, scalePattern, volumePattern, flatTimelineMap)

    new() =
        NoteOffsetStatePatternMap
            (0.0,
             StatePattern.empty,
             StatePattern.empty,
             StatePattern.empty,
             StatePattern.empty,
             StatePattern.empty,
             NoteOffsetStateTimelineMap.empty)

[<NoEquality>]
[<NoComparison>]
type NoteOffsetStatePatternMapInput =
    { DurationPattern: StatePattern<Duration>
      KeyOffsetPattern: StatePattern<KeyOffset>
      OctaveOffsetPattern: StatePattern<OctaveOffset>
      ScaleOffsetPattern: StatePattern<ScaleOffset>
      VolumePattern: StatePattern<Volume> }

[<NoEquality>]
[<NoComparison>]
type NoteOffsetStatePatternMapSourceInput =
    { DurationPatternInput: StatePatternInput<Duration>
      KeyOffsetPatternInput: StatePatternInput<KeyOffset>
      OctaveOffsetPatternInput: StatePatternInput<OctaveOffset>
      ScaleOffsetPatternInput: StatePatternInput<ScaleOffset>
      VolumePatternInput: StatePatternInput<Volume> }

module NoteOffsetStatePatternMap =
    let empty = NoteOffsetStatePatternMap()

    let fromInput (duration: Duration) (input: NoteOffsetStatePatternMapInput) =
        NoteOffsetStatePatternMap
            (duration,
             input.DurationPattern,
             input.KeyOffsetPattern,
             input.OctaveOffsetPattern,
             input.ScaleOffsetPattern,
             input.VolumePattern)

    let fromSourceInput (duration: Duration) (input: NoteOffsetStatePatternMapSourceInput) =
        NoteOffsetStatePatternMap
            (duration,
             input.DurationPatternInput
             |> StatePattern.fromInput duration NoteOffsetStateTimelineMap.Merger.duration,
             input.KeyOffsetPatternInput
             |> StatePattern.fromInput duration NoteOffsetStateTimelineMap.Merger.keyOffset,
             input.OctaveOffsetPatternInput
             |> StatePattern.fromInput duration NoteOffsetStateTimelineMap.Merger.octaveOffset,
             input.ScaleOffsetPatternInput
             |> StatePattern.fromInput duration NoteOffsetStateTimelineMap.Merger.scaleOffset,
             input.VolumePatternInput
             |> StatePattern.fromInput duration NoteOffsetStateTimelineMap.Merger.volume)
