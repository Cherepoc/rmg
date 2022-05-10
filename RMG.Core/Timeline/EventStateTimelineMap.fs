namespace RMG.Core

open RMG.Core.Tuples

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
        
type EventStateTimelineMapInput =
    {
        NoteTimeline: EventState Timeline
        DurationTimeline: Duration list Timeline
        VelocityTimeline: Velocity list Timeline
        ArticulationOffsetTimeline: ArticulationOffset list Timeline
        KeyOffsetTimeline: KeyOffset list Timeline
        OctaveOffsetTimeline: OctaveOffset list Timeline
        ChordRootOffsetTimeline: ChordRootOffset list Timeline
        ChordScaleOffsetsTimeline: ChordScaleOffsets list Timeline
        ChordNoteOffsetsTimeline: ChordNoteOffsets list Timeline
        ScaleOffsetsTimeline: ScaleOffsets list Timeline
        TempoTimeline: Tempo list Timeline
    }

module EventStateTimelineMap =
    let fromInput (duration: Duration) (input: EventStateTimelineMapInput) : EventStateTimelineMap =
        EventStateTimelineMap.ofArraysUnsafe (
            input.NoteTimeline |> Timeline.trimDuration duration,
            input.DurationTimeline |> StateTimeline.ofSeq duration,
            input.VelocityTimeline |> StateTimeline.ofSeq duration,
            input.ArticulationOffsetTimeline |> StateTimeline.ofSeq duration,
            input.KeyOffsetTimeline |> StateTimeline.ofSeq duration,
            input.OctaveOffsetTimeline |> StateTimeline.ofSeq duration,
            input.ChordRootOffsetTimeline |> StateTimeline.ofSeq duration,
            input.ChordScaleOffsetsTimeline |> StateTimeline.ofSeq duration,
            input.ChordNoteOffsetsTimeline |> StateTimeline.ofSeq duration,
            input.ScaleOffsetsTimeline |> StateTimeline.ofSeq duration,
            input.TempoTimeline |> StateTimeline.ofSeq duration
        )
    
    let emptyInput : EventStateTimelineMapInput =
        {
            NoteTimeline = Timeline.empty
            DurationTimeline = Timeline.empty
            VelocityTimeline = Timeline.empty
            ArticulationOffsetTimeline = Timeline.empty
            KeyOffsetTimeline = Timeline.empty
            OctaveOffsetTimeline = Timeline.empty
            ChordRootOffsetTimeline = Timeline.empty
            ChordScaleOffsetsTimeline = Timeline.empty
            ChordNoteOffsetsTimeline = Timeline.empty
            ScaleOffsetsTimeline = Timeline.empty
            TempoTimeline = Timeline.empty
        }
        
    let empty (duration: Duration) : EventStateTimelineMap = fromInput duration emptyInput

    let private concatWithMerger<'T when 'T: equality>
        (duration: Duration)
        (func: EventStateTimelineMap -> 'T StateTimeline)
        (sourceTimeline: EventStateTimelineMap Timeline)
        : 'T StateTimeline
        =
        sourceTimeline
        |> Timeline.map func
        |> StateTimeline.concat duration

    let concat (duration: Duration) (sourceTimeline: EventStateTimelineMap Timeline) : EventStateTimelineMap =
        let noteTimeline =
            sourceTimeline
            |> Timeline.map (fun timelineMap -> timelineMap.NoteTimeline)
            |> Timeline.concat

        let durationTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.DurationTimeline)

        let velocityTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.VelocityTimeline)

        let articulationOffsetTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.ArticulationOffsetTimeline)

        let keyOffsetTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.KeyOffsetTimeline)

        let octaveOffsetTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.OctaveOffsetTimeline)

        let chordRootOffsetTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.ChordRootOffsetTimeline)

        let chordScaleOffsetsTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.ChordScaleOffsetsTimeline)

        let chordNoteOffsetsTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.ChordNoteOffsetsTimeline)

        let scaleOffsetsTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.ScaleOffsetsTimeline)

        let tempoTimeline =
            sourceTimeline
            |> concatWithMerger duration (fun timelineMap -> timelineMap.TempoTimeline)

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

    let private effectiveValue<'T when 'T: equality> (position: Position) (sourceTimeline: 'T StateTimeline) : 'T list =
        sourceTimeline
        |> StateTimeline.tryFindEffectiveValue position
        |> Option.defaultValue List.empty

    let effectiveState (position: Position) (sourceTimeline: EventStateTimelineMap) : EventState =
        {
            Duration = sourceTimeline.DurationTimeline |> effectiveValue position
            Velocity = sourceTimeline.VelocityTimeline |> effectiveValue position
            ArticulationOffset =
                sourceTimeline.ArticulationOffsetTimeline
                |> effectiveValue position
            KeyOffset = sourceTimeline.KeyOffsetTimeline |> effectiveValue position
            OctaveOffset =
                sourceTimeline.OctaveOffsetTimeline
                |> effectiveValue position
            ChordRootOffset =
                sourceTimeline.ChordRootOffsetTimeline
                |> effectiveValue position
            ChordScaleOffsets =
                sourceTimeline.ChordScaleOffsetsTimeline
                |> effectiveValue position
            ChordNoteOffsets =
                sourceTimeline.ChordNoteOffsetsTimeline
                |> effectiveValue position
            ScaleOffsets =
                sourceTimeline.ScaleOffsetsTimeline
                |> effectiveValue position
            Tempo = sourceTimeline.TempoTimeline |> effectiveValue position
        }

    let shiftState (state: EventState) (inputTimelineMap: EventStateTimelineMap) : EventStateTimelineMap =
        let durationTimeline =
            inputTimelineMap.DurationTimeline
            |> StateTimeline.shiftValue state.Duration 

        let velocityTimeline =
            inputTimelineMap.VelocityTimeline
            |> StateTimeline.shiftValue state.Velocity

        let articulationOffsetTimeline =
            inputTimelineMap.ArticulationOffsetTimeline
            |> StateTimeline.shiftValue state.ArticulationOffset

        let keyOffsetTimeline =
            inputTimelineMap.KeyOffsetTimeline
            |> StateTimeline.shiftValue state.KeyOffset

        let octaveOffsetTimeline =
            inputTimelineMap.OctaveOffsetTimeline
            |> StateTimeline.shiftValue state.OctaveOffset

        let chordRootOffsetTimeline =
            inputTimelineMap.ChordRootOffsetTimeline
            |> StateTimeline.shiftValue state.ChordRootOffset

        let chordScaleOffsetsTimeline =
            inputTimelineMap.ChordScaleOffsetsTimeline
            |> StateTimeline.shiftValue state.ChordScaleOffsets

        let chordNoteOffsetsTimeline =
            inputTimelineMap.ChordNoteOffsetsTimeline
            |> StateTimeline.shiftValue state.ChordNoteOffsets

        let scaleOffsetsTimeline =
            inputTimelineMap.ScaleOffsetsTimeline
            |> StateTimeline.shiftValue state.ScaleOffsets

        let tempoTimeline =
            inputTimelineMap.TempoTimeline
            |> StateTimeline.shiftValue state.Tempo

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
