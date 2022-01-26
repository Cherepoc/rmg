namespace RMG.Core

type PitchInstrumentTrack =
    {
        Instrument: PitchInstrument
        EventState: EventState
        MinOctaveOffset: OctaveOffset
        MaxOctaveOffset: OctaveOffset
    }

type PercussionInstrumentTrack = { Instrument: PercussionInstrument; EventState: EventState }

type InstrumentTrack =
    | PitchInstrumentTrack of PitchInstrumentTrack
    | PercussionInstrumentTrack of PercussionInstrumentTrack
