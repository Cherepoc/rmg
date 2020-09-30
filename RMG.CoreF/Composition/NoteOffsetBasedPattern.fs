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
          PatternTimeline = Timeline.empty
          NoteOffsetPatternMapPatternTimeline = Timeline.empty
          NoteOffsetBasedPatternTimeline = Timeline.empty }

    let rec flatten<'T when 'T: equality> (duration: Duration)
                                          (combineInto: TimelineCombineFunction<'T>)
                                          (noteOffsetBasedPattern: NoteOffsetBasedPattern<'T>)
                                          : NoteOffsetBasedTimeline<'T> =
        let maxDuration =
            Math.Min(duration, noteOffsetBasedPattern.Duration)

        let flattenedPatterns =
            noteOffsetBasedPattern.PatternTimeline
            |> Timeline.withDuration maxDuration
            |> Timeline.map (fun (pattern, duration) ->
                pattern |> Pattern.flatten duration combineInto :> TimelineLike<'T>)
            |> Timeline.flatten maxDuration combineInto

        let flattenedNoteOffsetPatternMapPatterns =
            noteOffsetBasedPattern.NoteOffsetPatternMapPatternTimeline
            |> Timeline.withDuration maxDuration
            |> Timeline.map (fun (pattern, duration) ->
                pattern
                |> Pattern.map (NoteOffsetPatternMap.flatten duration)
                |> Pattern.flattenAggregate duration NoteOffsetTimelineMap.blendInto NoteOffsetTimelineMap.empty)
            |> NoteOffsetTimelineMap.flatten maxDuration

        let noteOffsetBasedPatterns =
            noteOffsetBasedPattern.NoteOffsetBasedPatternTimeline
            |> Timeline.withDuration maxDuration
            |> Timeline.map (fun (pattern, duration) -> pattern |> flatten duration combineInto)
            |> NoteOffsetBasedTimeline.flatten maxDuration combineInto

        { Timeline =
              flattenedPatterns
              |> combineInto
                  noteOffsetBasedPatterns.Timeline
                     { Position = 0.0
                       Duration = maxDuration }
              |> Timeline.fromSequence
          NoteOffsetTimelineMap =
              flattenedNoteOffsetPatternMapPatterns
              |> NoteOffsetTimelineMap.blendInto
                  noteOffsetBasedPatterns.NoteOffsetTimelineMap
                     { Position = 0.0
                       Duration = maxDuration } }
