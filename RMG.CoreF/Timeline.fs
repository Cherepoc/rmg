namespace RMG.CoreF

type Position = float
type Duration = float

type Vector =
    { Position: Position
      Duration: Duration }

[<NoComparison>]
[<NoEquality>]
type TimelineItem<'T> = { Position: Position; Value: 'T }

type TimelineLike<'T> = seq<TimelineItem<'T>>

[<NoEquality>]
[<NoComparison>]
type TimelineBlender<'T> = { Blend: 'T * 'T -> 'T; Default: 'T }

[<NoComparison>]
[<NoEquality>]
[<Sealed>]
type Timeline<'T> internal (items: array<TimelineItem<'T>>) =
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

[<NoComparison>]
[<NoEquality>]
[<Sealed>]
type TimelineSequence<'T> internal (items: TimelineLike<'T>) =
    member internal this.items = items

    interface System.Collections.Generic.IEnumerable<TimelineItem<'T>> with
        member this.GetEnumerator() = this.items.GetEnumerator()

    interface System.Collections.IEnumerable with
        member this.GetEnumerator() =
            (this.items :> System.Collections.IEnumerable).GetEnumerator()

type CombineFunction<'T> = 'T -> Vector -> 'T -> 'T

type TimelineCombineFunction<'T> = TimelineLike<'T> -> Vector -> TimelineLike<'T> -> TimelineSequence<'T>

