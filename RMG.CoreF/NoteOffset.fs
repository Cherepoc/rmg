namespace RMG.CoreF

open RMG.CoreF

type KeyOffset = int
type OctaveOffset = int
type ScaleOffset = int
type Volume = double

type NoteOffsetOld =
    { Duration: Duration
      KeyOffset: KeyOffset
      OctaveOffset: OctaveOffset
      ScaleOffset: ScaleOffset
      Volume: Volume }

[<NoEquality>]
[<NoComparison>]
type NoteOffsetTimelineMap =
    { Duration: Timeline<Duration>
      KeyOffset: Timeline<KeyOffset>
      OctaveOffset: Timeline<OctaveOffset>
      ScaleOffset: Timeline<ScaleOffset>
      Volume: Timeline<Volume> }

[<NoEquality>]
[<NoComparison>]
type NoteOffsetBasedTimeline<'T> =
    { Timeline: Timeline<'T>
      NoteOffsetTimelineMap: NoteOffsetTimelineMap }

type TrackNoteOffsetTimelineMap<'T> = Map<uint, NoteOffsetBasedTimeline<'T>>

module NoteOffsetTimelineMap =
    module Blender =
        let duration: TimelineBlender<Duration> = TimelineBlender.multiplicative
        let keyOffset: TimelineBlender<KeyOffset> = TimelineBlender.additive
        let octaveOffset: TimelineBlender<OctaveOffset> = TimelineBlender.additive
        let scaleOffset: TimelineBlender<ScaleOffset> = TimelineBlender.additive
        let volume: TimelineBlender<Volume> = TimelineBlender.multiplicative

    let empty: NoteOffsetTimelineMap =
        { Duration = Timeline.empty
          KeyOffset = Timeline.empty
          OctaveOffset = Timeline.empty
          ScaleOffset = Timeline.empty
          Volume = Timeline.empty }

    let blendInto (dest: NoteOffsetTimelineMap) (vector: Vector) (source: NoteOffsetTimelineMap): NoteOffsetTimelineMap =
        { Duration =
              source.Duration
              |> Timeline.blendInto dest.Duration vector Blender.duration
              |> Timeline.fromSequence
          KeyOffset =
              source.KeyOffset
              |> Timeline.blendInto dest.KeyOffset vector Blender.keyOffset
              |> Timeline.fromSequence
          OctaveOffset =
              source.OctaveOffset
              |> Timeline.blendInto dest.OctaveOffset vector Blender.octaveOffset
              |> Timeline.fromSequence
          ScaleOffset =
              source.ScaleOffset
              |> Timeline.blendInto dest.ScaleOffset vector Blender.scaleOffset
              |> Timeline.fromSequence
          Volume =
              source.Volume
              |> Timeline.blendInto dest.Volume vector Blender.volume
              |> Timeline.fromSequence }

    let flatten (duration: Duration) (timeline: TimelineLike<NoteOffsetTimelineMap>): NoteOffsetTimelineMap =
        timeline
        |> Timeline.aggregate duration blendInto empty

module NoteOffsetBasedTimeline =
    let empty<'T> : NoteOffsetBasedTimeline<'T> =
        { Timeline = Timeline.empty
          NoteOffsetTimelineMap = NoteOffsetTimelineMap.empty }

    let blendInto<'T when 'T: equality> (dest: NoteOffsetBasedTimeline<'T>)
                                        (vector: Vector)
                                        (combineInto: TimelineCombineFunction<'T>)
                                        (source: NoteOffsetBasedTimeline<'T>)
                                        : NoteOffsetBasedTimeline<'T> =
        { Timeline =
              source.Timeline
              |> combineInto dest.Timeline vector
              |> Timeline.fromSequence
          NoteOffsetTimelineMap =
              source.NoteOffsetTimelineMap
              |> NoteOffsetTimelineMap.blendInto dest.NoteOffsetTimelineMap vector }

    let flatten<'T when 'T: equality> (duration: Duration)
                                      (combineInto: TimelineCombineFunction<'T>)
                                      (timeline: TimelineLike<NoteOffsetBasedTimeline<'T>>)
                                      : NoteOffsetBasedTimeline<'T> =
        timeline
        |> Timeline.aggregate duration (fun dest vector -> blendInto dest vector combineInto) empty
