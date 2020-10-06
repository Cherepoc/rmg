namespace RMG.CoreF.Composition


open RMG.CoreF

[<Sealed>]
type NoteOffsetStateBasedEventPattern<'T> private (duration: Duration,
                                                   patternTimeline: EventTimeline<EventPattern<'T>>,
                                                   noteOffsetStatePatternMapPatternTimeline: EventTimeline<EventPattern<NoteOffsetStatePatternMap>>,
                                                   noteOffsetBasedPatternTimeline: EventTimeline<NoteOffsetStateBasedEventPattern<'T>>,
                                                   flatTimeline: NoteOffsetStateBasedEventTimeline<'T>) =
    member this.Duration = duration
    member this.PatternTimeline = patternTimeline
    member this.NoteOffsetStatePatternMapPatternTimeline = noteOffsetStatePatternMapPatternTimeline
    member this.NoteOffsetBasedPatternTimeline = noteOffsetBasedPatternTimeline

    member this.FlatTimeline = flatTimeline

    new(duration: Duration,
        patternTimeline: Timeline<EventPattern<'T>>,
        noteOffsetStatePatternMapPatternTimeline: Timeline<EventPattern<NoteOffsetStatePatternMap>>,
        noteOffsetBasedPatternTimeline: Timeline<NoteOffsetStateBasedEventPattern<'T>>) =
        let orderedPatternTimeline =
            patternTimeline |> EventTimeline.fromSequence

        let orderedNoteOffsetStatePatternMapPatternTimeline =
            noteOffsetStatePatternMapPatternTimeline
            |> EventTimeline.fromSequence

        let orderedNoteOffsetBasedPatternTimeline =
            noteOffsetBasedPatternTimeline
            |> EventTimeline.fromSequence

        let flatTimeline =
            { NoteOffsetStateBasedEventTimeline.Timeline =
                  seq {
                      yield! orderedPatternTimeline
                             |> Timeline.map (fun x -> x.FlatTimeline)
                      yield! orderedNoteOffsetBasedPatternTimeline
                             |> Timeline.map (fun x -> x.FlatTimeline.Timeline)
                  }
                  |> EventTimeline.merge
              NoteOffsetStateBasedEventTimeline.NoteOffsetStateTimelineMap =
                  seq {
                      yield! orderedNoteOffsetStatePatternMapPatternTimeline
                             |> Timeline.map (fun x -> x.FlatTimeline)
                             |> Timeline.merge
                             |> Timeline.map (fun x -> x.FlatTimelineMap)
                      yield! orderedNoteOffsetBasedPatternTimeline
                             |> Timeline.map (fun x -> x.FlatTimeline.NoteOffsetStateTimelineMap)
                  }
                  |> NoteOffsetStateTimelineMap.merge }

        NoteOffsetStateBasedEventPattern
            (duration,
             orderedPatternTimeline,
             orderedNoteOffsetStatePatternMapPatternTimeline,
             orderedNoteOffsetBasedPatternTimeline,
             flatTimeline)

    new() =
        NoteOffsetStateBasedEventPattern
            (0.0, EventTimeline.empty, EventTimeline.empty, EventTimeline.empty, NoteOffsetStateBasedEventTimeline.empty)

[<NoEquality>]
[<NoComparison>]
type NoteOffsetStateBasedEventPatternInput<'T> =
    { PatternTimelineInput: Timeline<EventPattern<'T>>
      NoteOffsetStatePatternMapPatternTimelineInput: Timeline<EventPattern<NoteOffsetStatePatternMap>>
      NoteOffsetBasedPatternTimelineInput: Timeline<NoteOffsetStateBasedEventPattern<'T>> }

module NoteOffsetStateBasedEventPattern =
    let empty<'T> = NoteOffsetStateBasedEventPattern<'T>()

    let fromInput<'T> (duration: Duration) (input: NoteOffsetStateBasedEventPatternInput<'T>) =
        NoteOffsetStateBasedEventPattern<'T>
            (duration,
             input.PatternTimelineInput,
             input.NoteOffsetStatePatternMapPatternTimelineInput,
             input.NoteOffsetBasedPatternTimelineInput)
