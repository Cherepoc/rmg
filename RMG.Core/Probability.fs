namespace RMG.CoreF

open System
open RMG.CoreF.Tuples

module Probability =
    type IntProbabilityFunction = int -> float

    let geometricIntProbabilityFunction struct (minProbability: float, maxProbability: float, probabilityMultiplier: float, offset: int) (value: int) : float =
        (probabilityMultiplier ** (abs (double (value - offset))))
        * (maxProbability - minProbability)
        + minProbability

    let getWeights struct (probabilityFunction: IntProbabilityFunction, minValue: int, maxValue: int) : seq<float> =
        seq {
            for value = minValue to maxValue do
                yield probabilityFunction value
        }

    let withWeights<'T> (probabilityFunction: IntProbabilityFunction) (items: seq<'T>) =
        items
        |> Seq.indexed
        |> Seq.map (fun (index, item) -> struct (item, probabilityFunction index))

    let test (value: float) (referenceValue: float) : bool =
        if value <= 0.0 then false
        else if value >= 1.0 then true
        else value < referenceValue

    let pickWeightedIndex (value: float) (weights: seq<float>) : int =
        let weightsArray = weights |> Seq.toArray

        if value <= 0.0 then
            0
        else if value >= 1.0 then
            weightsArray.Length - 1
        else
            let weightSum = weightsArray |> Array.sum

            let struct (index, _, _) =
                weightsArray
                |> Seq.indexed
                |> Seq.map
                    (fun (index, weight) ->
                        let minWeight = weightsArray |> Seq.take index |> Seq.sum

                        struct (index, minWeight / weightSum, (minWeight + weight) / weightSum))
                |> Seq.find (fun struct (_, minWeight, maxWeight) -> value >= minWeight && value < maxWeight)

            index

    let pickWeighted<'T> (value: float) (weightedItems: seq<struct('T * float)>) : 'T =
        let weightedItemsArray = weightedItems |> Seq.toArray
        let weights = weightedItemsArray |> Seq.map structSnd
        let index = pickWeightedIndex value weights
        let struct (value, _) = weightedItemsArray.[index]
        value

    let pickRank (probabilityFunction: IntProbabilityFunction) (value: float) (ranks: seq<int>) : int =
        ranks
        |> Seq.map (fun rank -> struct (rank, probabilityFunction rank))
        |> pickWeighted value

    let weightIndexPickItem<'T> (probabilityFunction: IntProbabilityFunction) (value: float) (items: seq<'T>) : 'T =
        items
        |> Seq.indexed
        |> Seq.map (fun (index, item) -> struct (item, probabilityFunction index))
        |> pickWeighted value

    let pickItem<'T> (value: float) (items: seq<'T>) : 'T =
        let itemArray = items |> Seq.toArray
        let index = int (value / (double itemArray.Length))
        itemArray.[index]

    let private primes = [ 3; 5; 7; 11; 13 ]

    let private rhythmPeriodValues: float array =
        let primeValues =
            primes
            |> List.map
                (fun x ->
                    let nearestPower = round (Math.Log2(float x))
                    let nearestDivider = 2.0 ** nearestPower
                    let values = [ float x / nearestDivider; nearestDivider / float x ]
                    (values |> List.min, values |> List.max))

        seq {
            yield! primeValues |> List.rev |> List.map fst
            yield 1.0
            yield! primeValues |> List.map snd
        }
        |> Array.ofSeq

    let rhythmPeriod (index: int) : float =
        let trimIndex =
            match index + primes.Length with
            | index when index >= rhythmPeriodValues.Length -> rhythmPeriodValues.Length - 1
            | index when index <= 0 -> 0
            | index -> index

        rhythmPeriodValues.[trimIndex]

    let private rankTimelines: int Timeline array =
        let maxRank = 8

        let rankTimeline (rank: int) : int Timeline =
            seq {
                yield struct (0.0, 0)

                for currentRank = 1 to rank do
                    let period = 2.0 ** (1.0 - float currentRank)
                    let maxItem = 1.0 / period - 1.0
                    let phase = period / 2.0

                    for i = 0.0 to maxItem do
                        yield (phase + period * i, currentRank)
            }
            |> Timeline.ofSeq

        Array.init (maxRank + 1) rankTimeline

    let rankTimeline (maxRank: int) (phase: float) (period: float) (duration: float) : int Timeline =
        let lastCycleIndex = ceil (duration / period) - 1.0

        let timeline =
            rankTimelines.[maxRank]
            |> Timeline.phaseShift (phase / period) 1.0
            |> Timeline.scalePosition period

        seq {
            for cycle = 0.0 to lastCycleIndex do
                let position = period * cycle

                let cycleTimeline =
                    if cycle = lastCycleIndex then
                        timeline |> Timeline.trimDuration (duration - position)
                    else
                        timeline

                yield struct (position, cycleTimeline)
        }
        |> Timeline.ofSeq
        |> Timeline.concat

    let floatSpline (coef: float) (value: float) : float =
        match (coef, value) with
        | _, value when value > 1.0 || value < -1.0 -> invalidArg (nameof value) "Value should be between [-1;1]"
        | coef, _ when coef < 0.0 -> invalidArg (nameof coef) "Coef should be greater then or equal to zero"
        | _, _ ->
            let circlePower = if coef <= 1.0 then 1.0 else (1.0 / coef)
            let circleValue = (Math.Sqrt(1.0 - value ** 2.0) ** circlePower) - 1.0
            let correctedCircleValue = if value > 0.0 then -circleValue else circleValue

            if coef <= 1.0 then
                coef * correctedCircleValue + (1.0 - coef) * value
            else
                correctedCircleValue
