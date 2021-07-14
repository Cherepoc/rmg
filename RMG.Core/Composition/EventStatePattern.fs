namespace RMG.CoreF.Composition

open RMG.CoreF

[<Sealed>]
type EventStatePattern
    private
    (
        duration: Duration,
        timeline: EventStateTimeline,
        patternTimeline: EventStatePattern WithEvents EventTimeline,
        flatTimeline: EventStateTimeline
    ) =
    member this.Duration = duration
    member this.Timeline = timeline

    member this.PatternTimeline = patternTimeline

    member this.FlatTimeline = flatTimeline

    new(duration: Duration, timeline: Event Timeline, patternTimeline: EventStatePattern WithEvents Timeline) =
        let orderedTimeline = timeline |> EventStateTimeline.ofSeq duration

        let orderedPatternTimeline = patternTimeline |> EventTimeline.fromSequence

        let flatTimeline =
            seq {
                yield orderedTimeline |> Timeline.itemFromSingle

                yield!
                    orderedPatternTimeline
                    |> Timeline.collect
                        (fun (events, pattern) ->
                            seq {
                                events |> Timeline.fromMultiple |> EventStateTimeline.ofSeq pattern.Duration
                                pattern.FlatTimeline
                            })
            }
            |> EventStateTimeline.concat

        EventStatePattern(duration, orderedTimeline, orderedPatternTimeline, flatTimeline)

    new() = EventStatePattern(0.0, EventStateTimeline.empty, EventTimeline.empty, EventStateTimeline.empty)

module EventStatePattern =
    let empty<'T> = EventStatePattern()

    let concat (sourceTimeline: EventStatePattern Timeline) : EventStateTimeline =
        sourceTimeline
        |> Timeline.map (fun pattern -> pattern.FlatTimeline)
        |> EventStateTimeline.concat

    let concatCombined (sourceTimeline: EventStatePattern WithEvents Timeline) : EventStateTimeline =
        sourceTimeline
        |> Timeline.map (fun (events, pattern) -> (events, pattern.FlatTimeline))
        |> EventStateTimeline.concatCombined
