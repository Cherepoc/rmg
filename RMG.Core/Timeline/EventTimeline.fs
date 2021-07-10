namespace RMG.CoreF

[<NoComparison>]
[<NoEquality>]
[<Sealed>]
type EventTimeline<'T> internal (items: array<TimelineItem<'T>>) =
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

module EventTimeline =
    let empty<'T> = EventTimeline<'T>(Array.empty)

    let fromSequence<'T> (inputTimeline: Timeline<'T>) : EventTimeline<'T> =
        EventTimeline(inputTimeline |> Timeline.sort |> Seq.toArray)

    let merge (inputTimeline: Timeline<EventTimeline<'T>>) : EventTimeline<'T> = inputTimeline |> Timeline.merge |> fromSequence

    let lastEffectiveValue<'T> (position: Position) (timeline: EventTimeline<'T>) : 'T Option =
        let effectiveItem =
            timeline.items
            |> Array.tryFindBack (fun (itemPosition, _) -> itemPosition <= position)
        effectiveItem |> Option.map snd
