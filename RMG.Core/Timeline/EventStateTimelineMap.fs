namespace RMG.CoreF

[<Sealed>]
type EventStateTimelineMap
    private
    (
        noteTimeline: EventState Timeline,
        durationTimeline: Duration StateTimeline,
        velocityTimeline: Velocity StateTimeline,
        articulationOffsetTimeline: ArticulationOffset StateTimeline,
        keyOffsetTimeline: KeyOffset StateTimeline,
        octaveOffsetTimeline: OctaveOffset StateTimeline,
        chordRootOffsetTimeline: ChordRootOffset StateTimeline,
        chordScaleOffsetsTimeline: ChordScaleOffsets StateTimeline,
        chordNoteOffsetsTimeline: ChordNoteOffsets StateTimeline,
        scaleOffsetsTimeline: ScaleOffsets StateTimeline,
        tempoTimeline: Tempo StateTimeline
    ) =
    member this.noteTimeline = noteTimeline
    member this.durationTimeline = durationTimeline
    member this.velocityTimeline = velocityTimeline
    member this.articulationOffsetTimeline = articulationOffsetTimeline
    member this.keyOffsetTimeline = keyOffsetTimeline
    member this.octaveOffsetTimeline = octaveOffsetTimeline
    member this.chordRootOffsetTimeline = chordRootOffsetTimeline
    member this.chordScaleOffsetsTimeline = chordScaleOffsetsTimeline
    member this.chordNoteOffsetsTimeline = chordNoteOffsetsTimeline
    member this.scaleOffsetsTimeline = scaleOffsetsTimeline
    member this.tempoTimeline = tempoTimeline

    static member internal ofArraysUnsafe
        (
            noteTimeline: EventState Timeline,
            durationTimeline: Duration StateTimeline,
            velocityTimeline: Velocity StateTimeline,
            articulationOffsetTimeline: ArticulationOffset StateTimeline,
            keyOffsetTimeline: KeyOffset StateTimeline,
            octaveOffsetTimeline: OctaveOffset StateTimeline,
            chordRootOffsetTimeline: ChordRootOffset StateTimeline,
            chordScaleOffsetsTimeline: ChordScaleOffsets StateTimeline,
            chordNoteOffsetsTimeline: ChordNoteOffsets StateTimeline,
            scaleOffsetsTimeline: ScaleOffsets StateTimeline,
            tempoTimeline: Tempo StateTimeline
        ) =
        EventStateTimelineMap(
            noteTimeline,
            durationTimeline,
            velocityTimeline,
            articulationOffsetTimeline,
            keyOffsetTimeline,
            octaveOffsetTimeline,
            chordRootOffsetTimeline,
            chordScaleOffsetsTimeline,
            chordNoteOffsetsTimeline,
            scaleOffsetsTimeline,
            tempoTimeline
        )

