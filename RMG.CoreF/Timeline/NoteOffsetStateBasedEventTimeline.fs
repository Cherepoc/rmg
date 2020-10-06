namespace RMG.CoreF

[<NoEquality>]
[<NoComparison>]
type NoteOffsetStateBasedEventTimeline<'T> =
    { Timeline: EventTimeline<'T>
      NoteOffsetStateTimelineMap: NoteOffsetStateTimelineMap }

[<NoEquality>]
[<NoComparison>]
type NoteOffsetStateBasedEventTimelineInput<'T> =
    { TimelineInput: Timeline<'T>
      NoteOffsetStateTimelineMapInput: NoteOffsetStateTimelineMapInput }

module NoteOffsetStateBasedEventTimeline =
    let empty<'T> : NoteOffsetStateBasedEventTimeline<'T> =
        { Timeline = EventTimeline.empty
          NoteOffsetStateTimelineMap = NoteOffsetStateTimelineMap.empty }

    let fromInput<'T> (duration: Duration)
                      (input: NoteOffsetStateBasedEventTimelineInput<'T>)
                      : NoteOffsetStateBasedEventTimeline<'T> =
        { Timeline = input.TimelineInput |> EventTimeline.fromSequence
          NoteOffsetStateTimelineMap =
              input.NoteOffsetStateTimelineMapInput
              |> NoteOffsetStateTimelineMap.fromInput duration }

    let merge<'T> (inputTimeline: Timeline<NoteOffsetStateBasedEventTimeline<'T>>): NoteOffsetStateBasedEventTimeline<'T> =
        let array = inputTimeline |> Seq.toArray
        { Timeline =
              array
              |> Timeline.map (fun value -> value.Timeline)
              |> EventTimeline.merge
          NoteOffsetStateTimelineMap =
              array
              |> Timeline.map (fun value -> value.NoteOffsetStateTimelineMap)
              |> NoteOffsetStateTimelineMap.merge }
