namespace RMG.CoreF

type Event =
    | NoteEvent of Event list
    | DurationEvent of Duration
    | VelocityEvent of Velocity
    | ArticulationOffsetEvent of VariationOffset
    | KeyOffsetEvent of KeyOffset
    | OctaveOffsetEvent of OctaveOffset
    | ChordRootOffsetEvent of ChordRootOffset
    | ChordScaleOffsetsEvent of ChordScaleOffsets
    | ChordNoteOffsetsEvent of ChordNoteOffsets
    | ScaleOffsetsEvent of Scale
    | TempoEvent of Tempo

type WithEvents<'T> = Event list * 'T

type EventStateMerger<'T> =
    {
        FromValue: 'T -> Event
        Check: Event -> bool
        TryGetValue: Event -> 'T option
        TryMergeValues: 'T seq -> 'T option
        TryMerge: Event seq -> Event option
        MergeToValues: Event seq -> 'T
        TryMergeToValues: Event seq -> 'T option
        DefaultValue: 'T
        Default: Event
    }

type CommonEventStateMerger =
    {
        Check: Event -> bool
        TryMerge: Event seq -> Event option
        Default: Event
    }

module Event =
    type private EventStateMergerInput<'T> =
        {
            FromValue: 'T -> Event
            TryGetValue: Event -> 'T option
            Merger: 'T -> 'T -> 'T
            DefaultValue: 'T
        }

    let inline private mergeMul x y = x * y
    let inline private mergeAdd x y = x + y
    let inline private mergeAppendList x y = x |> List.append y

    let private makeMergeValues<'T> (merger: 'T -> 'T -> 'T) : 'T seq -> 'T option =
        fun values ->
            if values |> Seq.isEmpty then
                None
            else
                values |> (Seq.reduce merger) |> Some

    let private createCommonMerger<'T> (merger: EventStateMerger<'T>) : CommonEventStateMerger =
        {
            Check = merger.Check
            TryMerge =
                fun events ->
                    events
                    |> Seq.choose merger.TryGetValue
                    |> merger.TryMergeValues
                    |> Option.map merger.FromValue
            Default = merger.FromValue merger.DefaultValue
        }

    let private createEventStateMerger<'T> (input: EventStateMergerInput<'T>) : EventStateMerger<'T> =
        let tryMergeValues = makeMergeValues input.Merger

        {
            FromValue = input.FromValue
            TryGetValue = input.TryGetValue
            Check = fun event -> input.TryGetValue event |> Option.isSome
            TryMergeValues = tryMergeValues
            TryMerge =
                fun events ->
                    events
                    |> Seq.choose input.TryGetValue
                    |> tryMergeValues
                    |> Option.map input.FromValue
            MergeToValues =
                fun events ->
                    events
                    |> Seq.choose input.TryGetValue
                    |> Seq.fold input.Merger input.DefaultValue
            TryMergeToValues = fun events -> events |> Seq.choose input.TryGetValue |> tryMergeValues
            DefaultValue = input.DefaultValue
            Default = input.FromValue input.DefaultValue
        }

    module Note =
        let check (event: Event) : bool =
            match event with
            | NoteEvent _ -> true
            | _ -> false

        let getValue (event: Event) : Event list option =
            match event with
            | NoteEvent value -> Some value
            | _ -> None

    let duration =
        {
            FromValue = DurationEvent
            TryGetValue =
                fun event ->
                    match event with
                    | DurationEvent value -> Some value
                    | _ -> None
            Merger = mergeMul
            DefaultValue = 1.0
        }
        |> createEventStateMerger

    let velocity =
        {
            FromValue = VelocityEvent
            TryGetValue =
                fun event ->
                    match event with
                    | VelocityEvent value -> Some value
                    | _ -> None
            Merger = mergeMul
            DefaultValue = 1.0
        }
        |> createEventStateMerger

    let articulationOffset =
        {
            FromValue = ArticulationOffsetEvent
            TryGetValue =
                fun event ->
                    match event with
                    | ArticulationOffsetEvent value -> Some value
                    | _ -> None
            Merger = mergeAdd
            DefaultValue = 0.0
        }
        |> createEventStateMerger

    let keyOffset =
        {
            FromValue = KeyOffsetEvent
            TryGetValue =
                fun event ->
                    match event with
                    | KeyOffsetEvent value -> Some value
                    | _ -> None
            Merger = mergeAdd
            DefaultValue = 0
        }
        |> createEventStateMerger

    let octaveOffset =
        {
            FromValue = OctaveOffsetEvent
            TryGetValue =
                fun event ->
                    match event with
                    | OctaveOffsetEvent value -> Some value
                    | _ -> None
            Merger = mergeAdd
            DefaultValue = 0
        }
        |> createEventStateMerger

    let chordRootOffset =
        {
            FromValue = ChordRootOffsetEvent
            TryGetValue =
                fun event ->
                    match event with
                    | ChordRootOffsetEvent value -> Some value
                    | _ -> None
            Merger = mergeAdd
            DefaultValue = 0.0
        }
        |> createEventStateMerger

    let chordScaleOffsets =
        {
            FromValue = ChordScaleOffsetsEvent
            TryGetValue =
                fun event ->
                    match event with
                    | ChordScaleOffsetsEvent value -> Some value
                    | _ -> None
            Merger = mergeAppendList
            DefaultValue = List.empty
        }
        |> createEventStateMerger

    let chordNoteOffsets =
        {
            FromValue = ChordNoteOffsetsEvent
            TryGetValue =
                fun event ->
                    match event with
                    | ChordNoteOffsetsEvent value -> Some value
                    | _ -> None
            Merger = mergeAppendList
            DefaultValue = List.empty
        }
        |> createEventStateMerger

    let scaleOffsets =
        {
            FromValue = ScaleOffsetsEvent
            TryGetValue =
                fun event ->
                    match event with
                    | ScaleOffsetsEvent value -> Some value
                    | _ -> None
            Merger = mergeAppendList
            DefaultValue = List.empty
        }
        |> createEventStateMerger

    let tempo =
        {
            FromValue = TempoEvent
            TryGetValue =
                fun event ->
                    match event with
                    | TempoEvent value -> Some value
                    | _ -> None
            Merger = mergeMul
            DefaultValue = 1.0
        }
        |> createEventStateMerger

    let mergers : CommonEventStateMerger list =
        [
            createCommonMerger duration
            createCommonMerger velocity
            createCommonMerger articulationOffset
            createCommonMerger keyOffset
            createCommonMerger octaveOffset
            createCommonMerger chordRootOffset
            createCommonMerger chordNoteOffsets
            createCommonMerger chordScaleOffsets
            createCommonMerger scaleOffsets
            createCommonMerger tempo
        ]

    let isState (event: Event) : bool = Note.check event |> not

    let mergeAll (events: Event seq) : Event seq =
        events
        |> Seq.groupBy isState
        |> Seq.collect
            (fun (isState, events) ->
                if isState then
                    let array = events |> Array.ofSeq
                    mergers |> Seq.choose (fun merger -> array :> Event seq |> merger.TryMerge)
                else
                    events)
