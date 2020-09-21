namespace RMG.CoreF

type Tempo = double

type Instrument = { Code: byte }

[<NoEquality>]
[<NoComparison>]
type Scale = { KeyOffsets: seq<KeyOffset> }

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
      ScaleTimeline: Timeline<Scale>
      NoteOffsetTimelineMap: NoteOffsetTimelineMap
      Tempo: Tempo
      TrackNoteOffsetTimelineMap: TrackNoteOffsetTimelineMap<NoteOffset> }
