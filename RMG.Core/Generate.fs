namespace RMG.CoreF

open System
open MathNet.Numerics.Distributions
open RMG.CoreF.Probability
open RMG.CoreF.Tuples

module Generate =
    [<Sealed>]
    type Context(random: Random) =
        member this.Random = random
        member this.GetProbability() = random.NextDouble()
        member this.GetInt(min: int, max: int) = random.Next(min, max)
        new() = Context(Random())

    let private primes = [ 2; 3; 5; 7; 11; 13 ]

    let rhythmPeriod struct (probabilityFunction: IntProbabilityFunction, maxValue: int) (context: Context) : float =
        let selectedPrime =
            primes
            |> Seq.takeWhile (fun prime -> prime <= maxValue)
            |> weightIndexPickItem probabilityFunction (context.GetProbability())

        let nearestPower = round (Math.Log2(float selectedPrime))
        let nearestDivider = 2.0 ** nearestPower

        seq {
            float selectedPrime / nearestDivider
            nearestDivider / float selectedPrime
        }
        |> pickItem (context.GetProbability())

    let rhythmValue
        struct (probabilityFunction: IntProbabilityFunction, offset: float, period: float, min: float, max: float, maxRank: int)
        (context: Context)
        : float
        =
        let normalizedMin = (min - offset) / period
        let normalizedMax = (max - offset) / period

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

        (rankPeriod * float (stepIndex + stepMin) + rankPeriod * 0.5) * period + offset

    let timeline<'T>
        (itemFunction: Context -> int -> 'T)
        struct (probabilityFunction: IntProbabilityFunction, maxRank: int, offset: float, period: float, duration: float)
        (context: Context)
        : Timeline<'T>
        =
        let convertPosition struct (normalizedPosition: float, cycle: int) =
            (normalizedPosition * period + offset) % period + float cycle * period

        let testPosition struct (normalizedPosition: float, cycle: int) =
            let position = convertPosition (normalizedPosition, cycle)

            position >= 0.0 && position < duration

        let rec generate struct (normalizedPosition: float, rankScale: float, rank: int, cycle: int) : 'T TimelineItem seq =
            let position = convertPosition struct (normalizedPosition, cycle)

            let probability = probabilityFunction rank

            seq {
                if probability |> test (context.GetProbability()) then
                    yield struct (position, itemFunction context rank)

                if rank < maxRank then
                    let childRank = rank + 1
                    let childRankScale = rankScale / 2.0

                    let leftPosition = normalizedPosition - childRankScale

                    if testPosition struct (leftPosition, cycle) then
                        yield! generate struct (leftPosition, childRankScale, childRank, cycle)

                    let rightPosition = normalizedPosition + childRankScale

                    if testPosition struct (rightPosition, cycle) then
                        yield! generate struct (rightPosition, childRankScale, childRank, cycle)
            }

        let cycleCount = int (ceil (duration / period))

        seq {
            for cycle = 0 to cycleCount do
                yield! generate struct (0.0, 1.0, 0, cycle)
        }
        |> Timeline.ofSeq

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

    let float struct (min: float, max: float) (context: Context) : float = (context.GetProbability()) * (max - min) + min

    let int struct (min: int, max: int) (context: Context) : int = (context.GetInt(min, max + 1))

    let intByRank struct (probabilityFunction: IntProbabilityFunction, min: int, max: int) (context: Context) : int =
        seq { min .. max } |> pickRank probabilityFunction (context.GetProbability())

    let item<'T> (sequence: seq<'T>) (context: Context) : 'T =
        let array = sequence |> Seq.toArray
        let index = (context.GetInt(0, array.Length))
        array.[index]

    let normalFloat (mean: float, stddev: float) (context: Context) : float = Normal.Sample(context.Random, mean, stddev)

    let test (value: float) (context: Context) : Boolean =
        if test (context.GetProbability()) value then
            true
        else
            false
