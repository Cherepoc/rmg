namespace RMG.CoreF

[<NoEquality>]
[<NoComparison>]
type NoteOffsetStateTimelineMap =
    { DurationTimeline: StateTimeline<Duration>
      KeyOffsetTimeline: StateTimeline<KeyOffset>
      OctaveOffsetTimeline: StateTimeline<OctaveOffset>
      ScaleOffsetTimeline: StateTimeline<ScaleOffset>
      VolumeTimeline: StateTimeline<Volume> }

[<NoEquality>]
[<NoComparison>]
type NoteOffsetStateTimelineMapInput =
    { DurationTimelineInput: Timeline<Duration>
      KeyOffsetTimelineInput: Timeline<KeyOffset>
      OctaveOffsetTimelineInput: Timeline<OctaveOffset>
      ScaleOffsetTimelineInput: Timeline<ScaleOffset>
      VolumeTimelineInput: Timeline<Volume> }

module NoteOffsetStateTimelineMap =
    module Merger =
        let duration: StateMerger<Duration> = StateMerger.multiplicative
        let keyOffset: StateMerger<KeyOffset> = StateMerger.additive
        let octaveOffset: StateMerger<OctaveOffset> = StateMerger.additive
        let scaleOffset: StateMerger<ScaleOffset> = StateMerger.additive
        let volume: StateMerger<Volume> = StateMerger.multiplicative

    let empty: NoteOffsetStateTimelineMap =
        { DurationTimeline = StateTimeline.empty
          KeyOffsetTimeline = StateTimeline.empty
          OctaveOffsetTimeline = StateTimeline.empty
          ScaleOffsetTimeline = StateTimeline.empty
          VolumeTimeline = StateTimeline.empty }

    let fromInput (duration: Duration) (input: NoteOffsetStateTimelineMapInput): NoteOffsetStateTimelineMap =
        { DurationTimeline =
              input.DurationTimelineInput
              |> StateTimeline.fromSequence duration Merger.duration
          KeyOffsetTimeline =
              input.KeyOffsetTimelineInput
              |> StateTimeline.fromSequence duration Merger.keyOffset
          OctaveOffsetTimeline =
              input.OctaveOffsetTimelineInput
              |> StateTimeline.fromSequence duration Merger.octaveOffset
          ScaleOffsetTimeline =
              input.ScaleOffsetTimelineInput
              |> StateTimeline.fromSequence duration Merger.scaleOffset
          VolumeTimeline =
              input.VolumeTimelineInput
              |> StateTimeline.fromSequence duration Merger.volume }

    let merge (inputTimeline: Timeline<NoteOffsetStateTimelineMap>): NoteOffsetStateTimelineMap =
        let array = inputTimeline |> Seq.toArray

        let mergeStateTimeline timelineFunc stateMerger =
            array
            |> Timeline.map timelineFunc
            |> StateTimeline.merge stateMerger

        { DurationTimeline = mergeStateTimeline (fun x -> x.DurationTimeline) Merger.duration
          KeyOffsetTimeline = mergeStateTimeline (fun x -> x.KeyOffsetTimeline) Merger.keyOffset
          OctaveOffsetTimeline = mergeStateTimeline (fun x -> x.OctaveOffsetTimeline) Merger.octaveOffset
          ScaleOffsetTimeline = mergeStateTimeline (fun x -> x.ScaleOffsetTimeline) Merger.scaleOffset
          VolumeTimeline = mergeStateTimeline (fun x -> x.VolumeTimeline) Merger.volume }
