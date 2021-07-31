namespace RMG.CoreF

open System
open RMG.CoreF.Probability
open RMG.CoreF.Tuples

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

    let private primes = [ 3; 5; 7; 11; 13 ]

    let private rhythmPeriodValues : float array =
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

    let private rankTimelines : int Timeline array =
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

    let timeline<'T>
        (itemFunction: Context -> int -> 'T)
        struct (probabilityFunction: IntProbabilityFunction, maxRank: int, offset: float, period: float, duration: float)
        (context: Context)
        : Timeline<'T>
        =
        rankTimeline maxRank offset period duration
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
