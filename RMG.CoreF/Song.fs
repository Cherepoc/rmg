namespace RMG.CoreF

open RMG.CoreF

type Tempo = double
type InstrumentCode = byte

type Instrument = { Code: InstrumentCode }

[<NoEquality>]
[<NoComparison>]
type Scale = { KeyOffsets: list<KeyOffset> }

type TimeSignature = { Numerator: uint; Denominator: uint }

[<NoEquality>]
[<NoComparison>]
type Track =
    { Instrument: Instrument
      NoteOffset: NoteOffset
      MinOctaveOffset: OctaveOffset
      MaxOctaveOffset: OctaveOffset }

[<NoEquality>]
[<NoComparison>]
type Song =
    { Duration: Duration
      Tracks: Map<TrackNumber, Track>
      ScaleTimeline: EventTimeline<Scale>
      TempoTimeline: StateTimeline<Tempo>
      NoteOffsetTimelineMap: NoteOffsetStateTimelineMap
      TrackNoteOffsetTimelineMap: Map<TrackNumber, NoteOffsetStateBasedEventTimeline<NoteOffset>> }

module Song =
    let empty =
        { Duration = 0.0
          Tracks = Map.empty
          ScaleTimeline = EventTimeline.empty
          TempoTimeline = StateTimeline.empty
          NoteOffsetTimelineMap = NoteOffsetStateTimelineMap.empty
          TrackNoteOffsetTimelineMap = Map.empty }
