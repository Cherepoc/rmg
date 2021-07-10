namespace RMG.CoreF

type NoteOffset =
    {
        Duration: Duration
        KeyOffset: KeyOffset
        OctaveOffset: OctaveOffset
        ChordRootOffset: ChordRootOffset
        ChordScaleOffsets: ChordScaleOffsets
        ChordNoteOffsets: ChordNoteOffsets
        Velocity: Volume
    }

module NoteOffset =
    let empty : NoteOffset =
        {
            Duration = 1.0
            KeyOffset = 0
            OctaveOffset = 0
            ChordRootOffset = 0.0
            ChordScaleOffsets = List.empty
            ChordNoteOffsets = List.empty
            Velocity = 1.0
        }

    let merge (x: NoteOffset, y: NoteOffset) : NoteOffset =
        {
            Duration = x.Duration * y.Duration
            KeyOffset = x.KeyOffset + y.KeyOffset
            OctaveOffset = x.OctaveOffset + y.OctaveOffset
            ChordRootOffset = x.ChordRootOffset + y.ChordRootOffset
            ChordScaleOffsets = x.ChordScaleOffsets |> List.append y.ChordScaleOffsets
            ChordNoteOffsets = x.ChordNoteOffsets |> List.append y.ChordNoteOffsets
            Velocity = x.Velocity * y.Velocity
        }
