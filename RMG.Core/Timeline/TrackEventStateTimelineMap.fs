namespace RMG.Core

type TrackEventStateTimelineMap = Map<TrackNumber option, EventStateTimelineMap>

module TrackEventStateTimelineMap =
    let ofSeq (duration: Duration) (input: (TrackNumber option * EventStateTimelineMap) seq) : TrackEventStateTimelineMap =
        input
        |> Seq.groupBy fst
        |> Seq.map
            (fun (trackNumber, trackTimeline) ->
                let mergedTimeline =
                    trackTimeline
                    |> Seq.map snd
                    |> Timeline.ofMultiple
                    |> EventStateTimelineMap.concat duration

                (trackNumber, mergedTimeline))
        |> Map.ofSeq

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
        inputTimelineMap
        |> Map.map (fun _ timeline -> timeline |> EventStateTimelineMap.shiftState state)

    let concatCombined (duration: Duration) (inputTimeline: TrackEventStateTimelineMap WithEventState Timeline) : TrackEventStateTimelineMap =
        inputTimeline
        |> Timeline.map (fun struct (eventState, timeline) -> timeline |> shiftState eventState)
        |> concat duration

    let mapTrackNumbers (trackNumberMap: Map<TrackNumber option, TrackNumber option>) (inputTimelineMap: TrackEventStateTimelineMap) : TrackEventStateTimelineMap =
        trackNumberMap
        |> Map.toSeq
        |> Seq.choose
            (fun (toTrack, fromTrack) ->
                inputTimelineMap
                |> Map.tryFind fromTrack
                |> Option.map (fun track -> (toTrack, track)))
        |> Map.ofSeq
