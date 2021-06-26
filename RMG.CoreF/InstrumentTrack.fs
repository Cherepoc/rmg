namespace RMG.CoreF

open RMG.CoreF

[<NoEquality>]
[<NoComparison>]
type PitchInstrumentTrack =
    { Instrument: PitchInstrument
      NoteOffset: NoteOffset
      MinOctaveOffset: OctaveOffset
      MaxOctaveOffset: OctaveOffset }

[<NoEquality>]
[<NoComparison>]
type PercussionInstrumentTrack =
    { Instruments: list<PercussionInstrument>
      NoteOffset: NoteOffset }

[<NoEquality>]
[<NoComparison>]
type InstrumentTrack =
    | PitchInstrumentTrack of PitchInstrumentTrack
    | PercussionInstrumentTrack of PercussionInstrumentTrack
