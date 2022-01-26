namespace RMG.Core

type EventState =
    {
        Duration: Duration
        Velocity: Velocity
        ArticulationOffset: ArticulationOffset
        KeyOffset: KeyOffset
        OctaveOffset: OctaveOffset
        ChordRootOffset: ChordRootOffset
        ChordScaleOffsets: ChordScaleOffsets
        ChordNoteOffsets: ChordNoteOffsets
        ScaleOffsets: ScaleOffsets
        Tempo: Tempo
    }

type Event =
    | DurationEvent of Duration
    | VelocityEvent of Velocity
    | ArticulationOffsetEvent of ArticulationOffset
    | KeyOffsetEvent of KeyOffset
    | OctaveOffsetEvent of OctaveOffset
    | ChordRootOffsetEvent of ChordRootOffset
    | ChordScaleOffsetsEvent of ChordScaleOffsets
    | ChordNoteOffsetsEvent of ChordNoteOffsets
    | ScaleOffsetsEvent of ScaleOffsets
    | TempoEvent of Tempo

type WithEventState<'T> = (struct (EventState * 'T))

type EventStateMerger<'T> =
    {
        Name: string
        GetValue: Event -> 'T
        Merge: 'T -> 'T -> 'T
        DefaultValue: 'T
    }

module Event =
    let inline private mergeMul x y = x * y
    let inline private mergeAdd x y = x + y
    let inline private mergeAppendList x y = x |> List.append y

    let getEventName (event: Event) : string =
        match event with
        | DurationEvent _ -> nameof DurationEvent
        | VelocityEvent _ -> nameof VelocityEvent
        | ArticulationOffsetEvent _ -> nameof ArticulationOffsetEvent
        | KeyOffsetEvent _ -> nameof KeyOffsetEvent
        | OctaveOffsetEvent _ -> nameof OctaveOffsetEvent
        | ChordRootOffsetEvent _ -> nameof ChordRootOffsetEvent
        | ChordScaleOffsetsEvent _ -> nameof ChordScaleOffsetsEvent
        | ChordNoteOffsetsEvent _ -> nameof ChordNoteOffsetsEvent
        | ScaleOffsetsEvent _ -> nameof ScaleOffsetsEvent
        | TempoEvent _ -> nameof TempoEvent

    let invalidEventType (event: Event) (expectedType: string) =
        invalidArg (nameof event) $"Invalid event type. Expected event of type {expectedType}, got {event}"

    let duration =
        {
            Name = nameof DurationEvent
            GetValue =
                fun event ->
                    match event with
                    | DurationEvent value -> value
                    | _ -> invalidEventType event (nameof DurationEvent)
            Merge = mergeMul
            DefaultValue = 1.0
        }

    let velocity =
        {
            Name = nameof VelocityEvent
            GetValue =
                fun event ->
                    match event with
                    | VelocityEvent value -> value
                    | _ -> invalidEventType event (nameof VelocityEvent)
            Merge = mergeMul
            DefaultValue = 1.0
        }

    let articulationOffset =
        {
            Name = nameof ArticulationOffsetEvent
            GetValue =
                fun event ->
                    match event with
                    | ArticulationOffsetEvent value -> value
                    | _ -> invalidEventType event (nameof ArticulationOffsetEvent)
            Merge = mergeAdd
            DefaultValue = 0.0
        }

    let keyOffset =
        {
            Name = nameof KeyOffsetEvent
            GetValue =
                fun event ->
                    match event with
                    | KeyOffsetEvent value -> value
                    | _ -> invalidEventType event (nameof KeyOffsetEvent)
            Merge = mergeAdd
            DefaultValue = 0
        }

    let octaveOffset =
        {
            Name = nameof OctaveOffsetEvent
            GetValue =
                fun event ->
                    match event with
                    | OctaveOffsetEvent value -> value
                    | _ -> invalidEventType event (nameof OctaveOffsetEvent)
            Merge = mergeAdd
            DefaultValue = 0
        }

    let chordRootOffset =
        {
            Name = nameof ChordRootOffsetEvent
            GetValue =
                fun event ->
                    match event with
                    | ChordRootOffsetEvent value -> value
                    | _ -> invalidEventType event (nameof ChordRootOffsetEvent)
            Merge = mergeAdd
            DefaultValue = 0.0
        }

    let chordScaleOffsets =
        {
            Name = nameof ChordScaleOffsetsEvent
            GetValue =
                fun event ->
                    match event with
                    | ChordScaleOffsetsEvent value -> value
                    | _ -> invalidEventType event (nameof ChordScaleOffsetsEvent)
            Merge = mergeAppendList
            DefaultValue = List.empty
        }

    let chordNoteOffsets =
        {
            Name = nameof ChordNoteOffsetsEvent
            GetValue =
                fun event ->
                    match event with
                    | ChordNoteOffsetsEvent value -> value
                    | _ -> invalidEventType event (nameof ChordNoteOffsetsEvent)
            Merge = mergeAppendList
            DefaultValue = List.empty
        }

    let scaleOffsets =
        {
            Name = nameof ScaleOffsetsEvent
            GetValue =
                fun event ->
                    match event with
                    | ScaleOffsetsEvent value -> value
                    | _ -> invalidEventType event (nameof ScaleOffsetsEvent)
            Merge = mergeAppendList
            DefaultValue = List.empty
        }

    let tempo =
        {
            Name = nameof TempoEvent
            GetValue =
                fun event ->
                    match event with
                    | TempoEvent value -> value
                    | _ -> invalidEventType event (nameof TempoEvent)
            Merge = mergeMul
            DefaultValue = 1.0
        }

