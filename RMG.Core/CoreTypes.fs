namespace RMG.CoreF

type KeyOffset = int
type OctaveOffset = int
type ScaleOffset = float
type ChordRootOffset = float
type ChordScaleOffsets = list<float>
type ChordNoteOffsets = list<float>
type Volume = float

type Position = float
type Duration = float
type Tempo = float

type TrackNumber = int

module Constants =
    let notesInOctave = 12
    let zeroOctaveOffset = 5
