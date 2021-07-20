namespace RMG.CoreF.Composition

open RMG.CoreF

[<Sealed>]
type EventStatePattern
    private
    (
        duration: Duration,
        timeline: EventStateTimelineMap,
        patternTimeline: EventStatePattern WithEventState Timeline,
        flatTimeline: EventStateTimelineMap
    ) =
    member this.Duration = duration
    member this.Timeline = timeline

    member this.PatternTimeline = patternTimeline

    member this.FlatTimeline = flatTimeline

    static member internal ofTimelinesUnsafe
        (duration: Duration)
        (timeline: EventStateTimelineMap)
        (patternTimeline: EventStatePattern WithEventState Timeline)
        (flatTimeline: EventStateTimelineMap)
        : EventStatePattern =
        EventStatePattern(duration, timeline, patternTimeline, flatTimeline)

module EventStatePattern =
    let ofTimelines (duration: Duration) (timeline: EventStateTimelineMap) (patternTimeline: EventStatePattern WithEventState Timeline) : EventStatePattern =
        let flatTimeline =
            seq {
                yield struct (EventState.empty, timeline) |> Timeline.itemOfSingle
                yield! patternTimeline |> Timeline.map (fun struct (eventState, pattern) -> struct (eventState, pattern.FlatTimeline))
            }
            |> Timeline.ofSeq
            |> EventStateTimelineMap.concatCombined duration

        EventStatePattern.ofTimelinesUnsafe duration timeline patternTimeline flatTimeline
