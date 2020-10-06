namespace RMG.CoreF.Composition

open RMG.CoreF

[<Sealed>]
type TrackNoteOffsetStateBasedEventPatternMap<'T> private (duration: Duration,
                                                           trackPatternTimelineMap: Map<TrackNumber, EventTimeline<NoteOffsetStateBasedEventPattern<'T>>>,
                                                           noteOffsetStatePatternMapPatternTimeline: EventTimeline<EventPattern<NoteOffsetStatePatternMap>>,
                                                           trackNoteOffsetStateBasedEventPatternMapTimeline: EventTimeline<TrackNoteOffsetStateBasedEventPatternMap<'T>>,
                                                           flatTimeline: TrackNoteOffsetStateBasedEventTimelineMap<'T>) =
    member this.Duration = duration
    member this.TrackPatternTimelineMap = trackPatternTimelineMap
    member this.NoteOffsetStatePatternMapPatternTimeline = noteOffsetStatePatternMapPatternTimeline

    member this.TrackNoteOffsetStateBasedEventPatternMapTimeline =
        trackNoteOffsetStateBasedEventPatternMapTimeline

    member this.FlatTimelineMap = flatTimeline

    new(duration: Duration,
        trackPatternTimelineMap: seq<TrackNumber * Timeline<NoteOffsetStateBasedEventPattern<'T>>>,
        noteOffsetStatePatternMapPatternTimeline: Timeline<EventPattern<NoteOffsetStatePatternMap>>,
        trackNoteOffsetStateBasedEventPatternMapTimeline: Timeline<TrackNoteOffsetStateBasedEventPatternMap<'T>>) =
        let orderedTrackPatternTimelineMap =
            trackPatternTimelineMap
            |> Seq.groupBy (fun (trackNumber, _) -> trackNumber)
            |> Seq.map (fun (trackNumber, items) ->
                let mergedTimeline =
                    items
                    |> Seq.map (fun (_, timeline) ->
                        { Position = 0.0
                          Value = timeline |> EventTimeline.fromSequence })
                    |> EventTimeline.merge

                (trackNumber, mergedTimeline))
            |> Map.ofSeq

        let orderedNoteOffsetStatePatternMapPatternTimeline =
            noteOffsetStatePatternMapPatternTimeline
            |> EventTimeline.fromSequence

        let orderedTrackNoteOffsetStateBasedEventPatternMapTimeline =
            trackNoteOffsetStateBasedEventPatternMapTimeline
            |> EventTimeline.fromSequence

        let flatTimelineMap =
            { TrackNoteOffsetStateBasedEventTimelineMap.TrackTimelineMap =
                  seq {
                      yield { Position = 0.0
                              Value =
                                  orderedTrackPatternTimelineMap
                                  |> Map.map (fun _ timeline ->
                                      timeline
                                      |> Timeline.map (fun pattern -> pattern.FlatTimeline)
                                      |> NoteOffsetStateBasedEventTimeline.merge) }
                      yield! orderedTrackNoteOffsetStateBasedEventPatternMapTimeline
                             |> Timeline.map (fun x -> x.FlatTimelineMap.TrackTimelineMap)
                  }
                  |> TrackNoteOffsetStateBasedEventTimelineMap.unwrapTrackMap
              TrackNoteOffsetStateBasedEventTimelineMap.NoteOffsetStateTimelineMap =
                  seq {
                      yield! orderedNoteOffsetStatePatternMapPatternTimeline
                             |> Timeline.map (fun x -> x.FlatTimeline)
                             |> Timeline.merge
                             |> Timeline.map (fun x -> x.FlatTimelineMap)
                      yield! orderedTrackNoteOffsetStateBasedEventPatternMapTimeline
                             |> Timeline.map (fun x -> x.FlatTimelineMap.NoteOffsetStateTimelineMap)
                  }
                  |> NoteOffsetStateTimelineMap.merge }

        TrackNoteOffsetStateBasedEventPatternMap
            (duration,
             orderedTrackPatternTimelineMap,
             orderedNoteOffsetStatePatternMapPatternTimeline,
             orderedTrackNoteOffsetStateBasedEventPatternMapTimeline,
             flatTimelineMap)

    new() =
        TrackNoteOffsetStateBasedEventPatternMap
            (0.0, Map.empty, EventTimeline.empty, EventTimeline.empty, TrackNoteOffsetStateBasedEventTimelineMap.empty)

[<NoEquality>]
[<NoComparison>]
type TrackNoteOffsetStateBasedEventPatternMapInput<'T> =
    { TrackPatternTimelineMapInput: seq<TrackNumber * Timeline<NoteOffsetStateBasedEventPattern<'T>>>
      NoteOffsetStatePatternMapPatternTimelineInput: Timeline<EventPattern<NoteOffsetStatePatternMap>>
      TrackNoteOffsetStateBasedEventPatternMapTimelineInput: Timeline<TrackNoteOffsetStateBasedEventPatternMap<'T>> }

module TrackNoteOffsetStateBasedEventPatternMap =
    let empty<'T> =
        TrackNoteOffsetStateBasedEventPatternMap<'T>()

    let fromInput<'T> (duration: Duration) (input: TrackNoteOffsetStateBasedEventPatternMapInput<'T>) =
        TrackNoteOffsetStateBasedEventPatternMap
            (duration,
             input.TrackPatternTimelineMapInput,
             input.NoteOffsetStatePatternMapPatternTimelineInput,
             input.TrackNoteOffsetStateBasedEventPatternMapTimelineInput)
