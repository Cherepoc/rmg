namespace RMG.CoreF

open RMG.CoreF.Tuples

[<Sealed>]
type Timeline<'T> private (items: 'T TimelineItem array) =
    member internal this.Items = items

    member this.Item
        with get index = this.Items.[index]

    static member internal ofArrayUnsafe(items: 'T TimelineItem array) : Timeline<'T> = Timeline items

    interface System.Collections.Generic.IEnumerable<TimelineItem<'T>> with
        member this.GetEnumerator() =
            (this.Items :> System.Collections.Generic.IEnumerable<TimelineItem<'T>>)
                .GetEnumerator()

    interface System.Collections.IEnumerable with
        member this.GetEnumerator() =
            (this.Items :> System.Collections.IEnumerable)
                .GetEnumerator()

    interface System.Collections.Generic.IReadOnlyCollection<TimelineItem<'T>> with
        member this.Count = this.Items.Length

    interface System.Collections.Generic.IReadOnlyList<TimelineItem<'T>> with
        member this.Item
            with get index = this.Items.[index]

    member this.Count = this.Items.Length

module Timeline =
    let empty<'T> = Timeline<'T>.ofArrayUnsafe Array.empty

    let ofSeq<'T> (items: 'T TimelineItem seq) =
        items |> Seq.sortBy structFst |> Array.ofSeq |> Timeline.ofArrayUnsafe

    let map<'TSource, 'TDest> (func: 'TSource -> 'TDest) (inputTimeline: 'TSource Timeline) : 'TDest Timeline =
        inputTimeline.Items |> TimelineArray.map func |> Timeline.ofArrayUnsafe

    let scalePosition<'T> (scaleValue: Position) (inputTimeline: 'T Timeline) : 'T Timeline =
        inputTimeline.Items
        |> TimelineArray.scalePosition scaleValue
        |> Timeline.ofArrayUnsafe

    let choose<'TSource, 'TDest> (func: 'TSource -> 'TDest option) (inputTimeline: 'TSource Timeline) : Timeline<'TDest> =
        inputTimeline.Items |> TimelineArray.choose func |> Timeline.ofArrayUnsafe

    let shift<'T> (offset: Position) (inputTimeline: 'T Timeline) : 'T Timeline =
        inputTimeline.Items |> TimelineArray.shift offset |> Timeline.ofArrayUnsafe

    let phaseShift<'T> (phase: Position) (period: Position) (inputTimeline: 'T Timeline) : 'T Timeline =
        inputTimeline.Items
        |> TimelineArray.phaseShift phase period
        |> Timeline.ofArrayUnsafe

    let trimDuration (duration: Duration) (inputTimeline: 'T Timeline) : 'T Timeline =
        inputTimeline.Items
        |> TimelineArray.trimDuration duration
        |> Timeline.ofArrayUnsafe

    let concat (inputTimeline: 'T Timeline Timeline) : 'T Timeline =
        inputTimeline.Items
        |> Array.collect (fun (struct (position, timeline)) -> (timeline |> shift position).Items)
        |> Array.sortBy structFst
        |> Timeline.ofArrayUnsafe

    let itemOfSingle (value: 'T) : 'T TimelineItem = (0.0, value)

    let ofSingle (value: 'T) : 'T Timeline = [| itemOfSingle value |] |> Timeline.ofArrayUnsafe

    let ofMultiple (values: 'T seq) =
        values |> Seq.map itemOfSingle |> Array.ofSeq |> Timeline.ofArrayUnsafe
