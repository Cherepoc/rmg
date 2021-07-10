namespace RMG.CoreF

[<NoEquality>]
[<NoComparison>]
type StateMerger<'T> =
    {
        Merge: 'T * 'T -> 'T
        CompareEqual: 'T * 'T -> bool
        DefaultValue: 'T
    }

[<NoComparison>]
[<NoEquality>]
[<Sealed>]
type StateTimeline<'T> internal (items: array<TimelineItem<'T>>) =
    member internal this.items = items

    interface System.Collections.Generic.IEnumerable<TimelineItem<'T>> with
        member this.GetEnumerator() =
            (this.items :> System.Collections.Generic.IEnumerable<TimelineItem<'T>>)
                .GetEnumerator()

    interface System.Collections.IEnumerable with
        member this.GetEnumerator() =
            (this.items :> System.Collections.IEnumerable)
                .GetEnumerator()

    interface System.Collections.Generic.IReadOnlyCollection<TimelineItem<'T>> with
        member this.Count = this.items.Length

    interface System.Collections.Generic.IReadOnlyList<TimelineItem<'T>> with
        member this.Item
            with get index = this.items.[index]

    member this.Count = this.items.Length

module StateTimeline =
    let empty<'T> = StateTimeline<'T>(Array.empty)

    let private trim<'T> (stateMerger: StateMerger<'T>) (inputTimeline: Timeline<'T>) : Timeline<'T> =
        let sorted =
            inputTimeline
            |> Seq.skipWhile (fun (_, value) -> stateMerger.CompareEqual(value, stateMerger.DefaultValue))
            |> Seq.toArray

        if sorted.Length <= 1 then
            sorted :> Timeline<'T>
        else
            seq {
                sorted |> Array.head

                for (_, previousValue), (position, value) in sorted |> Seq.pairwise do
                    if not (stateMerger.CompareEqual(value, previousValue)) then
                        (position, value)
            }

    let inline private mergeAggregate<'T> (merger: StateMerger<'T>) (values: seq<'T>) : 'T =
        values |> Seq.fold (fun agg x -> merger.Merge(agg, x)) merger.DefaultValue

    let fromSequence<'T> (duration: Duration) (stateMerger: StateMerger<'T>) (inputTimeline: Timeline<'T>) : StateTimeline<'T> =
        let trimmed =
            seq {
                yield!
                    inputTimeline
                    |> Timeline.trimDuration duration
                    |> Timeline.groupByPosition
                    |> Timeline.map (fun value -> value |> mergeAggregate stateMerger)
                    |> Timeline.sort

                yield (duration, stateMerger.DefaultValue)
            }
            |> trim stateMerger
            |> Seq.toArray

        StateTimeline trimmed

    let merge<'T> (stateMerger: StateMerger<'T>) (timelineInput: Timeline<StateTimeline<'T>>) : StateTimeline<'T> =
        let shiftedTimelines =
            timelineInput
            |> Seq.map (fun (position, timeline) -> timeline |> Timeline.shift position |> Seq.toArray)
            |> Seq.toArray

        let effective (effectivePosition: Position) (inputTimeline: array<TimelineItem<'T>>) : 'T =
            let effectiveItem =
                inputTimeline
                |> Array.tryFindBack (fun (position, _) -> position <= effectivePosition)

            match effectiveItem with
            | Some (_, value) -> value
            | _ -> stateMerger.DefaultValue

        let result =
            seq {
                for timeline in shiftedTimelines do
                    for position, _ in timeline do
                        yield position
            }
            |> Seq.distinct
            |> Seq.sort
            |> Seq.map
                (fun position ->
                    let value =
                        shiftedTimelines
                        |> Seq.map (fun timeline -> timeline |> effective position)
                        |> mergeAggregate stateMerger

                    (position, value))
            |> trim stateMerger
            |> Seq.toArray

        StateTimeline result

    let effectiveValue<'T> (position: Position) (stateMerger: StateMerger<'T>) (timeline: StateTimeline<'T>) : 'T =
        let effectiveItem =
            timeline.items
            |> Array.tryFindBack (fun (itemPosition, _) -> itemPosition <= position)

        match effectiveItem with
        | Some (_, value) -> value
        | _ -> stateMerger.DefaultValue

module StateMerger =
    let additiveInt : StateMerger<int> =
        {
            Merge = fun (v1, v2) -> v1 + v2
            CompareEqual = fun (v1, v2) -> v1 = v2
            DefaultValue = 0
        }

    let additiveFloat : StateMerger<float> =
        {
            Merge = fun (v1, v2) -> v1 + v2
            CompareEqual = fun (v1, v2) -> v1 = v2
            DefaultValue = 0.0
        }

    let multiplicativeFloat : StateMerger<float> =
        {
            Merge = fun (v1, v2) -> v1 * v2
            CompareEqual = fun (v1, v2) -> v1 = v2
            DefaultValue = 1.0
        }
