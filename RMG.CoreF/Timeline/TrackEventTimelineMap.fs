namespace RMG.CoreF

type SubTrackEventTimelineMap<'T> = Map<TrackNumber, EventTimeline<'T>>
type TrackEventTimelineMap<'T> = Map<TrackNumber, SubTrackEventTimelineMap<'T>>

type SubTrackEventTimelineMapInput<'T> = seq<TrackNumber * Timeline<'T>>
type TrackEventTimelineMapInput<'T> = seq<TrackNumber * SubTrackEventTimelineMapInput<'T>>

module TrackEventTimelineMap =
    let empty<'T> : TrackEventTimelineMap<'T> = Map.empty

    let fromSeq<'T> (input: TrackEventTimelineMapInput<'T>) : TrackEventTimelineMap<'T> =
        let fromSeqSubTrack (input: SubTrackEventTimelineMapInput<'T>) : SubTrackEventTimelineMap<'T> =
            input
            |> Seq.groupBy fst
            |> Seq.map
                (fun (trackNumber, trackTimeline) ->
                    let mergedTimeline =
                        trackTimeline
                        |> Seq.map
                            (fun (_, timeline) ->
                                timeline
                                |> EventTimeline.fromSequence
                                |> Timeline.itemFromSingle)
                        |> EventTimeline.merge

                    (trackNumber, mergedTimeline))
            |> Map.ofSeq

        input
        |> Seq.groupBy fst
        |> Seq.map
            (fun (trackNumber, trackTimeline) ->
                let mergedTimeline =
                    trackTimeline
                    |> Seq.collect snd
                    |> fromSeqSubTrack

                (trackNumber, mergedTimeline))
        |> Map.ofSeq

    let shift<'T> (position: Position) (timelineMap: TrackEventTimelineMap<'T>): TrackEventTimelineMap<'T> =
        timelineMap
        |> Map.map (fun _ subTrackTimelineMap ->
                subTrackTimelineMap
                |> Map.map (fun _ timeline ->
                    timeline
                    |> Timeline.shift position
                    |> EventTimeline.fromSequence))

    let merge<'T> (inputTimeline: Timeline<TrackEventTimelineMap<'T>>) : TrackEventTimelineMap<'T> =
        let mergeSubTrack (inputTimeline: seq<SubTrackEventTimelineMap<'T>>): SubTrackEventTimelineMap<'T> =
            inputTimeline
            |> Seq.collect (fun x -> x |> Map.toSeq)
            |> Seq.groupBy fst
            |> Seq.map (fun (trackNumber, timelines) ->
                let eventTimeline =
                    timelines
                    |> Seq.collect (fun (_, timeline) -> timeline)
                    |> EventTimeline.fromSequence
                (trackNumber, eventTimeline))
            |> Map.ofSeq

        inputTimeline
        |> Seq.collect (fun x -> x.Value |> shift x.Position |> Map.toSeq)
        |> Seq.groupBy fst
        |> Seq.map (fun (trackNumber, subTrackTimelines) ->
            let subTrackTimeline =
                subTrackTimelines
                |> Seq.map snd
                |> mergeSubTrack
            (trackNumber, subTrackTimeline))
        |> Map.ofSeq

    let map<'TSource, 'TDest>
        (func: 'TSource -> 'TDest)
        (input: TrackEventTimelineMap<'TSource>)
        : TrackEventTimelineMap<'TDest> =
        input
        |> Map.map (fun _ subTrackTimelineMap ->
            subTrackTimelineMap
            |> Map.map (fun _ timeline ->
                timeline
                |> Timeline.map func
                |> EventTimeline.fromSequence))

    let mergeItem
        (item: 'T)
        (timelineItemMerge: TimelineItemMerge<'T>)
        (inputTimelineMap: TrackEventTimelineMap<'T>)
        : TrackEventTimelineMap<'T> =
        inputTimelineMap
        |> map (fun x -> timelineItemMerge (x, item))

    let mergeComposite
        (timelineItemMerge: TimelineItemMerge<'T>)
        (inputTimelineMap: Timeline<TrackEventTimelineMap<'T> * 'T>)
        : TrackEventTimelineMap<'T> =
        inputTimelineMap
        |> Timeline.map
            (fun (timelineMap, compositeItem) ->
                timelineMap
                |> map (fun x -> timelineItemMerge(x,  compositeItem)))
        |> merge

    let mergeCompositeInner
        (timelineItemMerge: TimelineItemMerge<'T>)
        (inputTimelineMap: TrackEventTimelineMap<#Timeline<'T> * 'T>)
        : TrackEventTimelineMap<'T> =
        inputTimelineMap
        |> Map.map (fun _ subTrackTimelineMap ->
            subTrackTimelineMap
            |> Map.map (fun _ timeline ->
                timeline
                |> Timeline.mergeComposite timelineItemMerge
                |> EventTimeline.fromSequence))
