namespace RMG.CoreF.Composition

open RMG.CoreF

[<Sealed>]
type TrackEventStatePattern
    private
    (
        duration: Duration,
        trackPatternTimelineMap: Map<TrackNumber option, EventStatePattern WithEventState Timeline>,
        trackPatternMapTimeline: TrackEventStatePattern WithEventState Timeline,
        flatTimelineMap: TrackEventStateTimelineMap
    ) =
    member this.Duration = duration
    member this.TrackPatternTimelineMap = trackPatternTimelineMap
    member this.TrackPatternMapTimeline = trackPatternMapTimeline
    member this.FlatTimelineMap = flatTimelineMap

    static member internal ofTimelineMapsUnsafe
        (duration: Duration)
        (trackPatternTimelineMap: Map<TrackNumber option, EventStatePattern WithEventState Timeline>)
        (trackPatternMapTimeline: TrackEventStatePattern WithEventState Timeline)
        (flatTimelineMap: TrackEventStateTimelineMap)
        : TrackEventStatePattern =
        TrackEventStatePattern(duration, trackPatternTimelineMap, trackPatternMapTimeline, flatTimelineMap)

module TrackEventStatePattern =
    let ofTimelineMaps
        (duration: Duration)
        (trackPatternTimelineMap: Map<TrackNumber option, EventStatePattern WithEventState Timeline>)
        (trackPatternMapTimeline: TrackEventStatePattern WithEventState Timeline)
        : TrackEventStatePattern =

        let flatTimelineMap =
            seq {
                yield
                    trackPatternTimelineMap
                    |> Map.map
                        (fun key patternTimeline ->
                            patternTimeline
                            |> Timeline.map (fun struct (eventState, pattern) -> struct (eventState, pattern.FlatTimeline))
                            |> EventStateTimelineMap.concatCombined duration)
                    |> Timeline.itemOfSingle

                yield!
                    trackPatternMapTimeline
                    |> Timeline.map (fun struct (eventState, pattern) -> pattern.FlatTimelineMap |> TrackEventStateTimelineMap.shiftState eventState)
            }
            |> Timeline.ofSeq
            |> TrackEventStateTimelineMap.concat duration

        TrackEventStatePattern.ofTimelineMapsUnsafe duration trackPatternTimelineMap trackPatternMapTimeline flatTimelineMap
