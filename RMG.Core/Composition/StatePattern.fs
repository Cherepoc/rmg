namespace RMG.CoreF.Composition

open RMG.CoreF

[<Sealed>]
type StatePattern<'T>
    private
    (
        duration: Duration,
        timeline: StateTimeline<'T>,
        patternTimeline: EventTimeline<StatePattern<'T>>,
        flatTimeline: StateTimeline<'T>
    ) =
    member this.Duration = duration
    member this.Timeline = timeline

    member this.PatternTimeline = patternTimeline

    member this.FlatTimeline = flatTimeline

    new(duration: Duration, timeline: Timeline<'T>, patternTimeline: Timeline<StatePattern<'T>>, stateMerger: StateMerger<'T>) =
        let orderedTimeline = timeline |> StateTimeline.fromSequence duration stateMerger

        let orderedPatternTimeline = patternTimeline |> EventTimeline.fromSequence

        let flatTimeline =
            seq {
                yield orderedTimeline |> Timeline.itemFromSingle
                yield! orderedPatternTimeline |> Timeline.map (fun x -> x.FlatTimeline)
            }
            |> StateTimeline.merge stateMerger

        StatePattern(duration, orderedTimeline, orderedPatternTimeline, flatTimeline)

    new() = StatePattern(0.0, StateTimeline.empty, EventTimeline.empty, StateTimeline.empty)

[<NoComparison>]
[<NoEquality>]
type StatePatternInput<'T> =
    {
        TimelineInput: Timeline<'T>
        PatternTimelineInput: Timeline<StatePattern<'T>>
    }

module StatePattern =
    let empty<'T> = StatePattern<'T>()

    let fromInput<'T> (duration: Duration) (stateMerger: StateMerger<'T>) (input: StatePatternInput<'T>) =
        StatePattern<'T>(duration, input.TimelineInput, input.PatternTimelineInput, stateMerger)
