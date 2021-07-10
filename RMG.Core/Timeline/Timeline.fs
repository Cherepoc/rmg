namespace RMG.CoreF

type TimelineItem<'T> = Position * 'T

type Timeline<'T> = seq<TimelineItem<'T>>

type TimelineItemMerge<'T> = ('T * 'T) -> 'T

module Timeline =
    let map<'TSource, 'TDest> (func: 'TSource -> 'TDest) (inputTimeline: Timeline<'TSource>) : Timeline<'TDest> =
        inputTimeline |> Seq.map (fun (position, value) -> (position, func value))

    let shift (offset: Position) (inputTimeline: Timeline<'T>) : Timeline<'T> =
        inputTimeline |> Seq.map (fun (position, value) -> (position + offset, value))

    let trimDuration (duration: Duration) (inputTimeline: Timeline<'T>) : Timeline<'T> =
        inputTimeline |> Seq.filter (fun (position, _) -> position < duration)

    let groupByPosition (inputTimeline: Timeline<'T>) : Timeline<seq<'T>> =
        inputTimeline
        |> Seq.groupBy fst
        |> Seq.map (fun (position, items) -> (position, items |> Seq.map snd))

    let sort (inputTimeline: Timeline<'T>) : Timeline<'T> = inputTimeline |> Seq.sortBy fst

    let merge (inputTimeline: Timeline<#Timeline<'T>>) : Timeline<'T> =
        seq {
            for position, timeline in inputTimeline do
                yield! timeline |> shift position
        }

    let mergeItem (item: 'T) (timelineItemMerge: TimelineItemMerge<'T>) (inputTimeline: Timeline<'T>) : Timeline<'T> =
        inputTimeline |> map (fun x -> timelineItemMerge (x, item))

    let mergeComposite (timelineItemMerge: TimelineItemMerge<'T>) (inputTimeline: Timeline<#Timeline<'T> * 'T>) : Timeline<'T> =
        inputTimeline
        |> map (fun (timeline, item) -> (timeline |> mergeItem item timelineItemMerge))
        |> merge

    let itemFromSingle (value: 'T) : TimelineItem<'T> = (0.0, value)

    let fromSingle (value: 'T) : Timeline<'T> = seq { itemFromSingle value }
