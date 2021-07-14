namespace RMG.CoreF

type KeyOffset = int
type OctaveOffset = int
type VariationOffset = float
type ChordRootOffset = float
type ChordScaleOffsets = float list
type ChordNoteOffsets = float list
type Volume = float
type Velocity = float
type Scale = KeyOffset list

type Position = float
type Duration = float
type Tempo = float

type TrackNumber = int

module Constants =
    let notesInOctave = 12
    let zeroOctaveOffset = 5
