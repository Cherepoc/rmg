namespace RMG.CoreF

[<NoEquality>]
[<NoComparison>]
type StateMerger<'T> =
    { Merge: 'T * 'T -> 'T
      CompareEqual: 'T * 'T -> bool
      DefaultValue: 'T }

[<NoComparison>]
[<NoEquality>]
[<Sealed>]
type StateTimeline<'T> internal (items: array<TimelineItem<'T>>) =
    member internal this.items = items

    interface System.Collections.Generic.IEnumerable<TimelineItem<'T>> with
        member this.GetEnumerator() =
            (this.items :> System.Collections.Generic.IEnumerable<TimelineItem<'T>>).GetEnumerator()

    interface System.Collections.IEnumerable with
        member this.GetEnumerator() =
            (this.items :> System.Collections.IEnumerable).GetEnumerator()

    interface System.Collections.Generic.IReadOnlyCollection<TimelineItem<'T>> with
        member this.Count = this.items.Length

    interface System.Collections.Generic.IReadOnlyList<TimelineItem<'T>> with
        member this.Item
            with get (index) = this.items.[index]

    member this.Count = this.items.Length

module StateTimeline =
    let empty<'T> = StateTimeline<'T>(Array.empty)

    let private trim<'T> (stateMerger: StateMerger<'T>) (inputTimeline: Timeline<'T>): Timeline<'T> =
        let sorted =
            inputTimeline
            |> Seq.skipWhile (fun x -> stateMerger.CompareEqual(x.Value, stateMerger.DefaultValue))
            |> Seq.toArray

        if sorted.Length <= 1 then
            sorted :> Timeline<'T>
        else
            seq {
                yield sorted |> Array.head

                for (previousItem, item) in sorted |> Seq.pairwise do
                    if not (stateMerger.CompareEqual(item.Value, previousItem.Value))
                    then yield item
            }

    let inline private mergeAggregate<'T> (merger: StateMerger<'T>) (values: seq<'T>): 'T =
        values
        |> Seq.fold (fun agg x -> merger.Merge(agg, x)) merger.DefaultValue

    let fromSequence<'T> (duration: Duration)
                         (stateMerger: StateMerger<'T>)
                         (inputTimeline: Timeline<'T>)
                         : StateTimeline<'T> =
        let trimmed =
            seq {
                yield! inputTimeline
                       |> Seq.filter (fun item -> item.Position < duration)
                       |> Seq.groupBy (fun item -> item.Position)
                       |> Seq.map (fun (position, items) ->
                           { Position = position
                             Value =
                                 items
                                 |> Seq.map (fun item -> item.Value)
                                 |> mergeAggregate stateMerger })
                       |> Seq.sortBy (fun x -> x.Position)

                yield { Position = duration
                        Value = stateMerger.DefaultValue }
            }
            |> trim stateMerger
            |> Seq.toArray

        StateTimeline(trimmed)

    let merge<'T> (stateMerger: StateMerger<'T>) (timelineInput: Timeline<StateTimeline<'T>>): StateTimeline<'T> =
        let shiftedTimelines =
            timelineInput
            |> Seq.map (fun timelineItem ->
                timelineItem.Value
                |> Timeline.shift timelineItem.Position
                |> Seq.toArray)
            |> Seq.toArray

        let effective (effectivePosition: Position) (inputTimeline: array<TimelineItem<'T>>): 'T =
            let effectiveItem =
                inputTimeline
                |> Array.tryFindBack (fun item -> item.Position <= effectivePosition)

            match effectiveItem with
            | Some effectiveItem -> effectiveItem.Value
            | _ -> stateMerger.DefaultValue

        let result =
            seq {
                for timeline in shiftedTimelines do
                    for item in timeline do
                        yield item.Position
            }
            |> Seq.distinct
            |> Seq.sort
            |> Seq.map (fun position ->
                { Position = position
                  Value =
                      shiftedTimelines
                      |> Seq.map (fun timeline -> timeline |> effective position)
                      |> mergeAggregate stateMerger })
            |> trim stateMerger
            |> Seq.toArray

        StateTimeline(result)

module StateMerger =
    let additive: StateMerger<int> =
        { Merge = fun (v1, v2) -> v1 + v2
          CompareEqual = fun (v1, v2) -> v1 = v2
          DefaultValue = 0 }

    let multiplicative: StateMerger<double> =
        { Merge = fun (v1, v2) -> v1 * v2
          CompareEqual = fun (v1, v2) -> v1 = v2
          DefaultValue = 1.0 }