module EventStateTimelineMap =
    let private stateFromMap<'T when 'T: equality>
        (duration: Duration)
        (merger: EventStateMerger<'T>)
        (groupedTimeline: Map<string, Event TimelineItem seq>)
        : 'T StateTimeline =
        groupedTimeline
        |> Map.tryFind merger.Name
        |> Option.defaultValue Seq.empty
        |> Seq.map (fun struct (position, event) -> struct (position, merger.GetValue event))
        |> StateTimeline.ofSeq duration merger.Merge merger.DefaultValue

    let ofEventTimeline (duration: Duration) (sourceTimeline: Event Timeline) : EventStateTimelineMap =
        let groupedTimeline =
            sourceTimeline
            |> Timeline.trimDuration duration
            |> Seq.groupBy (fun struct (_, value) -> Event.getEventName value)
            |> Map.ofSeq

        let noteTimeline =
            groupedTimeline
            |> Map.tryFind (nameof NoteEvent)
            |> Option.defaultValue Seq.empty
            |> Seq.map (fun struct (position, value) -> struct (position, Event.Note.getValue value))
            |> Array.ofSeq
            |> Timeline.ofArrayUnsafe

        let durationTimeline = groupedTimeline |> stateFromMap duration Event.duration
        let velocityTimeline = groupedTimeline |> stateFromMap duration Event.velocity

        let articulationOffsetTimeline = groupedTimeline |> stateFromMap duration Event.articulationOffset

        let keyOffsetTimeline = groupedTimeline |> stateFromMap duration Event.keyOffset
        let octaveOffsetTimeline = groupedTimeline |> stateFromMap duration Event.octaveOffset

        let chordRootOffsetTimeline = groupedTimeline |> stateFromMap duration Event.chordRootOffset

        let chordScaleOffsetsTimeline = groupedTimeline |> stateFromMap duration Event.chordScaleOffsets

        let chordNoteOffsetsTimeline = groupedTimeline |> stateFromMap duration Event.chordNoteOffsets

        let scaleOffsetsTimeline = groupedTimeline |> stateFromMap duration Event.scaleOffsets
        let tempoTimeline = groupedTimeline |> stateFromMap duration Event.tempo

        EventStateTimelineMap.ofArraysUnsafe (
            noteTimeline,
            durationTimeline,
            velocityTimeline,
            articulationOffsetTimeline,
            keyOffsetTimeline,
            octaveOffsetTimeline,
            chordRootOffsetTimeline,
            chordScaleOffsetsTimeline,
            chordNoteOffsetsTimeline,
            scaleOffsetsTimeline,
            tempoTimeline
        )

    let empty (duration: Duration) : EventStateTimelineMap = ofEventTimeline duration Timeline.empty

    let private concatWithMerger<'T when 'T: equality>
        (duration: Duration)
        (func: EventStateTimelineMap -> 'T StateTimeline)
        (merger: EventStateMerger<'T>)
        (sourceTimeline: EventStateTimelineMap Timeline)
        : 'T StateTimeline =
        sourceTimeline
        |> Timeline.map func
        |> StateTimeline.concat duration merger.Merge merger.DefaultValue

    let concat (duration: Duration) (sourceTimeline: EventStateTimelineMap Timeline) : EventStateTimelineMap =
        let noteTimeline =
            sourceTimeline |> Timeline.map (fun timelineMap -> timelineMap.noteTimeline) |> Timeline.concat

        let durationTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.durationTimeline) Event.duration

        let velocityTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.velocityTimeline) Event.velocity

        let articulationOffsetTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.articulationOffsetTimeline) Event.articulationOffset

        let keyOffsetTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.keyOffsetTimeline) Event.keyOffset

        let octaveOffsetTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.octaveOffsetTimeline) Event.octaveOffset

        let chordRootOffsetTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.chordRootOffsetTimeline) Event.chordRootOffset

        let chordScaleOffsetsTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.chordScaleOffsetsTimeline) Event.chordScaleOffsets

        let chordNoteOffsetsTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.chordNoteOffsetsTimeline) Event.chordNoteOffsets

        let scaleOffsetsTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.scaleOffsetsTimeline) Event.scaleOffsets

        let tempoTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.tempoTimeline) Event.tempo

        EventStateTimelineMap.ofArraysUnsafe (
            noteTimeline,
            durationTimeline,
            velocityTimeline,
            articulationOffsetTimeline,
            keyOffsetTimeline,
            octaveOffsetTimeline,
            chordRootOffsetTimeline,
            chordScaleOffsetsTimeline,
            chordNoteOffsetsTimeline,
            scaleOffsetsTimeline,
            tempoTimeline
        )

    let private effectiveValue<'T when 'T: equality> (position: Position) (merger: EventStateMerger<'T>) (sourceTimeline: 'T StateTimeline) : 'T =
        sourceTimeline
        |> StateTimeline.tryFindEffectiveValue position
        |> Option.defaultValue merger.DefaultValue

    let effectiveState (position: Position) (sourceTimeline: EventStateTimelineMap) : EventState =
        {
            Duration = sourceTimeline.durationTimeline |> effectiveValue position Event.duration
            Velocity = sourceTimeline.velocityTimeline |> effectiveValue position Event.velocity
            ArticulationOffset = sourceTimeline.articulationOffsetTimeline |> effectiveValue position Event.articulationOffset
            KeyOffset = sourceTimeline.keyOffsetTimeline |> effectiveValue position Event.keyOffset
            OctaveOffset = sourceTimeline.octaveOffsetTimeline |> effectiveValue position Event.octaveOffset
            ChordRootOffset = sourceTimeline.chordRootOffsetTimeline |> effectiveValue position Event.chordRootOffset
            ChordScaleOffsets = sourceTimeline.chordScaleOffsetsTimeline |> effectiveValue position Event.chordScaleOffsets
            ChordNoteOffsets = sourceTimeline.chordNoteOffsetsTimeline |> effectiveValue position Event.chordNoteOffsets
            ScaleOffsets = sourceTimeline.scaleOffsetsTimeline |> effectiveValue position Event.scaleOffsets
            Tempo = sourceTimeline.tempoTimeline |> effectiveValue position Event.tempo
        }

    let ofMultiple (duration: Duration) (events: Event seq) : EventStateTimelineMap = events |> Timeline.ofMultiple |> ofEventTimeline duration

    let shiftState (state: EventState) (inputTimelineMap: EventStateTimelineMap) : EventStateTimelineMap =
        let durationTimeline =
            inputTimelineMap.durationTimeline
            |> StateTimeline.shiftValue state.Duration Event.duration.DefaultValue Event.duration.Merge

        let velocityTimeline =
            inputTimelineMap.velocityTimeline
            |> StateTimeline.shiftValue state.Velocity Event.velocity.DefaultValue Event.velocity.Merge

        let articulationOffsetTimeline =
            inputTimelineMap.articulationOffsetTimeline
            |> StateTimeline.shiftValue state.ArticulationOffset Event.articulationOffset.DefaultValue Event.articulationOffset.Merge

        let keyOffsetTimeline =
            inputTimelineMap.keyOffsetTimeline
            |> StateTimeline.shiftValue state.KeyOffset Event.keyOffset.DefaultValue Event.keyOffset.Merge

        let octaveOffsetTimeline =
            inputTimelineMap.octaveOffsetTimeline
            |> StateTimeline.shiftValue state.OctaveOffset Event.octaveOffset.DefaultValue Event.octaveOffset.Merge

        let chordRootOffsetTimeline =
            inputTimelineMap.chordRootOffsetTimeline
            |> StateTimeline.shiftValue state.ChordRootOffset Event.chordRootOffset.DefaultValue Event.chordRootOffset.Merge

        let chordScaleOffsetsTimeline =
            inputTimelineMap.chordScaleOffsetsTimeline
            |> StateTimeline.shiftValue state.ChordScaleOffsets Event.chordScaleOffsets.DefaultValue Event.chordScaleOffsets.Merge

        let chordNoteOffsetsTimeline =
            inputTimelineMap.chordNoteOffsetsTimeline
            |> StateTimeline.shiftValue state.ChordNoteOffsets Event.chordNoteOffsets.DefaultValue Event.chordNoteOffsets.Merge

        let scaleOffsetsTimeline =
            inputTimelineMap.scaleOffsetsTimeline
            |> StateTimeline.shiftValue state.ScaleOffsets Event.scaleOffsets.DefaultValue Event.scaleOffsets.Merge

        let tempoTimeline =
            inputTimelineMap.tempoTimeline
            |> StateTimeline.shiftValue state.Tempo Event.tempo.DefaultValue Event.tempo.Merge

        EventStateTimelineMap.ofArraysUnsafe (
            inputTimelineMap.noteTimeline,
            durationTimeline,
            velocityTimeline,
            articulationOffsetTimeline,
            keyOffsetTimeline,
            octaveOffsetTimeline,
            chordRootOffsetTimeline,
            chordScaleOffsetsTimeline,
            chordNoteOffsetsTimeline,
            scaleOffsetsTimeline,
            tempoTimeline
        )

    let concatCombined (duration: Duration) (sourceTimeline: EventStateTimelineMap WithEventState Timeline) : EventStateTimelineMap =
        sourceTimeline
        |> Timeline.map (fun struct (eventState, timeline) -> timeline |> shiftState eventState)
        |> concat duration
