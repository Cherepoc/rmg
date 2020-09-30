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
          Timeline = Timeline.empty
          PatternTimeline = Timeline.empty }

    let rec map<'TSource, 'TDest> (mapFunction: 'TSource -> 'TDest) (pattern: Pattern<'TSource>): Pattern<'TDest> =
        { Duration = pattern.Duration
          Timeline =
              pattern.Timeline
              |> Timeline.map mapFunction
              |> Timeline.fromSequence
          PatternTimeline =
              pattern.PatternTimeline
              |> Timeline.map (map mapFunction)
              |> Timeline.fromSequence }

    let rec flatten<'T> (duration: Duration)
                        (combineInto: TimelineCombineFunction<'T>)
                        (pattern: Pattern<'T>)
                        : TimelineSequence<'T> =
        let maxDuration = Math.Min(duration, pattern.Duration)

        let flattenedPatterns =
            pattern.PatternTimeline
            |> Timeline.withDuration maxDuration
            |> Timeline.map (fun (pattern, duration) -> pattern |> flatten duration combineInto :> TimelineLike<'T>)
            |> Timeline.flatten maxDuration combineInto

        pattern.Timeline
        |> combineInto
            flattenedPatterns
               { Position = 0.0
                 Duration = maxDuration }

    let rec flattenAggregate<'T> (duration: Duration)
                                 (combineInto: CombineFunction<'T>)
                                 (initialValue: 'T)
                                 (pattern: Pattern<'T>)
                                 : 'T =
        let maxDuration = Math.Min(duration, pattern.Duration)

        let flattenedPatterns =
            pattern.PatternTimeline
            |> Timeline.withDuration maxDuration
            |> Timeline.map (fun (pattern, duration) ->
                pattern
                |> flattenAggregate duration combineInto initialValue)
            |> Timeline.aggregate maxDuration combineInto initialValue

        pattern.Timeline
        |> Timeline.aggregate maxDuration combineInto initialValue
        |> combineInto
            flattenedPatterns
               { Position = 0.0
                 Duration = maxDuration }
