namespace RMG.Core

type EventState =
    {
        Duration: Duration list
        Velocity: Velocity list
        ArticulationOffset: ArticulationOffset list
        KeyOffset: KeyOffset list
        OctaveOffset: OctaveOffset list
        ChordRootOffset: ChordRootOffset list
        ChordScaleOffsets: ChordScaleOffsets list
        ChordNoteOffsets: ChordNoteOffsets list
        ScaleOffsets: ScaleOffsets list
        Tempo: Tempo list
    }

type WithEventState<'T> = (struct (EventState * 'T))

module EventState =
    let empty : EventState =
        {
            Duration = List.empty
            Velocity = List.empty
            ArticulationOffset = List.empty
            KeyOffset = List.empty
            OctaveOffset = List.empty
            ChordRootOffset = List.empty
            ChordScaleOffsets = List.empty
            ChordNoteOffsets = List.empty
            ScaleOffsets = List.empty
            Tempo = List.empty
        }

    let merge (eventState1: EventState) (eventState2: EventState) : EventState =
        {
            Duration = List.append eventState1.Duration eventState2.Duration
            Velocity = List.append eventState1.Velocity eventState2.Velocity
            ArticulationOffset = List.append eventState1.ArticulationOffset eventState2.ArticulationOffset
            KeyOffset = List.append eventState1.KeyOffset eventState2.KeyOffset
            OctaveOffset = List.append eventState1.OctaveOffset eventState2.OctaveOffset
            ChordRootOffset = List.append eventState1.ChordRootOffset eventState2.ChordRootOffset
            ChordScaleOffsets = List.append eventState1.ChordScaleOffsets eventState2.ChordScaleOffsets
            ChordNoteOffsets = List.append eventState1.ChordNoteOffsets eventState2.ChordNoteOffsets
            ScaleOffsets = List.append eventState1.ScaleOffsets eventState2.ScaleOffsets
            Tempo = List.append eventState1.Tempo eventState2.Tempo
        }
