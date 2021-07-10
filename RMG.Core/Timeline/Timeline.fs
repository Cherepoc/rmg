namespace RMG.CoreF

type TimelineItem<'T> = { Position: Position; Value: 'T }

type Timeline<'T> = seq<TimelineItem<'T>>

type TimelineItemMerge<'T> = ('T * 'T) -> 'T

module Timeline =
    let map<'TSource, 'TDest> (func: 'TSource -> 'TDest) (inputTimeline: Timeline<'TSource>) : Timeline<'TDest> =
        inputTimeline
        |> Seq.map (fun item -> { Position = item.Position; Value = func item.Value })

    let shift (position: Position) (inputTimeline: Timeline<'T>) : Timeline<'T> =
        inputTimeline |> Seq.map (fun x -> { x with Position = x.Position + position })

    let merge (inputTimeline: Timeline<#Timeline<'T>>) : Timeline<'T> =
        seq {
            for timelineItem in inputTimeline do
                yield! timelineItem.Value |> shift timelineItem.Position
        }

    let mergeItem (item: 'T) (timelineItemMerge: TimelineItemMerge<'T>) (inputTimeline: Timeline<'T>) : Timeline<'T> =
        inputTimeline |> map (fun x -> timelineItemMerge (x, item))

    let mergeComposite (timelineItemMerge: TimelineItemMerge<'T>) (inputTimeline: Timeline<#Timeline<'T> * 'T>) : Timeline<'T> =
        inputTimeline
        |> map (fun (timeline, item) -> (timeline |> mergeItem item timelineItemMerge))
        |> merge

    let itemFromSingle (item: 'T) : TimelineItem<'T> = { Position = 0.0; Value = item }

    let fromSingle (item: 'T) : Timeline<'T> = seq { yield { Position = 0.0; Value = item } }
