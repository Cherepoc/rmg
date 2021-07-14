namespace RMG.CoreF

[<Sealed>]
type EventStateTimeline internal (items: Event TimelineItem array, duration: Duration) =
    member internal this.items = items

    interface System.Collections.Generic.IEnumerable<TimelineItem<Event>> with
        member this.GetEnumerator() =
            (this.items :> System.Collections.Generic.IEnumerable<TimelineItem<Event>>)
                .GetEnumerator()

    interface System.Collections.IEnumerable with
        member this.GetEnumerator() =
            (this.items :> System.Collections.IEnumerable)
                .GetEnumerator()

    interface System.Collections.Generic.IReadOnlyCollection<TimelineItem<Event>> with
        member this.Count = this.items.Length

    interface System.Collections.Generic.IReadOnlyList<TimelineItem<Event>> with
        member this.Item
            with get index = this.items.[index]

    member this.Count = this.items.Length
    member this.Duration = duration

module EventStateTimeline =
    let private trim (defaultValue: Event) (inputTimeline: Event Timeline) : Event Timeline =
        let sorted =
            inputTimeline
            |> Seq.skipWhile (fun (_, value) -> value = defaultValue)
            |> Seq.toArray

        if sorted.Length <= 1 then
            sorted :> Event Timeline
        else
            seq {
                sorted |> Array.head

                for (_, previousValue), (position, value) in sorted |> Seq.pairwise do
                    if not (value = previousValue) then (position, value)
            }

    let private stateOfSeq (duration: Duration) (merger: CommonEventStateMerger) (sourceTimeline: Event Timeline) : Event Timeline =
        seq {
            yield!
                sourceTimeline
                |> Timeline.filter merger.Check
                |> Timeline.groupByPosition
                |> Timeline.choose merger.TryMerge

            yield (duration, merger.Default)
        }
        |> trim merger.Default

    let private effectiveByMerger (effectivePosition: Position) (defaultValue: Event) (inputTimeline: Event TimelineItem array) : Event =
        inputTimeline
        |> Array.tryFindBack (fun (position, _) -> position <= effectivePosition)
        |> Option.map snd
        |> Option.defaultValue defaultValue

    let private concatByMerger (merger: CommonEventStateMerger) (sourceTimeline: Event TimelineItem array array) : Event Timeline =
        let filteredTimelines =
            sourceTimeline
            |> Array.choose
                (fun timeline ->
                    let filteredTimeline =
                        timeline |> Array.filter (fun (_, value) -> merger.Check value)

                    if filteredTimeline |> Array.isEmpty then
                        None
                    else
                        Some filteredTimeline)

        filteredTimelines
        |> Seq.collect Timeline.toPositions
        |> Seq.distinct
        |> Seq.sort
        |> Seq.map
            (fun position ->
                let value =
                    filteredTimelines
                    |> Seq.map (fun timeline -> timeline |> effectiveByMerger position merger.Default)
                    |> merger.TryMerge
                    |> Option.defaultValue merger.Default

                (position, value))
        |> trim merger.Default

    let empty : EventStateTimeline = EventStateTimeline(Array.empty, 0.0)

    let ofSeq (duration: Duration) (sourceTimeline: Event Timeline) : EventStateTimeline =
        let items =
            sourceTimeline
            |> Timeline.trimDuration duration
            |> Timeline.sort
            |> Seq.groupBy (fun (_, value) -> Event.isState value)
            |> Seq.collect
                (fun (isState, events) ->
                    if isState then
                        let array = Array.ofSeq events
                        Event.mergers |> Seq.collect (fun merger -> array |> stateOfSeq duration merger)
                    else
                        events)
            |> Timeline.sort
            |> Array.ofSeq

        EventStateTimeline(items, duration)

    let concat (sourceTimeline: EventStateTimeline Timeline) : EventStateTimeline =
        let timelineArray = sourceTimeline |> Array.ofSeq

        let shiftedTimelines =
            timelineArray
            |> Array.map (fun (position, value) -> value |> Timeline.shift position |> Array.ofSeq)

        let result =
            seq {
                yield!
                    shiftedTimelines
                    |> Seq.collect (fun timeline -> timeline |> Timeline.filter Event.Note.check)

                yield!
                    Event.mergers
                    |> Seq.collect (fun merger -> shiftedTimelines |> concatByMerger merger)
            }
            |> Seq.sort
            |> Array.ofSeq

        let duration =
            timelineArray
            |> Timeline.sort
            |> Seq.tryLast
            |> Option.map (fun (position, timeline) -> position + timeline.Duration)
            |> Option.defaultValue 0.0

        EventStateTimeline(result, duration)

    let effectiveState (effectivePosition: Position) (sourceTimeline: EventStateTimeline) : list<Event> =
        Event.mergers
        |> List.map
            (fun merger ->
                sourceTimeline
                |> Timeline.filter merger.Check
                |> Array.ofSeq
                |> effectiveByMerger effectivePosition merger.Default)

    let concatCombined (sourceTimeline: EventStateTimeline WithEvents Timeline) : EventStateTimeline =
        sourceTimeline
        |> Timeline.collect
            (fun (events, timeline) ->
                seq {
                    timeline
                    events |> Timeline.fromMultiple |> ofSeq timeline.Duration
                })
        |> concat
