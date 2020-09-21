namespace RMG.CoreF.Composition

open System
open RMG.CoreF

[<NoEquality>]
[<NoComparison>]
type Pattern<'T> =
    { Duration: Duration
      Timeline: Timeline<'T>
      PatternTimeline: Timeline<Pattern<'T>> }

module Pattern =
    let empty<'T> : Pattern<'T> =
        { Duration = 0.0
          Timeline = Seq.empty
          PatternTimeline = Seq.empty }

    let rec map<'TSource, 'TDest> (mapFunction: 'TSource -> 'TDest) (pattern: Pattern<'TSource>) : Pattern<'TDest> =
        { Duration = pattern.Duration
          Timeline = pattern.Timeline |> Timeline.map mapFunction
          PatternTimeline = pattern.PatternTimeline |> Timeline.map (map mapFunction) }

    let rec flatten<'T> (duration: Duration, combineFunction: TimelineCombineFunction<'T>)
                        (pattern: Pattern<'T>)
                        : Timeline<'T> =
        let maxDuration = Math.Min(duration, pattern.Duration)

        let flattenedPatterns =
            pattern.PatternTimeline
            |> Timeline.cutAfter maxDuration
            |> Timeline.sort
            |> Timeline.map (fun x -> x |> flatten (x.Duration, combineFunction))
            |> Timeline.flatten (maxDuration, combineFunction)

        pattern.Timeline
        |> combineFunction (flattenedPatterns, 0.0, maxDuration)

    let rec flattenAggregate<'T> (duration: Duration, combineFunction: CombineFunction<'T>)
                                 (initialValue: 'T)
                                 (pattern: Pattern<'T>)
                                 : 'T =
        let maxDuration = Math.Min(duration, pattern.Duration)

        let flattenedPatterns =
            pattern.PatternTimeline
            |> Timeline.withDuration maxDuration
            |> Timeline.map (fun x ->
                x.Value
                |> flattenAggregate (x.Duration, combineFunction) initialValue)
            |> Timeline.aggregate (maxDuration, combineFunction) initialValue

        pattern.Timeline
        |> Timeline.aggregate (maxDuration, combineFunction) initialValue
        |> combineFunction (flattenedPatterns, 0.0, maxDuration)
