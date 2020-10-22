namespace RMG.CoreF

type NoteOffset =
    { Duration: Duration
      KeyOffset: KeyOffset
      OctaveOffset: OctaveOffset
      ScaleOffset: ScaleOffset
      Volume: Volume }

module NoteOffset =
    let merge (x: NoteOffset) (y: NoteOffset): NoteOffset =
        { Duration = x.Duration * y.Duration
          KeyOffset = x.KeyOffset + y.KeyOffset
          OctaveOffset = x.OctaveOffset + y.OctaveOffset
          ScaleOffset = x.ScaleOffset + y.ScaleOffset
          Volume = x.Volume * y.Volume }
