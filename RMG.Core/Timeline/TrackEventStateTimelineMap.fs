namespace RMG.CoreF

type TrackEventStateTimelineMap = Map<TrackNumber option, EventStateTimelineMap>

module TrackEventStateTimelineMap =
    let ofSeq (duration: Duration) (input: (TrackNumber option * EventStateTimelineMap) seq) : TrackEventStateTimelineMap =
        input
        |> Seq.groupBy fst
        |> Seq.map
            (fun (trackNumber, trackTimeline) ->
                let mergedTimeline =
                    trackTimeline |> Seq.map snd |> Timeline.ofMultiple |> EventStateTimelineMap.concat duration

                (trackNumber, mergedTimeline))
        |> Map.ofSeq

    let ofSeqSeparated
        (duration: Duration)
        (sharedTimeline: EventStateTimelineMap)
        (trackTimelines: (TrackNumber * EventStateTimelineMap) seq)
        : TrackEventStateTimelineMap =
        seq {
            yield (None, sharedTimeline)

            yield! trackTimelines |> Seq.map (fun (trackNumber, timeline) -> (Some trackNumber, timeline))
        }
        |> ofSeq duration

    let ofMultiple (duration: Duration) (input: Event seq) : TrackEventStateTimelineMap =
        seq { (None, input |> EventStateTimelineMap.ofMultiple duration) } |> Map.ofSeq

    let concat (duration: Duration) (inputTimeline: TrackEventStateTimelineMap Timeline) : TrackEventStateTimelineMap =
        inputTimeline
        |> Seq.collect (fun struct (_, value) -> value |> Map.toSeq |> Seq.map fst)
        |> Seq.distinct
        |> Seq.map
            (fun trackNumber ->
                let timeline =
                    inputTimeline
                    |> Timeline.choose (fun trackMap -> trackMap |> Map.tryFind trackNumber)
                    |> EventStateTimelineMap.concat duration

                (trackNumber, timeline))
        |> Map.ofSeq

    let shiftState (state: EventState) (inputTimelineMap: TrackEventStateTimelineMap) : TrackEventStateTimelineMap =
        inputTimelineMap |> Map.map (fun _ timeline -> timeline |> EventStateTimelineMap.shiftState state)

    let concatCombined (duration: Duration) (inputTimeline: TrackEventStateTimelineMap WithEventState Timeline) : TrackEventStateTimelineMap =
        inputTimeline
        |> Timeline.map (fun struct (eventState, timeline) -> timeline |> shiftState eventState)
        |> concat duration
