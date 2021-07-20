namespace RMG.CoreF

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
