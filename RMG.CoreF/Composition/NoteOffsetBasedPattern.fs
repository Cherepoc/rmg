namespace RMG.CoreF.Composition


open System
open RMG.CoreF


[<NoEquality>]
[<NoComparison>]
type NoteOffsetBasedPattern<'T> =
    { Duration: Duration
      PatternTimeline: Timeline<Pattern<'T>>
      NoteOffsetPatternMapPatternTimeline: Timeline<Pattern<NoteOffsetPatternMap>>
      NoteOffsetBasedPatternTimeline: Timeline<NoteOffsetBasedPattern<'T>> }

module NoteOffsetBasedPattern =
    let empty<'T> : NoteOffsetBasedPattern<'T> =
        { Duration = 0.0
          PatternTimeline = Seq.empty
          NoteOffsetPatternMapPatternTimeline = Seq.empty
          NoteOffsetBasedPatternTimeline = Seq.empty }

    let rec flatten<'T when 'T: equality> (duration: Duration, combineFunction: TimelineCombineFunction<'T>)
                                          (noteOffsetBasedPattern: NoteOffsetBasedPattern<'T>)
                                          : NoteOffsetBasedTimeline<'T> =
        let maxDuration =
            Math.Min(duration, noteOffsetBasedPattern.Duration)

        let flattenedPatterns =
            noteOffsetBasedPattern.PatternTimeline
            |> Timeline.cutAfter maxDuration
            |> Timeline.sort
            |> Timeline.map (fun x -> x |> Pattern.flatten (x.Duration, combineFunction))
            |> Timeline.flatten (maxDuration, combineFunction)

        let flattenedNoteOffsetPatternMapPatterns =
            noteOffsetBasedPattern.NoteOffsetPatternMapPatternTimeline
            |> Timeline.cutAfter maxDuration
            |> Timeline.sort
            |> Timeline.map (fun x ->
                x
                |> Pattern.map (NoteOffsetPatternMap.flatten x.Duration)
                |> Pattern.flattenAggregate (x.Duration, NoteOffsetTimelineMap.merge) NoteOffsetTimelineMap.empty)
            |> NoteOffsetTimelineMap.flatten maxDuration

        let noteOffsetBasedPatterns =
            noteOffsetBasedPattern.NoteOffsetBasedPatternTimeline
            |> Timeline.cutAfter maxDuration
            |> Timeline.sort
            |> Timeline.map (fun x -> x |> flatten (x.Duration, combineFunction))
            |> NoteOffsetBasedTimeline.flatten (maxDuration, combineFunction)

        { Timeline =
              flattenedPatterns
              |> combineFunction (noteOffsetBasedPatterns.Timeline, 0.0, maxDuration)
          NoteOffsetTimelineMap =
              flattenedNoteOffsetPatternMapPatterns
              |> NoteOffsetTimelineMap.merge (noteOffsetBasedPatterns.NoteOffsetTimelineMap, 0.0, maxDuration) }
