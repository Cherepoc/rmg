namespace RMG.CoreF

type NoteOffset =
    { Duration: Duration
      KeyOffset: KeyOffset
      OctaveOffset: OctaveOffset
      ScaleOffset: ScaleOffset
      Velocity: Volume }

module NoteOffset =
    let empty: NoteOffset =
        { Duration = 1.0
          KeyOffset = 0
          OctaveOffset = 0
          ScaleOffset = 0.0
          Velocity = 1.0 }

    let merge (x:NoteOffset, y: NoteOffset): NoteOffset =
        { Duration = x.Duration * y.Duration
          KeyOffset = x.KeyOffset + y.KeyOffset
          OctaveOffset = x.OctaveOffset + y.OctaveOffset
          ScaleOffset = x.ScaleOffset + y.ScaleOffset
          Velocity = x.Velocity * y.Velocity }
