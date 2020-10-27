namespace RMG.CoreF

open System
open RMG.CoreF.Probability

module Generate =
    [<Sealed>]
    type Context(random: Random) =
        member this.Random = random
        member this.GetProbability = random.NextDouble
        new() = Context(Random())

    let private primes = [ 2; 3; 5; 7; 11; 13 ]

    let rhythmPeriod (context: Context) (probabilityFunction: IntProbabilityFunction, maxValue: int): float =
        let selectedPrime =
            primes
            |> Seq.takeWhile (fun prime -> prime <= maxValue)
            |> pickItemWeighted probabilityFunction (context.GetProbability())

        let nearestPower = round (Math.Log2(float selectedPrime))
        let nearestDivider = 2.0 ** nearestPower
        seq {
            float selectedPrime / nearestDivider
            nearestDivider / float selectedPrime
        }
        |> pickItem (context.GetProbability())
