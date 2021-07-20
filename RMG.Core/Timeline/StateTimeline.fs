namespace RMG.CoreF

open RMG.CoreF.Tuples

[<Sealed>]
type StateTimeline<'T when 'T: equality> private (items: 'T TimelineItem array) =
    member internal this.Items = items

    member this.Item
        with get index = this.Items.[index]

    static member internal ofArrayUnsafe(items: 'T TimelineItem array) : StateTimeline<'T> = StateTimeline items

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

module StateTimeline =
//    let shift<'T when 'T: equality> (offset: Position) (inputTimeline: 'T StateTimeline) : 'T StateTimeline =
//        inputTimeline.Items |> TimelineArray.shift offset |> StateTimeline.ofArrayUnsafe

//    let toPositions<'T when 'T: equality> (inputTimeline: 'T StateTimeline) : Position array = inputTimeline.Items |> TimelineArray.toPositions

    let tryFindEffectiveIndex<'T when 'T: equality> (position: Position) (inputTimeline: 'T StateTimeline) : int option =
        inputTimeline.Items |> TimelineArray.tryFindEffectiveIndex position

    let tryFindEffectiveValue<'T when 'T: equality> (position: Position) (inputTimeline: 'T StateTimeline) : 'T option =
        inputTimeline.Items |> TimelineArray.tryFindEffectiveValue position

    let private trimState<'T when 'T: equality> (inputTimeline: 'T TimelineItem array) : 'T TimelineItem array =
        match inputTimeline.Length with
        | length when length < 3 -> inputTimeline
        | _ ->
            let mutable newIndex = 0
            let newItems = Array.zeroCreate inputTimeline.Length
            newItems.[0] <- (inputTimeline |> Array.head)

            for index = 1 to inputTimeline.Length - 2 do
                let value = inputTimeline.[index]

                if not (value = newItems.[newIndex]) then
                    newIndex <- newIndex + 1
                    newItems.[newIndex] <- value

            newIndex <- newIndex + 1
            newItems.[newIndex] <- inputTimeline.[inputTimeline.Length - 1]
            Array.sub newItems 0 (newIndex + 1)

    let ofSeq<'T when 'T: equality> (duration: Duration) (merger: 'T -> 'T -> 'T) (defaultValue: 'T) (inputTimeline: 'T TimelineItem seq) : 'T StateTimeline =
        seq {
            yield struct (0.0, defaultValue)

            yield!
                inputTimeline
                |> Seq.filter (fun (struct (position, _)) -> position < duration)
                |> Seq.sortBy structFst

            yield (duration, defaultValue)
        }
        |> Seq.groupBy structFst
        |> Seq.map (fun (position, items) -> struct (position, items |> Seq.map structSnd |> Seq.fold merger defaultValue))
        |> Array.ofSeq
        |> trimState
        |> StateTimeline.ofArrayUnsafe

    let concat<'T when 'T: equality>
        (duration: Duration)
        (merger: 'T -> 'T -> 'T)
        (defaultValue: 'T)
        (inputTimeline: 'T StateTimeline Timeline)
        : 'T StateTimeline
        =
        let shiftedTimelines =
            inputTimeline.Items
            |> Array.map
                (fun (struct (position, timeline)) ->
                    timeline.Items
                    |> TimelineArray.shift position
                    |> TimelineArray.trimDuration duration)

        shiftedTimelines
        |> Seq.collect TimelineArray.toPositions
        |> Seq.append (
            seq {
                0.0
                duration
            }
        )
        |> Seq.distinct
        |> Seq.sort
        |> Seq.map
            (fun position ->
                let value =
                    if (position = duration) then
                        defaultValue
                    else
                        shiftedTimelines
                        |> Seq.choose (fun timeline -> timeline |> TimelineArray.tryFindEffectiveValue position)
                        |> Seq.fold merger defaultValue

                struct (position, value))
        |> Array.ofSeq
        |> trimState
        |> StateTimeline.ofArrayUnsafe

    let shiftValue<'T when 'T: equality> (value: 'T) (defaultValue: 'T) (merger: 'T -> 'T -> 'T) (inputTimeline: 'T StateTimeline) : 'T StateTimeline =
        if value = defaultValue then
            inputTimeline
        else
            let lastIndex = inputTimeline.Items.Length - 1

            inputTimeline.Items
            |> Array.mapi
                (fun index (struct (position, timelineValue)) ->
                    if index = lastIndex then
                        struct (position, timelineValue)
                    else
                        struct (position, merger timelineValue value))
            |> StateTimeline.ofArrayUnsafe
