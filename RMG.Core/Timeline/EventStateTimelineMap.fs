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
    member this.NoteTimeline = noteTimeline
    member this.DurationTimeline = durationTimeline
    member this.VelocityTimeline = velocityTimeline
    member this.ArticulationOffsetTimeline = articulationOffsetTimeline
    member this.KeyOffsetTimeline = keyOffsetTimeline
    member this.OctaveOffsetTimeline = octaveOffsetTimeline
    member this.ChordRootOffsetTimeline = chordRootOffsetTimeline
    member this.ChordScaleOffsetsTimeline = chordScaleOffsetsTimeline
    member this.ChordNoteOffsetsTimeline = chordNoteOffsetsTimeline
    member this.ScaleOffsetsTimeline = scaleOffsetsTimeline
    member this.TempoTimeline = tempoTimeline

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
        : 'T StateTimeline
        =
        groupedTimeline
        |> Map.tryFind merger.Name
        |> Option.defaultValue Seq.empty
        |> Seq.map (fun (struct (position, event)) -> struct (position, merger.GetValue event))
        |> StateTimeline.ofSeq duration merger.Merge merger.DefaultValue

    let ofTimelines (duration: Duration) (sourceNoteTimeline: EventState Timeline) (sourceStateTimeline: Event Timeline) : EventStateTimelineMap =
        let groupedStateTimeline =
            sourceStateTimeline
            |> Timeline.trimDuration duration
            |> Seq.groupBy (fun (struct (_, value)) -> Event.getEventName value)
            |> Map.ofSeq

        let noteTimeline = sourceNoteTimeline |> Timeline.trimDuration duration

        let durationTimeline = groupedStateTimeline |> stateFromMap duration Event.duration
        let velocityTimeline = groupedStateTimeline |> stateFromMap duration Event.velocity

        let articulationOffsetTimeline =
            groupedStateTimeline |> stateFromMap duration Event.articulationOffset

        let keyOffsetTimeline =
            groupedStateTimeline |> stateFromMap duration Event.keyOffset

        let octaveOffsetTimeline =
            groupedStateTimeline |> stateFromMap duration Event.octaveOffset

        let chordRootOffsetTimeline =
            groupedStateTimeline |> stateFromMap duration Event.chordRootOffset

        let chordScaleOffsetsTimeline =
            groupedStateTimeline |> stateFromMap duration Event.chordScaleOffsets

        let chordNoteOffsetsTimeline =
            groupedStateTimeline |> stateFromMap duration Event.chordNoteOffsets

        let scaleOffsetsTimeline =
            groupedStateTimeline |> stateFromMap duration Event.scaleOffsets

        let tempoTimeline = groupedStateTimeline |> stateFromMap duration Event.tempo

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

    let empty (duration: Duration) : EventStateTimelineMap = ofTimelines duration Timeline.empty Timeline.empty

    let private concatWithMerger<'T when 'T: equality>
        (duration: Duration)
        (func: EventStateTimelineMap -> 'T StateTimeline)
        (merger: EventStateMerger<'T>)
        (sourceTimeline: EventStateTimelineMap Timeline)
        : 'T StateTimeline
        =
        sourceTimeline
        |> Timeline.map func
        |> StateTimeline.concat duration merger.Merge merger.DefaultValue

    let concat (duration: Duration) (sourceTimeline: EventStateTimelineMap Timeline) : EventStateTimelineMap =
        let noteTimeline =
            sourceTimeline
            |> Timeline.map (fun timelineMap -> timelineMap.NoteTimeline)
            |> Timeline.concat

        let durationTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.DurationTimeline) Event.duration

        let velocityTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.VelocityTimeline) Event.velocity

        let articulationOffsetTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.ArticulationOffsetTimeline) Event.articulationOffset

        let keyOffsetTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.KeyOffsetTimeline) Event.keyOffset

        let octaveOffsetTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.OctaveOffsetTimeline) Event.octaveOffset

        let chordRootOffsetTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.ChordRootOffsetTimeline) Event.chordRootOffset

        let chordScaleOffsetsTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.ChordScaleOffsetsTimeline) Event.chordScaleOffsets

        let chordNoteOffsetsTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.ChordNoteOffsetsTimeline) Event.chordNoteOffsets

        let scaleOffsetsTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.ScaleOffsetsTimeline) Event.scaleOffsets

        let tempoTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.TempoTimeline) Event.tempo

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
            Duration = sourceTimeline.DurationTimeline |> effectiveValue position Event.duration
            Velocity = sourceTimeline.VelocityTimeline |> effectiveValue position Event.velocity
            ArticulationOffset =
                sourceTimeline.ArticulationOffsetTimeline
                |> effectiveValue position Event.articulationOffset
            KeyOffset = sourceTimeline.KeyOffsetTimeline |> effectiveValue position Event.keyOffset
            OctaveOffset =
                sourceTimeline.OctaveOffsetTimeline
                |> effectiveValue position Event.octaveOffset
            ChordRootOffset =
                sourceTimeline.ChordRootOffsetTimeline
                |> effectiveValue position Event.chordRootOffset
            ChordScaleOffsets =
                sourceTimeline.ChordScaleOffsetsTimeline
                |> effectiveValue position Event.chordScaleOffsets
            ChordNoteOffsets =
                sourceTimeline.ChordNoteOffsetsTimeline
                |> effectiveValue position Event.chordNoteOffsets
            ScaleOffsets =
                sourceTimeline.ScaleOffsetsTimeline
                |> effectiveValue position Event.scaleOffsets
            Tempo = sourceTimeline.TempoTimeline |> effectiveValue position Event.tempo
        }

    let ofMultipleState (duration: Duration) (events: Event seq) : EventStateTimelineMap =
        ofTimelines duration Timeline.empty (events |> Timeline.ofMultiple)

    let shiftState (state: EventState) (inputTimelineMap: EventStateTimelineMap) : EventStateTimelineMap =
        let durationTimeline =
            inputTimelineMap.DurationTimeline
            |> StateTimeline.shiftValue state.Duration Event.duration.DefaultValue Event.duration.Merge

        let velocityTimeline =
            inputTimelineMap.VelocityTimeline
            |> StateTimeline.shiftValue state.Velocity Event.velocity.DefaultValue Event.velocity.Merge

        let articulationOffsetTimeline =
            inputTimelineMap.ArticulationOffsetTimeline
            |> StateTimeline.shiftValue state.ArticulationOffset Event.articulationOffset.DefaultValue Event.articulationOffset.Merge

        let keyOffsetTimeline =
            inputTimelineMap.KeyOffsetTimeline
            |> StateTimeline.shiftValue state.KeyOffset Event.keyOffset.DefaultValue Event.keyOffset.Merge

        let octaveOffsetTimeline =
            inputTimelineMap.OctaveOffsetTimeline
            |> StateTimeline.shiftValue state.OctaveOffset Event.octaveOffset.DefaultValue Event.octaveOffset.Merge

        let chordRootOffsetTimeline =
            inputTimelineMap.ChordRootOffsetTimeline
            |> StateTimeline.shiftValue state.ChordRootOffset Event.chordRootOffset.DefaultValue Event.chordRootOffset.Merge

        let chordScaleOffsetsTimeline =
            inputTimelineMap.ChordScaleOffsetsTimeline
            |> StateTimeline.shiftValue state.ChordScaleOffsets Event.chordScaleOffsets.DefaultValue Event.chordScaleOffsets.Merge

        let chordNoteOffsetsTimeline =
            inputTimelineMap.ChordNoteOffsetsTimeline
            |> StateTimeline.shiftValue state.ChordNoteOffsets Event.chordNoteOffsets.DefaultValue Event.chordNoteOffsets.Merge

        let scaleOffsetsTimeline =
            inputTimelineMap.ScaleOffsetsTimeline
            |> StateTimeline.shiftValue state.ScaleOffsets Event.scaleOffsets.DefaultValue Event.scaleOffsets.Merge

        let tempoTimeline =
            inputTimelineMap.TempoTimeline
            |> StateTimeline.shiftValue state.Tempo Event.tempo.DefaultValue Event.tempo.Merge

        EventStateTimelineMap.ofArraysUnsafe (
            inputTimelineMap.NoteTimeline,
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
        |> Timeline.map (fun (struct (eventState, timeline)) -> timeline |> shiftState eventState)
        |> concat duration
