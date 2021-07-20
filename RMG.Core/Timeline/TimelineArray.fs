namespace RMG.CoreF

open RMG.CoreF.Tuples

type TimelineItem<'T> = (struct (Position * 'T))

module internal TimelineArray =
    let toPositions<'T> (inputTimeline: 'T TimelineItem array) : Position array = inputTimeline |> Array.map structFst

    let map<'TSource, 'TDest> (func: 'TSource -> 'TDest) (inputTimeline: 'TSource TimelineItem array) : 'TDest TimelineItem array =
        inputTimeline
        |> Array.map (fun (struct (position, value)) -> (position, func value))

//    let collect<'TSource, 'TDest> (func: 'TSource -> 'TDest seq) (inputTimeline: 'TSource TimelineItem array) : 'TDest TimelineItem array =
//        inputTimeline
//        |> Seq.collect
//            (fun (struct (position, value)) ->
//                (func value)
//                |> Seq.map (fun convertedValue -> struct (position, convertedValue)))
//        |> Array.ofSeq

//    let filter<'T> (func: 'T -> bool) (inputTimeline: 'T TimelineItem array) : 'T TimelineItem array =
//        inputTimeline |> Array.filter (fun (struct (_, value)) -> func value)

    let choose<'TSource, 'TDest> (func: 'TSource -> 'TDest option) (inputTimeline: 'TSource TimelineItem array) : 'TDest TimelineItem array =
        inputTimeline
        |> Array.choose (fun (struct (position, value)) -> func value |> Option.map (fun mappedValue -> (position, mappedValue)))

    let shift<'T> (offset: Position) (inputTimeline: 'T TimelineItem array) : 'T TimelineItem array =
        inputTimeline
        |> Array.map (fun (struct (position, value)) -> struct (position + offset, value))

    let tryFindEffectiveIndex<'T> (effectivePosition: Position) (inputTimeline: 'T TimelineItem array) : int option =
        let rec tryFindEffectiveInternal (minIndex: int, maxIndex: int) : int option =
            match (minIndex, maxIndex) with
            | minIndex, maxIndex when minIndex = maxIndex ->
                match inputTimeline.[minIndex] with
                | position, _ when position <= effectivePosition -> Some minIndex
                | _ -> None
            | minIndex, maxIndex when minIndex < maxIndex ->
                let midIndex = minIndex + (maxIndex - minIndex) / 2
                let struct (position, _) = inputTimeline.[midIndex]

                match position with
                | position when position > effectivePosition -> tryFindEffectiveInternal (minIndex, midIndex)
                | _ ->
                    tryFindEffectiveInternal (midIndex + 1, maxIndex)
                    |> Option.orElse (Some midIndex)
            | _ -> None

        tryFindEffectiveInternal (0, inputTimeline.Length - 1)

    let tryFindEffectiveValue<'T> (position: Position) (inputTimeline: 'T TimelineItem array) : 'T option =
        inputTimeline
        |> tryFindEffectiveIndex position
        |> Option.map
            (fun index ->
                let struct (_, value) = inputTimeline.[index]
                value)

    let trimDuration<'T> (duration: Duration) (inputTimeline: 'T TimelineItem array) : 'T TimelineItem array =
        let length =
            inputTimeline
            |> tryFindEffectiveIndex duration
            |> Option.map
                (fun index ->
                    let struct (position, _) = inputTimeline.[index]
                    if position = duration then index else index + 1)
            |> Option.defaultValue 0

        match length with
        | 0 -> Array.empty
        | length when length = inputTimeline.Length -> inputTimeline
        | _ -> Array.sub inputTimeline 0 length

    let itemOfSingle (value: 'T) : 'T TimelineItem = (0.0, value)

    let ofSingle (value: 'T) : 'T TimelineItem array = [| itemOfSingle value |]

    let ofMultiple (values: 'T seq) = values |> Seq.map itemOfSingle |> Array.ofSeq
