namespace RMG.CoreF

[<NoEquality>]
[<NoComparison>]
type TrackNoteOffsetStateBasedEventTimelineMap<'T> =
    { TrackTimelineMap: Map<TrackNumber, NoteOffsetStateBasedEventTimeline<'T>>
      NoteOffsetStateTimelineMap: NoteOffsetStateTimelineMap }

[<NoEquality>]
[<NoComparison>]
type TrackNoteOffsetStateBasedEventTimelineMapInput<'T> =
    { TrackTimelineMapInput: seq<TrackNumber * NoteOffsetStateBasedEventTimeline<'T>>
      NoteOffsetStateTimelineMapInput: NoteOffsetStateTimelineMapInput }

module TrackNoteOffsetStateBasedEventTimelineMap =
    let empty<'T> : TrackNoteOffsetStateBasedEventTimelineMap<'T> =
        { TrackTimelineMap = Map.empty
          NoteOffsetStateTimelineMap = NoteOffsetStateTimelineMap.empty }

    let trackMapFromSeq<'T> (input: seq<TrackNumber * NoteOffsetStateBasedEventTimeline<'T>>)
                            : Map<TrackNumber, NoteOffsetStateBasedEventTimeline<'T>> =
        input
        |> Seq.groupBy (fun (trackNumber, _) -> trackNumber)
        |> Seq.map (fun (trackNumber, items) ->
            let mergedTimeline =
                items
                |> Seq.map (fun (_, timeline) -> { Position = 0.0; Value = timeline })
                |> NoteOffsetStateBasedEventTimeline.merge

            (trackNumber, mergedTimeline))
        |> Map.ofSeq

    let unwrapTrackMap<'T> (inputTimeline: Timeline<Map<TrackNumber, NoteOffsetStateBasedEventTimeline<'T>>>)
                           : Map<TrackNumber, NoteOffsetStateBasedEventTimeline<'T>> =
        let array = inputTimeline |> Seq.toArray
        array
        |> Seq.collect (fun item ->
            item.Value
            |> Map.toSeq
            |> Seq.map (fun (trackNumber, _) -> trackNumber))
        |> Seq.distinct
        |> Seq.map (fun trackNumber ->
            let mergedTimeline =
                array
                |> Seq.filter (fun item -> item.Value |> Map.containsKey trackNumber)
                |> Timeline.map (fun map -> map |> Map.find trackNumber)
                |> NoteOffsetStateBasedEventTimeline.merge

            (trackNumber, mergedTimeline))
        |> Map.ofSeq

    let fromInput<'T> (duration: Duration)
                      (input: TrackNoteOffsetStateBasedEventTimelineMapInput<'T>)
                      : TrackNoteOffsetStateBasedEventTimelineMap<'T> =
        { TrackTimelineMap = input.TrackTimelineMapInput |> trackMapFromSeq
          NoteOffsetStateTimelineMap =
              input.NoteOffsetStateTimelineMapInput
              |> NoteOffsetStateTimelineMap.fromInput duration }

    let merge<'T> (inputTimeline: Timeline<TrackNoteOffsetStateBasedEventTimelineMap<'T>>)
                  : TrackNoteOffsetStateBasedEventTimelineMap<'T> =
        let array = inputTimeline |> Seq.toArray
        { TrackTimelineMap =
              array
              |> Timeline.map (fun pattern -> pattern.TrackTimelineMap)
              |> unwrapTrackMap
          NoteOffsetStateTimelineMap =
              array
              |> Timeline.map (fun item -> item.NoteOffsetStateTimelineMap)
              |> NoteOffsetStateTimelineMap.merge }
