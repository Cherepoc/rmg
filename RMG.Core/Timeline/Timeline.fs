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

    let ofSeq<'T> (items: 'T TimelineItem seq) = items |> Seq.sortBy structFst |> Array.ofSeq |> Timeline.ofArrayUnsafe

//    let isEmpty<'T> (inputTimeline: 'T Timeline) : bool = inputTimeline.Items |> Array.isEmpty

//    let toPositions<'T> (inputTimeline: 'T Timeline) : Position array = inputTimeline.Items |> TimelineArray.toPositions

    let map<'TSource, 'TDest> (func: 'TSource -> 'TDest) (inputTimeline: 'TSource Timeline) : 'TDest Timeline =
        inputTimeline.Items |> TimelineArray.map func |> Timeline.ofArrayUnsafe

//    let collect<'TSource, 'TDest> (func: 'TSource -> 'TDest seq) (inputTimeline: 'TSource Timeline) : 'TDest Timeline =
//        inputTimeline.Items |> TimelineArray.collect func |> Timeline.ofArrayUnsafe

//    let filter<'T> (func: 'T -> bool) (inputTimeline: 'T Timeline) : 'T Timeline = inputTimeline.Items |> TimelineArray.filter func |> Timeline.ofArrayUnsafe

    let choose<'TSource, 'TDest> (func: 'TSource -> 'TDest option) (inputTimeline: 'TSource Timeline) : Timeline<'TDest> =
        inputTimeline.Items |> TimelineArray.choose func |> Timeline.ofArrayUnsafe

    let shift<'T> (offset: Position) (inputTimeline: 'T Timeline) : 'T Timeline = inputTimeline.Items |> TimelineArray.shift offset |> Timeline.ofArrayUnsafe

//    let tryFindEffectiveIndex<'T> (position: Position) (inputTimeline: 'T Timeline) : int option =
//        inputTimeline.Items |> TimelineArray.tryFindEffectiveIndex position
//
//    let tryFindEffectiveValue<'T> (position: Position) (inputTimeline: 'T Timeline) : 'T option =
//        inputTimeline.Items |> TimelineArray.tryFindEffectiveValue position

    let trimDuration (duration: Duration) (inputTimeline: 'T Timeline) : 'T Timeline =
        inputTimeline.Items |> TimelineArray.trimDuration duration |> Timeline.ofArrayUnsafe

    //let groupByPosition (inputTimeline: 'T Timeline) : 'T seq Timeline = inputTimeline.Items |> TimelineArray.groupByPosition |> Timeline.ofArrayUnsafe

    let concat (inputTimeline: 'T Timeline Timeline) : 'T Timeline =
        inputTimeline.Items
        |> Array.collect (fun struct (position, timeline) -> (timeline |> shift position).Items)
        |> Array.sortBy structFst
        |> Timeline.ofArrayUnsafe

    let itemOfSingle (value: 'T) : 'T TimelineItem = (0.0, value)

    let ofSingle (value: 'T) : 'T Timeline = [| itemOfSingle value |] |> Timeline.ofArrayUnsafe

    let ofMultiple (values: 'T seq) = values |> Seq.map itemOfSingle |> Array.ofSeq |> Timeline.ofArrayUnsafe
