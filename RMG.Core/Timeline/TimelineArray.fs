namespace RMG.Core

open RMG.Core.Tuples

type TimelineItem<'T> = (struct (Position * 'T))

module internal TimelineArray =
    let toPositions<'T> (inputTimeline: 'T TimelineItem array) : Position array = inputTimeline |> Array.map structFst

    let map<'TSource, 'TDest> (func: 'TSource -> 'TDest) (inputTimeline: 'TSource TimelineItem array) : 'TDest TimelineItem array =
        inputTimeline
        |> Array.map (fun (struct (position, value)) -> (position, func value))

    let scalePosition<'T> (scaleValue: Position) (inputTimeline: 'T TimelineItem array) : 'T TimelineItem array =
        if (scaleValue <= 0.0) then
            invalidArg (nameof scaleValue) "Should be positive"
        else
            inputTimeline
            |> Array.map (fun (struct (position, value)) -> (position * scaleValue, value))

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

    let tryFindEffectiveValue<'T> (effectivePosition: Position) (inputTimeline: 'T TimelineItem array) : 'T option =
        inputTimeline
        |> tryFindEffectiveIndex effectivePosition
        |> Option.map
            (fun index ->
                let struct (_, value) = inputTimeline.[index]
                value)

    let tryFindReverseEffectiveIndex<'T> (effectivePosition: Position) (inputTimeline: 'T TimelineItem array) : int option =
        match inputTimeline |> tryFindEffectiveIndex effectivePosition with
        | Some effectiveIndex ->
            let struct (position, _) = inputTimeline.[effectiveIndex]

            match (position = effectivePosition, inputTimeline.Length) with
            | true, _ -> Some effectiveIndex
            | false, length when length > effectiveIndex + 1 -> Some(effectiveIndex + 1)
            | _, _ -> None
        | _ -> if inputTimeline.Length > 0 then Some 0 else None

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

    let phaseShift<'T> (phase: Position) (period: Position) (inputTimeline: 'T TimelineItem array) : 'T TimelineItem array =
        match (inputTimeline.Length, phase) with
        | 0, _ -> Array.empty
        | _, 0.0 -> inputTimeline
        | _, _ ->
            let correctedPhase = phase % period
            let breakPosition = period - correctedPhase

            let breakIndex = inputTimeline |> tryFindReverseEffectiveIndex breakPosition

            let lastIndex = inputTimeline.Length - 1
            let result : 'T TimelineItem array = Array.zeroCreate inputTimeline.Length
            let mutable resultIndex = 0

            match breakIndex with
            | Some phaseIndex ->
                for index = phaseIndex to lastIndex do
                    let struct (position, value) = inputTimeline.[index]
                    result.[resultIndex] <- struct (position - breakPosition, value)
                    resultIndex <- resultIndex + 1
            | _ -> ()

            let indexBeforeBreak =
                breakIndex
                |> Option.map (fun breakIndex -> breakIndex - 1)
                |> Option.defaultValue lastIndex

            for index = 0 to indexBeforeBreak do
                let struct (position, value) = inputTimeline.[index]
                result.[resultIndex] <- struct (position + correctedPhase, value)
                resultIndex <- resultIndex + 1

            result

    let itemOfSingle (value: 'T) : 'T TimelineItem = (0.0, value)

    let ofSingle (value: 'T) : 'T TimelineItem array = [| itemOfSingle value |]

    let ofMultiple (values: 'T seq) = values |> Seq.map itemOfSingle |> Array.ofSeq
