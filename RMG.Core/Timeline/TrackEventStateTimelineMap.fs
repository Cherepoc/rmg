namespace RMG.CoreF

type TrackEventStateTimelineMap = Map<TrackNumber option, EventStateTimeline>
type TrackEventStateTimelineMapInput = (TrackNumber option * Event Timeline) seq

module TrackEventStateTimelineMap =
    let empty : TrackEventStateTimelineMap = Map.empty

    let ofSeq (duration: Duration) (input: (TrackNumber option * Event Timeline) seq) : TrackEventStateTimelineMap =
        input
        |> Seq.groupBy fst
        |> Seq.map
            (fun (trackNumber, trackTimeline) ->
                let mergedTimeline =
                    trackTimeline
                    |> Seq.map (fun (_, timeline) -> timeline |> EventStateTimeline.ofSeq duration |> Timeline.itemFromSingle)
                    |> EventStateTimeline.concat

                (trackNumber, mergedTimeline))
        |> Map.ofSeq

    let ofSeqSeparated (duration: Duration) (sharedTimeline: Event Timeline) (trackTimelines: (TrackNumber * Event Timeline) seq) : TrackEventStateTimelineMap =
        seq {
            yield (None, sharedTimeline)

            yield!
                trackTimelines
                |> Seq.map (fun (trackNumber, timeline) -> (Some trackNumber, timeline))
        }
        |> ofSeq duration

    let ofMultiple (duration: Duration) (input: Event seq) : TrackEventStateTimelineMap =
        seq { (None, input |> Timeline.fromMultiple |> EventStateTimeline.ofSeq duration) }
        |> Map.ofSeq

    let getDuration (input: TrackEventStateTimelineMap) : Duration =
        if input |> Map.isEmpty then
            0.0
        else
            input
            |> Map.toSeq
            |> Seq.map (fun (_, timeline) -> timeline.Duration)
            |> Seq.max

    let concat (inputTimeline: TrackEventStateTimelineMap Timeline) : TrackEventStateTimelineMap =
        let array = inputTimeline |> Array.ofSeq

        if array |> Array.isEmpty then
            empty
        else
            let duration =
                array
                |> Seq.collect
                    (fun (position, timelineMap) ->
                        timelineMap
                        |> Map.toSeq
                        |> Seq.map (fun (_, timeline) -> timeline.Duration + position))
                |> Seq.max

            array
            |> Seq.collect
                (fun (position, timelineMap) ->
                    timelineMap
                    |> Map.toSeq
                    |> Seq.map (fun (trackNumber, timeline) -> (trackNumber, timeline |> Timeline.shift position)))
            |> ofSeq duration

    let concatCombined (inputTimeline: TrackEventStateTimelineMap WithEvents Timeline) : TrackEventStateTimelineMap =
        inputTimeline
        |> Timeline.collect
            (fun (events, timelineMap) ->
                seq {
                    events |> ofMultiple (timelineMap |> getDuration)
                    timelineMap
                })
        |> concat
