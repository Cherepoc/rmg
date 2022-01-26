namespace RMG.Core

type InstrumentCode = byte
type ArticulationCode = byte

type PitchInstrument = { Code: InstrumentCode }

type PercussionInstrument = { ArticulationCodes: list<ArticulationCode> }
