namespace RMG.Core

open System
open RMG.Core.Probability
open RMG.Core.Tuples

module Generate =
    [<Sealed>]
    type Context(random: Random) =
        member this.Random = random
        member this.GetProbability() = random.NextDouble()
        member this.GetInt(min: int, max: int) = random.Next(min, max)
        new() = Context(Random())

    let intByRank struct (probabilityFunction: IntProbabilityFunction, min: int, max: int) (context: Context) : int =
        seq { min .. max } |> pickRank probabilityFunction (context.GetProbability())

    let item<'T> (sequence: seq<'T>) (context: Context) : 'T =
        let array = sequence |> Seq.toArray
        let index = (context.GetInt(0, array.Length))
        array.[index]

    let test (value: float) (context: Context) : Boolean =
        if test (context.GetProbability()) value then
            true
        else
            false

    let pickRhythmPeriod struct (probabilityFunction: IntProbabilityFunction, maxIndex: int) (context: Context) : float =
        context |> intByRank (probabilityFunction, -maxIndex, maxIndex) |> rhythmPeriod

    let rhythmValue
        struct (probabilityFunction: IntProbabilityFunction, phase: float, period: float, min: float, max: float, maxRank: int)
        (context: Context)
        : float
        =
        let normalizedMin = (min - phase) / period
        let normalizedMax = (max - phase) / period

        let calculateRank (rank: int) =
            let rankPeriod = 0.5 ** double rank
            let rankOffset = rankPeriod * 0.5

            let stepMin = int (ceil ((normalizedMin - rankOffset) / rankPeriod))

            let stepMax = int (floor ((normalizedMax - rankOffset) / rankPeriod))
            struct (rankPeriod, stepMin, stepMax)

        let rank =
            seq { 0 .. maxRank }
            |> Seq.filter
                (fun rank ->
                    let struct (_, stepMin, stepMax) = calculateRank rank
                    stepMin <= stepMax)
            |> pickRank probabilityFunction (context.GetProbability())

        let struct (rankPeriod, stepMin, stepMax) = calculateRank rank
        let stepCount = stepMax - stepMin + 1
        let stepIndex = context.GetInt(0, stepCount)

        (rankPeriod * float (stepIndex + stepMin) + rankPeriod * 0.5) * period + phase

    let fromRankTimeline<'T>
        (context: Context)
        (itemFunction: Context -> int -> 'T)
        (probabilityFunction: IntProbabilityFunction)
        (timeline: int Timeline)
        : 'T Timeline
        =
        timeline
        |> Timeline.choose
            (fun rank ->
                let probability = probabilityFunction rank

                if context |> test probability then
                    Some(itemFunction context rank)
                else
                    None)

    let sequentialTimeline<'T> (itemFunction: Context -> struct ('T * Duration)) (duration: Duration) (context: Context) : Timeline<'T> =
        let mutable list = List.empty
        let mutable durationSum = 0.0

        while durationSum < duration do
            let struct (item, itemDuration) = itemFunction context

            list <- list |> List.append [ struct (durationSum, item) ]

            durationSum <- durationSum + itemDuration

        list |> Timeline.ofSeq

    let sequence<'T> (itemFunction: Context -> 'T) (count: int) (context: Context) : seq<'T> =
        seq {
            for _ = 1 to count do
                itemFunction context
        }

    let rec subSequence<'T> (sequence: seq<'T>) (count: int) (context: Context) : seq<'T> =
        let array = sequence |> Seq.toArray

        if count <= 0 || array.Length <= 0 then
            Seq.empty
        else
            let index = (context.GetInt(0, array.Length))

            let remainder =
                seq {
                    yield! array |> Seq.take index
                    yield! array |> Seq.skip (index + 1)
                }

            seq {
                yield array.[index]
                yield! subSequence remainder (count - 1) context
            }

    let rec subSequenceWeighted<'T> (sequence: seq<struct ('T * float)>) (count: int) (context: Context) : seq<'T> =
        let array = sequence |> Seq.toArray

        if count <= 0 || array.Length <= 0 then
            Seq.empty
        else
            let weights = sequence |> Seq.map structSnd
            let index = pickWeightedIndex (context.GetProbability()) weights

            let remainder =
                seq {
                    yield! array |> Seq.take index
                    yield! array |> Seq.skip (index + 1)
                }

            let struct (item, _) = array.[index]

            seq {
                yield item
                yield! subSequenceWeighted remainder (count - 1) context
            }

    let int struct (min: int, max: int) (context: Context) : int = (context.GetInt(min, max + 1))

    let float struct (min: float, max: float) (context: Context) : float = (context.GetProbability()) * (max - min) + min
