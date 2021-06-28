namespace RMG.CoreF

type TrackEventTimelineMap<'T> = Map<TrackNumber, EventTimeline<'T>>
type TrackEventTimelineMapInput<'T> = seq<TrackNumber * Timeline<'T>>

module TrackEventTimelineMap =
    let empty<'T> : TrackEventTimelineMap<'T> = Map.empty

    let fromSeq<'T> (input: TrackEventTimelineMapInput<'T>) : TrackEventTimelineMap<'T> =
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

    let merge<'T> (inputTimeline: Timeline<TrackEventTimelineMap<'T>>) : TrackEventTimelineMap<'T> =
        let array = inputTimeline |> Seq.toArray

        array
        |> Seq.collect (fun item -> item.Value |> Map.toSeq |> Seq.map fst)
        |> Seq.distinct
        |> Seq.map
            (fun trackNumber ->
                let mergedTimeline =
                    array
                    |> Seq.filter (fun item -> item.Value |> Map.containsKey trackNumber)
                    |> Timeline.map (fun map -> map |> Map.find trackNumber)
                    |> EventTimeline.merge

                (trackNumber, mergedTimeline))
        |> Map.ofSeq

    let map<'TSource, 'TDest>
        (func: 'TSource -> 'TDest)
        (input: TrackEventTimelineMap<'TSource>)
        : TrackEventTimelineMap<'TDest> =
        input
        |> Map.map
            (fun _ trackTimeline ->
                trackTimeline
                |> Timeline.map func
                |> EventTimeline.fromSequence)

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
                |> Map.map
                    (fun _ timeline ->
                        (timeline
                         |> Timeline.mergeItem compositeItem timelineItemMerge
                         |> EventTimeline.fromSequence)))
        |> merge
