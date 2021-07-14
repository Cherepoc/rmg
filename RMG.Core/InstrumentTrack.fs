namespace RMG.CoreF

open RMG.CoreF

type PitchInstrumentTrack =
    {
        Instrument: PitchInstrument
        Events: Event list
        MinOctaveOffset: OctaveOffset
        MaxOctaveOffset: OctaveOffset
    }

type PercussionInstrumentTrack = { Instrument: PercussionInstrument; Events: Event list }

type InstrumentTrack =
    | PitchInstrumentTrack of PitchInstrumentTrack
    | PercussionInstrumentTrack of PercussionInstrumentTrack
