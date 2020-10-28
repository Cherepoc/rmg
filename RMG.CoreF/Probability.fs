namespace RMG.CoreF

module Probability =
    type IntProbabilityFunction = int -> float

    let geometricIntProbabilityFunction (minProbability: float,
                                         maxProbability: float,
                                         probabilityMultiplier: float,
                                         offset: int)
                                        (value: int)
                                        : float =
        (probabilityMultiplier
         ** (abs (double (value - offset))))
        * (maxProbability - minProbability)
        + minProbability

    let getWeights (probabilityFunction: IntProbabilityFunction, minValue: int, maxValue: int): seq<float> =
        seq {
            for value = minValue to maxValue do
                yield probabilityFunction value
        }

    let withWeights<'T> (probabilityFunction: IntProbabilityFunction) (items: seq<'T>) =
        items
        |> Seq.indexed
        |> Seq.map (fun (index, item) -> (item, probabilityFunction index))

    let test (value: float) (referenceValue: float): bool =
        if value <= 0.0 then false
        else if value >= 1.0 then true
        else value < referenceValue

    let pickWeighted<'T> (value: float) (weightedItems: seq<'T * float>): 'T =
        let weightedItemsArray = weightedItems |> Seq.toArray

        if value <= 0.0 then
            let (item, _) = weightedItemsArray |> Seq.head
            item
        else if value >= 1.0 then
            let (item, _) = weightedItemsArray |> Seq.last
            item
        else
            let weightSum =
                weightedItemsArray
                |> Seq.map (fun (_, weight) -> weight)
                |> Seq.sum

            let (item, _, _) =
                weightedItemsArray
                |> Seq.indexed
                |> Seq.map (fun (index, (item, weight)) ->
                    let minWeight =
                        weightedItemsArray
                        |> Seq.take index
                        |> Seq.map (fun (_, weight) -> weight)
                        |> Seq.sum

                    (item, minWeight / weightSum, (minWeight + weight) / weightSum))
                |> Seq.find (fun (_, minWeight, maxWeight) -> value >= minWeight && value < maxWeight)

            item

    let pickRank (probabilityFunction: IntProbabilityFunction) (value: float) (ranks: seq<int>): int =
        ranks
        |> Seq.map (fun rank -> (rank, probabilityFunction rank))
        |> pickWeighted value

    let weightIndexPickItem<'T> (probabilityFunction: IntProbabilityFunction) (value: float) (items: seq<'T>): 'T =
        items
        |> Seq.indexed
        |> Seq.map (fun (index, item) -> (item, probabilityFunction index))
        |> pickWeighted value

    let pickItem<'T> (value: float) (items: seq<'T>): 'T =
        let itemArray = items |> Seq.toArray
        let index = int (value / (double itemArray.Length))
        itemArray.[index]
