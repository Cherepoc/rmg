namespace RMG.CoreF.Composition

open RMG.CoreF

[<Sealed>]
type EventPattern<'T> private (duration: Duration,
                               timeline: EventTimeline<'T>,
                               patternTimeline: EventTimeline<EventPattern<'T>>,
                               flatTimeline: EventTimeline<'T>) =
    member this.Duration = duration
    member this.Timeline = timeline

    member this.PatternTimeline = patternTimeline

    member this.FlatTimeline = flatTimeline

    new(duration: Duration, timeline: Timeline<'T>, patternTimeline: Timeline<EventPattern<'T>>) =
        let orderedTimeline = timeline |> EventTimeline.fromSequence

        let orderedPatternTimeline =
            patternTimeline |> EventTimeline.fromSequence

        let flatTimeline =
            seq {
                yield { Position = 0.0
                        Value = orderedTimeline }
                yield! orderedPatternTimeline
                       |> Timeline.map (fun x -> x.FlatTimeline)
            }
            |> EventTimeline.merge

        EventPattern(duration, orderedTimeline, orderedPatternTimeline, flatTimeline)

    new() = EventPattern(0.0, EventTimeline.empty, EventTimeline.empty, EventTimeline.empty)

[<NoComparison>]
[<NoEquality>]
type EventPatternInput<'T> =
    { TimelineInput: Timeline<'T>
      PatternTimelineInput: Timeline<EventPattern<'T>> }

module EventPattern =
    let empty<'T> = EventPattern()

    let fromInput<'T> (duration: Duration) (input: EventPatternInput<'T>) =
        EventPattern<'T>(duration, input.TimelineInput, input.PatternTimelineInput)