module Timeline =
    let empty<'T> = Timeline<'T>(Array.empty)

    let private emptySequence<'T> = TimelineSequence<'T>(Seq.empty)

    let inline private sort<'T> (inputTimeline: TimelineLike<'T>): TimelineLike<'T> =
        inputTimeline |> Seq.sortBy (fun x -> x.Position)

    let private ensureSorted<'T> (inputTimeline: TimelineLike<'T>): TimelineSequence<'T> =
        match inputTimeline with
        | :? (Timeline<'T>) as timeline -> TimelineSequence(timeline.items)
        | :? (TimelineSequence<'T>) as timeline -> timeline
        | _ -> TimelineSequence(inputTimeline |> sort)

    let private ensureSortedArray<'T> (inputTimeline: TimelineLike<'T>): array<TimelineItem<'T>> =
        let items =
            match inputTimeline with
            | :? (Timeline<'T>) as timeline -> timeline.items :> TimelineLike<'T>
            | :? (TimelineSequence<'T>) as timeline -> timeline.items
            | _ -> inputTimeline |> sort

        match items with
        | :? (array<TimelineItem<'T>>) as array -> array
        | _ -> items |> Seq.toArray

    let private asSequenceNoSort<'T> (inputTimeline: TimelineLike<'T>): TimelineSequence<'T> =
        match inputTimeline with
        | :? (Timeline<'T>) as timeline -> TimelineSequence(timeline.items)
        | :? (TimelineSequence<'T>) as timeline -> timeline
        | _ -> TimelineSequence(inputTimeline)

    let private groupByPosition<'T> (inputTimeline: TimelineLike<'T>): TimelineSequence<seq<'T>> =
        inputTimeline
        |> ensureSorted
        |> Seq.groupBy (fun item -> item.Position)
        |> Seq.map (fun (position, items) ->
            { Position = position
              Value = items |> Seq.map (fun item -> item.Value) })
        |> asSequenceNoSort

    let private blendAggregate<'T> (merger: TimelineBlender<'T>) (values: seq<'T>): 'T =
        values
        |> Seq.fold (fun agg x -> merger.Blend(agg, x)) merger.Default

    let fromSequence<'T> (inputTimeline: TimelineLike<'T>): Timeline<'T> =
        Timeline(inputTimeline |> ensureSortedArray)

    let map<'TSource, 'TDest> (func: 'TSource -> 'TDest) (inputTimeline: TimelineLike<'TSource>): TimelineSequence<'TDest> =
        inputTimeline
        |> ensureSorted
        |> Seq.map (fun item ->
            { Position = item.Position
              Value = func (item.Value) })
        |> asSequenceNoSort

    let trimDuration<'T> (duration: Duration) (inputTimeline: TimelineLike<'T>): TimelineSequence<'T> =
        inputTimeline
        |> ensureSorted
        |> Seq.filter (fun item -> item.Position < duration)
        |> asSequenceNoSort

    let shift (position: Position) (inputTimeline: TimelineLike<'T>): TimelineSequence<'T> =
        inputTimeline
        |> ensureSorted
        |> Seq.map (fun x ->
            { x with
                  Position = x.Position + position })
        |> asSequenceNoSort

    let shiftSlice (vector: Vector) (inputTimeline: TimelineLike<'T>): TimelineSequence<'T> =
        inputTimeline
        |> ensureSorted
        |> Seq.filter (fun item -> item.Position < vector.Duration)
        |> asSequenceNoSort
        |> shift vector.Position

    let withDuration<'T> (duration: Duration) (inputTimeline: TimelineLike<'T>): TimelineSequence<'T * Duration> =
        let sortedTimeline =
            inputTimeline
            |> ensureSorted
            |> trimDuration duration
            |> groupByPosition
            |> ensureSortedArray

        if sortedTimeline.Length = 0 then
            emptySequence
        else
            let lastItem = sortedTimeline |> Array.last

            let mapItems (position: Position, nextPosition: Position) (items: seq<'T>): TimelineLike<'T * Duration> =
                let duration = nextPosition - position
                items
                |> Seq.map (fun x ->
                    { Position = position
                      Value = (x, duration) })

            seq {
                yield! sortedTimeline
                       |> Seq.pairwise
                       |> Seq.map (fun (item, nextItem) ->
                           item.Value
                           |> mapItems (item.Position, nextItem.Position))
                       |> Seq.collect (fun item -> item)
                yield! lastItem.Value
                       |> mapItems (lastItem.Position, duration)
            }
            |> asSequenceNoSort

    let simplify<'T when 'T: equality> (merger: TimelineBlender<'T>)
                                       (inputTimeline: TimelineLike<'T>)
                                       : TimelineSequence<'T> =
        let sorted =
            inputTimeline
            |> ensureSorted
            |> groupByPosition
            |> map (fun values -> values |> blendAggregate merger)
            |> Seq.skipWhile (fun x -> x.Value = merger.Default)
            |> Seq.toArray

        if sorted.Length <= 1 then
            sorted |> asSequenceNoSort
        else
            seq {
                yield sorted |> Array.head

                yield! sorted
                       |> Seq.pairwise
                       |> Seq.choose (fun (previousItem, item) ->
                           if item.Value = previousItem.Value then None else Some item)
            }
            |> ensureSorted

    let insertInto (dest: TimelineLike<'T>) (vector: Vector) (source: TimelineLike<'T>): TimelineSequence<'T> =
        seq {
            yield! dest
            yield! source |> shiftSlice vector
        }
        |> ensureSorted

    let blendInto (dest: TimelineLike<'T>)
                  (vector: Vector)
                  (merger: TimelineBlender<'T>)
                  (source: TimelineLike<'T>)
                  : TimelineSequence<'T> =
        let sortedSource =
            seq {
                yield! source
                       |> ensureSorted
                       |> shiftSlice vector

                yield { Position = vector.Position + vector.Duration
                        Value = merger.Default }
            }
            |> ensureSortedArray

        let sortedDest = dest |> ensureSortedArray

        let effective (effectivePosition: Position) (timeline: array<TimelineItem<'T>>): seq<'T> =
            let lastEffectiveItem =
                timeline
                |> Seq.filter (fun item -> item.Position <= effectivePosition)
                |> Seq.tryLast

            match lastEffectiveItem with
            | Some effectiveItem ->
                timeline
                |> Seq.filter (fun item -> item.Position = effectiveItem.Position)
                |> Seq.map (fun item -> item.Value)
            | None -> Seq.empty

        seq {
            yield! sortedSource
            yield! sortedDest
        }
        |> Seq.map (fun x -> x.Position)
        |> Seq.distinct
        |> Seq.sort
        |> Seq.map (fun position ->
            let sourceValues = sortedSource |> effective position
            let destValues = sortedDest |> effective position
            { Position = position
              Value =
                  sourceValues
                  |> Seq.append destValues
                  |> blendAggregate merger })
        |> simplify merger
        |> asSequenceNoSort

    let aggregate<'T> (duration: Duration)
                      (combineInto: CombineFunction<'T>)
                      (initialValue: 'T)
                      (timeline: TimelineLike<'T>)
                      : 'T =
        timeline
        |> ensureSorted
        |> withDuration duration
        |> Seq.fold (fun aggregate item ->
            let (value, duration) = item.Value

            let vector =
                { Position = item.Position
                  Duration = duration }

            value |> combineInto aggregate vector) initialValue

    let flatten<'T> (duration: Duration)
                    (combineInto: TimelineCombineFunction<'T>)
                    (timelineInput: TimelineLike<TimelineLike<'T>>)
                    : TimelineSequence<'T> =
        let fixedCombine (dest: TimelineLike<'T>) (vector: Vector) (source: TimelineLike<'T>): TimelineLike<'T> =
            source |> combineInto dest vector :> TimelineLike<'T>

        timelineInput
        |> ensureSorted
        |> aggregate duration fixedCombine Seq.empty
        |> asSequenceNoSort

module TimelineBlender =
    let additive: TimelineBlender<int> =
        { Blend = fun (v1, v2) -> v1 + v2
          Default = 0 }

    let multiplicative: TimelineBlender<double> =
        { Blend = fun (v1, v2) -> v1 * v2
          Default = 1.0 }

    let toCombineFunction<'T when 'T: equality> (merger: TimelineBlender<'T>): TimelineCombineFunction<'T> =
        fun (timeline: TimelineLike<'T>) (vector: Vector) -> Timeline.blendInto timeline vector merger