module EventState =
    let empty : EventState =
        {
            Duration = Event.duration.DefaultValue
            Velocity = Event.velocity.DefaultValue
            ArticulationOffset = Event.articulationOffset.DefaultValue
            KeyOffset = Event.keyOffset.DefaultValue
            OctaveOffset = Event.octaveOffset.DefaultValue
            ChordRootOffset = Event.chordRootOffset.DefaultValue
            ChordScaleOffsets = Event.chordScaleOffsets.DefaultValue
            ChordNoteOffsets = Event.chordNoteOffsets.DefaultValue
            ScaleOffsets = Event.scaleOffsets.DefaultValue
            Tempo = Event.tempo.DefaultValue
        }

    let merge (eventState1: EventState) (eventState2: EventState) : EventState =
        {
            Duration = Event.duration.Merge eventState1.Duration eventState2.Duration
            Velocity = Event.velocity.Merge eventState1.Velocity eventState2.Velocity
            ArticulationOffset = Event.articulationOffset.Merge eventState1.ArticulationOffset eventState2.ArticulationOffset
            KeyOffset = Event.keyOffset.Merge eventState1.KeyOffset eventState2.KeyOffset
            OctaveOffset = Event.octaveOffset.Merge eventState1.OctaveOffset eventState2.OctaveOffset
            ChordRootOffset = Event.chordRootOffset.Merge eventState1.ChordRootOffset eventState2.ChordRootOffset
            ChordScaleOffsets = Event.chordScaleOffsets.Merge eventState1.ChordScaleOffsets eventState2.ChordScaleOffsets
            ChordNoteOffsets = Event.chordNoteOffsets.Merge eventState1.ChordNoteOffsets eventState2.ChordNoteOffsets
            ScaleOffsets = Event.scaleOffsets.Merge eventState1.ScaleOffsets eventState2.ScaleOffsets
            Tempo = Event.tempo.Merge eventState1.Tempo eventState2.Tempo
        }

    let private eventValuesFromMap<'T> (merger: EventStateMerger<'T>) (map: Map<string, Event seq>) : 'T =
        map
        |> Map.tryFind merger.Name
        |> Option.map (fun events -> events |> Seq.map merger.GetValue |> Seq.reduce merger.Merge)
        |> Option.defaultValue merger.DefaultValue

    let ofSeq (items: Event seq) : EventState =
        let itemMap = items |> Seq.groupBy Event.getEventName |> Map.ofSeq

        {
            Duration = itemMap |> eventValuesFromMap Event.duration
            Velocity = itemMap |> eventValuesFromMap Event.velocity
            ArticulationOffset = itemMap |> eventValuesFromMap Event.articulationOffset
            KeyOffset = itemMap |> eventValuesFromMap Event.keyOffset
            OctaveOffset = itemMap |> eventValuesFromMap Event.octaveOffset
            ChordRootOffset = itemMap |> eventValuesFromMap Event.chordRootOffset
            ChordScaleOffsets = itemMap |> eventValuesFromMap Event.chordScaleOffsets
            ChordNoteOffsets = itemMap |> eventValuesFromMap Event.chordNoteOffsets
            ScaleOffsets = itemMap |> eventValuesFromMap Event.scaleOffsets
            Tempo = itemMap |> eventValuesFromMap Event.tempo
        }
