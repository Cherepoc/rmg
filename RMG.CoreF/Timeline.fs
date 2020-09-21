namespace RMG.CoreF

type Position = double
type Duration = double

[<NoEquality>]
[<NoComparison>]
type TimelineItem<'T> = { Position: Position; Value: 'T }

[<NoEquality>]
[<NoComparison>]
type DurationItem<'T> = { Duration: Duration; Value: 'T }

type Timeline<'T> = seq<TimelineItem<'T>>

type CombineFunction<'T> = 'T * Position * Duration -> 'T -> 'T

type TimelineCombineFunction<'T> = CombineFunction<Timeline<'T>>

[<NoEquality>]
[<NoComparison>]
type TimelineMerger<'T> = { Merge: 'T * 'T -> 'T; Default: 'T }

module Timeline =
    let sort (timeline: Timeline<'T>): Timeline<'T> =
        timeline |> Seq.sortBy (fun x -> x.Position)

    let map<'TSource, 'TDest> (mapFunction: 'TSource -> 'TDest) (timeline: Timeline<'TSource>): Timeline<'TDest> =
        timeline
        |> Seq.map (fun x ->
            { Position = x.Position
              Value = mapFunction x.Value })

    let cutAfter (position: Position) (timeline: Timeline<'T>): Timeline<'T> =
        timeline
        |> Seq.where (fun item -> item.Position < position)

    let slice (position: Position, duration: Duration) (timeline: Timeline<'T>): Timeline<'T> =
        timeline
        |> Seq.filter (fun x ->
            x.Position
            >= position
            && x.Position < position + duration)

    let shift (offset: Position) (timeline: Timeline<'T>): Timeline<'T> =
        timeline
        |> Seq.map (fun x ->
            { x with
                  Position = x.Position + offset })

    let shiftSlice (position: Position, duration: Duration) (timeline: Timeline<'T>): Timeline<'T> =
        timeline
        |> slice (0.0, duration)
        |> shift position

    let withDuration<'T> (duration: Duration) (timeline: Timeline<'T>): Timeline<DurationItem<'T>> =
        let sortedTimeline = timeline |> cutAfter duration |> sort
        match sortedTimeline with
        | empty when empty |> Seq.isEmpty -> Seq.empty
        | _ ->
            let lastItem = sortedTimeline |> Seq.last
            seq {
                yield! sortedTimeline
                       |> Seq.pairwise
                       |> Seq.map (fun (item, nextItem) ->
                           { Position = item.Position
                             Value =
                                 { Duration = nextItem.Position - item.Position
                                   Value = item.Value } })
                { Position = lastItem.Position
                  Value =
                      { Duration = duration - lastItem.Position
                        Value = lastItem.Value } }
            }

    let trim (def: 'T) (timeline: Timeline<'T>): Timeline<'T> =
        let rec removeDuplicates (innerTimeline: Timeline<'T>): Timeline<'T> =
            match innerTimeline |> Seq.tryHead with
            | Some timelineItem ->
                seq {
                    yield timelineItem
                    yield! innerTimeline
                           |> Seq.skip 1
                           |> Seq.skipWhile (fun x -> x.Value = timelineItem.Value)
                           |> removeDuplicates
                }
            | None -> Seq.empty

        timeline
        |> sort
        |> Seq.skipWhile (fun x -> x.Value = def)
        |> removeDuplicates

    let private getPositions (timeline: Timeline<'T>, positionBegin: Position, positionEnd: Position): seq<Position> =
        timeline
        |> Seq.filter (fun x ->
            x.Position
            >= positionBegin
            && x.Position < positionEnd)
        |> Seq.map (fun x -> x.Position)

    let union (source: Timeline<'T>, position: Position, duration: Duration) (dest: Timeline<'T>): Timeline<'T> =
        seq {
            yield! dest
            yield! source |> shiftSlice (position, duration)
        }

    let merge (source: Timeline<'T>, position: Position, duration: Duration, merger: TimelineMerger<'T>)
              (dest: Timeline<'T>)
              : Timeline<'T> =
        let fixedSource =
            seq {
                yield! source |> shiftSlice (position, duration) |> sort

                yield { Position = position + duration
                        Value = merger.Default }
            }

        let fixedDest = dest |> sort

        let positions =
            seq {
                yield! fixedSource
                yield! fixedDest
            }
            |> Seq.map (fun x -> x.Position)
            |> Seq.distinct
            |> Seq.sort

        let effective (effectivePosition: Position) (timeline: Timeline<'T>): 'T =
            let lastTimelineItem =
                timeline
                |> Seq.filter (fun x -> x.Position <= effectivePosition)
                |> Seq.tryLast

            match lastTimelineItem with
            | Some timelineItem -> timelineItem.Value
            | None -> merger.Default

        positions
        |> Seq.map (fun itemPosition ->
            let effectiveSourceItem = fixedSource |> effective itemPosition

            let effectiveTargetItem = fixedDest |> effective itemPosition

            { Position = itemPosition
              Value = merger.Merge(effectiveSourceItem, effectiveTargetItem) })
        |> trim merger.Default

    let aggregate<'T> (duration: Duration, combine: CombineFunction<'T>) (initialValue: 'T) (timeline: Timeline<'T>): 'T =
        timeline
        |> withDuration duration
        |> Seq.fold (fun aggregate x ->
            aggregate
            |> combine (x.Value.Value, x.Position, x.Value.Duration)) initialValue

    let flatten<'T> (duration: Duration, combine: TimelineCombineFunction<'T>)
                    (timeline: Timeline<Timeline<'T>>)
                    : Timeline<'T> =
        timeline
        |> aggregate (duration, combine) Seq.empty

    module Merger =
        let toCombineFunction<'T when 'T: equality> (merger: TimelineMerger<'T>): TimelineCombineFunction<'T> =
            fun (timeline: Timeline<'T>, position: Position, duration: Duration) ->
                merge (timeline, position, duration, merger)

        let additive: TimelineMerger<int> =
            { Merge = fun (v1, v2) -> v1 + v2
              Default = 0 }

        let multiplicative: TimelineMerger<double> =
            { Merge = fun (v1, v2) -> v1 * v2
              Default = 1.0 }
