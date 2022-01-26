namespace RMG.Core.Composition

open RMG.Core

[<Sealed>]
type TrackEventStatePattern
    private
    (
        duration: Duration,
        trackNumberMap: Map<TrackNumber option, TrackNumber option> option,
        trackPatternTimelineMap: Map<TrackNumber option, EventStatePattern WithEventState Timeline>,
        trackPatternMapTimeline: TrackEventStatePattern WithEventState Timeline,
        flatTimelineMap: TrackEventStateTimelineMap
    ) =
    member this.Duration = duration
    member this.TrackNumberMap = trackNumberMap
    member this.TrackPatternTimelineMap = trackPatternTimelineMap
    member this.TrackPatternMapTimeline = trackPatternMapTimeline
    member this.FlatTimelineMap = flatTimelineMap

    static member internal ofTimelineMapsUnsafe
        (duration: Duration)
        (trackNumberMap: Map<TrackNumber option, TrackNumber option> option)
        (trackPatternTimelineMap: Map<TrackNumber option, EventStatePattern WithEventState Timeline>)
        (trackPatternMapTimeline: TrackEventStatePattern WithEventState Timeline)
        (flatTimelineMap: TrackEventStateTimelineMap)
        : TrackEventStatePattern =
        TrackEventStatePattern(duration, trackNumberMap, trackPatternTimelineMap, trackPatternMapTimeline, flatTimelineMap)

module TrackEventStatePattern =
    let ofTimelineMaps
        (duration: Duration)
        (trackNumberMap: Map<TrackNumber option, TrackNumber option> option)
        (trackPatternTimelineMap: Map<TrackNumber option, EventStatePattern WithEventState Timeline>)
        (trackPatternMapTimeline: TrackEventStatePattern WithEventState Timeline)
        : TrackEventStatePattern
        =

        let mapTrackNumbers (inputTimelineMap: TrackEventStateTimelineMap) : TrackEventStateTimelineMap =
            match trackNumberMap with
            | Some map -> inputTimelineMap |> TrackEventStateTimelineMap.mapTrackNumbers map
            | _ -> inputTimelineMap

        let flatTimelineMap =
            seq {
                yield
                    trackPatternTimelineMap
                    |> Map.map
                        (fun _ patternTimeline ->
                            patternTimeline
                            |> Timeline.map (fun struct (eventState, pattern) -> struct (eventState, pattern.FlatTimeline))
                            |> EventStateTimelineMap.concatCombined duration)
                    |> Timeline.itemOfSingle

                yield!
                    trackPatternMapTimeline
                    |> Timeline.map
                        (fun struct (eventState, pattern) ->
                            pattern.FlatTimelineMap
                            |> mapTrackNumbers
                            |> TrackEventStateTimelineMap.shiftState eventState)
            }
            |> Timeline.ofSeq
            |> TrackEventStateTimelineMap.concat duration

        TrackEventStatePattern.ofTimelineMapsUnsafe duration trackNumberMap trackPatternTimelineMap trackPatternMapTimeline flatTimelineMap
