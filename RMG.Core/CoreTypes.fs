namespace RMG.Core

type KeyOffset = int
type OctaveOffset = int
type ArticulationOffset = float
type ChordRootOffset = float
type ChordScaleOffsets = float
type ChordNoteOffsets = float
type Volume = float
type Velocity = float
type ScaleOffsets = KeyOffset

type Position = float
type Duration = float
type Tempo = float

type TrackNumber = int

module Constants =
    let notesInOctave = 12
    let zeroOctaveOffset = 5
