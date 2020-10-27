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

    let pickWeightedWithRemainder<'T> (value: float) (items: seq<'T * float>) =
        let itemArray = items |> Seq.toArray

        let weightSum =
            itemArray
            |> Seq.map (fun (_, weight) -> weight)
            |> Seq.sum

        let (item, weight, minWeight) =
            itemArray
            |> Seq.indexed
            |> Seq.map (fun (index, (item, weight)) ->
                let minWeight =
                    itemArray
                    |> Seq.take index
                    |> Seq.map (fun (_, weight) -> weight)
                    |> Seq.sum

                (item, weight / weightSum, minWeight / weightSum))
            |> Seq.find (fun (_, weight, minWeight) -> value >= minWeight && value < minWeight + weight)

        (item, (value - minWeight) / weight)

    let pickItemWeighted<'T> (probabilityFunction: IntProbabilityFunction) (value: float) (items: seq<'T>): 'T =
        let weightedItems =
            items
            |> Seq.indexed
            |> Seq.map (fun (index, item) -> (item, probabilityFunction index))
            |> Seq.toArray

        if value <= 0.0 then
            let (item, _) = weightedItems |> Seq.head
            item
        else if value >= 1.0 then
            let (item, _) = weightedItems |> Seq.last
            item
        else
            let weightSum =
                weightedItems
                |> Seq.map (fun (_, weight) -> weight)
                |> Seq.sum

            let (item, _, _) =
                weightedItems
                |> Seq.indexed
                |> Seq.map (fun (index, (item, weight)) ->
                    let minWeight =
                        weightedItems
                        |> Seq.take index
                        |> Seq.map (fun (_, weight) -> weight)
                        |> Seq.sum

                    (item, minWeight / weightSum, (minWeight + weight) / weightSum))
                |> Seq.find (fun (_, minWeight, maxWeight) -> value >= minWeight && value < maxWeight)

            item

    let pickItem<'T> (value: float) (items: seq<'T>): 'T =
        let itemArray = items |> Seq.toArray
        let index = int (value / (double itemArray.Length))
        itemArray.[index]
