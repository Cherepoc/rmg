namespace RMG.CoreF

type KeyOffset = int
type OctaveOffset = int
type ScaleOffset = int
type Volume = double

type NoteOffset =
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
    module Merger =
        let duration: TimelineMerger<Duration> = Timeline.Merger.multiplicative
        let keyOffset: TimelineMerger<KeyOffset> = Timeline.Merger.additive
        let octaveOffset: TimelineMerger<OctaveOffset> = Timeline.Merger.additive
        let scaleOffset: TimelineMerger<ScaleOffset> = Timeline.Merger.additive
        let volume: TimelineMerger<Volume> = Timeline.Merger.multiplicative

    let empty: NoteOffsetTimelineMap =
        { Duration = Seq.empty
          KeyOffset = Seq.empty
          OctaveOffset = Seq.empty
          ScaleOffset = Seq.empty
          Volume = Seq.empty }

    let merge (source: NoteOffsetTimelineMap, position: Position, duration: Duration)
              (dest: NoteOffsetTimelineMap)
              : NoteOffsetTimelineMap =
        { Duration =
              dest.Duration
              |> Timeline.merge (source.Duration, position, duration, Merger.duration)
          KeyOffset =
              dest.KeyOffset
              |> Timeline.merge (source.KeyOffset, position, duration, Merger.keyOffset)
          OctaveOffset =
              dest.OctaveOffset
              |> Timeline.merge (source.OctaveOffset, position, duration, Merger.octaveOffset)
          ScaleOffset =
              dest.ScaleOffset
              |> Timeline.merge (source.ScaleOffset, position, duration, Merger.scaleOffset)
          Volume =
              dest.Volume
              |> Timeline.merge (source.Volume, position, duration, Merger.volume) }

    let flatten (duration: Duration) (timeline: Timeline<NoteOffsetTimelineMap>): NoteOffsetTimelineMap =
        timeline
        |> Timeline.aggregate (duration, merge) empty

module NoteOffsetBasedTimeline =
    let empty<'T> : NoteOffsetBasedTimeline<'T> =
        { Timeline = Seq.empty
          NoteOffsetTimelineMap = NoteOffsetTimelineMap.empty }

    let merge<'T when 'T: equality> (source: NoteOffsetBasedTimeline<'T>,
                                     position: Position,
                                     duration: Duration,
                                     combineFunction: TimelineCombineFunction<'T>)
                                    (dest: NoteOffsetBasedTimeline<'T>)
                                    : NoteOffsetBasedTimeline<'T> =
        { Timeline =
              dest.Timeline
              |> combineFunction (source.Timeline, position, duration)
          NoteOffsetTimelineMap =
              dest.NoteOffsetTimelineMap
              |> NoteOffsetTimelineMap.merge (source.NoteOffsetTimelineMap, position, duration) }

    let flatten<'T when 'T: equality> (duration: Duration, combineFunction: TimelineCombineFunction<'T>)
                                      (timeline: Timeline<NoteOffsetBasedTimeline<'T>>)
                                      : NoteOffsetBasedTimeline<'T> =
        timeline
        |> Timeline.aggregate
            (duration, (fun (source, position, duration) -> merge (source, position, duration, combineFunction)))
               empty
