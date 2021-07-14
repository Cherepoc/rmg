namespace RMG.CoreF

type TimelineItem<'T> = Position * 'T

type Timeline<'T> = seq<TimelineItem<'T>>

type TimelineItemMerge<'T> = ('T * 'T) -> 'T

module Timeline =
    let toPositions<'T> (inputTimeline: 'T Timeline) : Position seq = inputTimeline |> Seq.map fst

    let map<'TSource, 'TDest> (func: 'TSource -> 'TDest) (inputTimeline: Timeline<'TSource>) : Timeline<'TDest> =
        inputTimeline |> Seq.map (fun (position, value) -> (position, func value))

    let collect<'TSource, 'TDest> (func: 'TSource -> 'TDest seq) (inputTimeline: Timeline<'TSource>) : Timeline<'TDest> =
        inputTimeline
        |> Seq.collect (fun (position, value) -> func value |> Seq.map (fun convertedValue -> (position, convertedValue)))

    let filter<'T> (func: 'T -> bool) (inputTimeline: 'T Timeline) : 'T Timeline = inputTimeline |> Seq.filter (fun (_, value) -> func value)

    let choose<'TSource, 'TDest> (func: 'TSource -> 'TDest option) (inputTimeline: Timeline<'TSource>) : Timeline<'TDest> =
        inputTimeline
        |> Seq.choose (fun (position, value) -> func value |> Option.map (fun mappedValue -> (position, mappedValue)))

    let shift (offset: Position) (inputTimeline: Timeline<'T>) : Timeline<'T> =
        inputTimeline |> Seq.map (fun (position, value) -> (position + offset, value))

    let trimDuration (duration: Duration) (inputTimeline: Timeline<'T>) : Timeline<'T> =
        inputTimeline |> Seq.filter (fun (position, _) -> position < duration)

    let groupByPosition (inputTimeline: Timeline<'T>) : Timeline<seq<'T>> =
        inputTimeline
        |> Seq.groupBy fst
        |> Seq.map (fun (position, items) -> (position, items |> Seq.map snd))

    let sort (inputTimeline: Timeline<'T>) : Timeline<'T> = inputTimeline |> Seq.sortBy fst

    let concat (inputTimeline: Timeline<#Timeline<'T>>) : Timeline<'T> =
        seq {
            for position, timeline in inputTimeline do
                yield! timeline |> shift position
        }

    let mergeItem (item: 'T) (timelineItemMerge: TimelineItemMerge<'T>) (inputTimeline: Timeline<'T>) : Timeline<'T> =
        inputTimeline |> map (fun x -> timelineItemMerge (x, item))

    let mergeComposite (timelineItemMerge: TimelineItemMerge<'T>) (inputTimeline: Timeline<#Timeline<'T> * 'T>) : Timeline<'T> =
        inputTimeline
        |> map (fun (timeline, item) -> (timeline |> mergeItem item timelineItemMerge))
        |> concat

    let itemFromSingle (value: 'T) : TimelineItem<'T> = (0.0, value)

    let fromSingle (value: 'T) : Timeline<'T> = seq { itemFromSingle value }

    let fromMultiple (values: 'T seq) = values |> Seq.map itemFromSingle
