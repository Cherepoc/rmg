namespace RMG.CoreF.Composition

open RMG.CoreF

[<Sealed>]
type CompositeEventPattern<'T>
    private
    (
        duration: Duration,
        timeline: EventTimeline<'T>,
        compositePatternTimeline: EventTimeline<CompositeEventPattern<'T> * 'T>,
        flatTimeline: EventTimeline<'T>
    ) =
    member this.Duration = duration
    member this.Timeline = timeline

    member this.CompositePatternTimeline = compositePatternTimeline

    member this.FlatTimeline = flatTimeline

    new(duration: Duration,
        timeline: Timeline<'T>,
        compositePatternTimeline: Timeline<CompositeEventPattern<'T> * 'T>,
        timelineItemMerge: TimelineItemMerge<'T>) =
        let orderedTimeline = timeline |> EventTimeline.fromSequence

        let orderedPatternTimeline =
            compositePatternTimeline
            |> EventTimeline.fromSequence

        let flatTimeline =
            seq {
                yield orderedTimeline |> Timeline.itemFromSingle

                yield
                    orderedPatternTimeline
                    |> Timeline.map (fun (pattern, compositeItem) -> (pattern.FlatTimeline, compositeItem))
                    |> Timeline.mergeComposite timelineItemMerge
                    |> EventTimeline.fromSequence
                    |> Timeline.itemFromSingle
            }
            |> EventTimeline.merge

        CompositeEventPattern(duration, orderedTimeline, orderedPatternTimeline, flatTimeline)

    new() = CompositeEventPattern(0.0, EventTimeline.empty, EventTimeline.empty, EventTimeline.empty)

[<NoComparison>]
[<NoEquality>]
type CompositeEventPatternInput<'T> =
    { TimelineInput: Timeline<'T>
      CompositePatternTimelineInput: Timeline<CompositeEventPattern<'T> * 'T> }

module CompositeEventPattern =
    let empty<'T> = CompositeEventPattern()

    let fromInput<'T> (duration: Duration) (merge: 'T * 'T -> 'T) (input: CompositeEventPatternInput<'T>) =
        CompositeEventPattern<'T>(duration, input.TimelineInput, input.CompositePatternTimelineInput, merge)
