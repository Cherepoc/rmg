namespace RMG.CoreF

open RMG.CoreF

[<NoEquality>]
[<NoComparison>]
type Scale = { KeyOffsets: list<KeyOffset> }

type TimeSignature =
    { Numerator: uint8
      Denominator: uint8 }

[<NoEquality>]
[<NoComparison>]
type Song =
    { Duration: Duration
      Tracks: Map<TrackNumber, InstrumentTrack>
      ScaleTimeline: EventTimeline<Scale>
      TempoTimeline: StateTimeline<Tempo>
      NoteOffset: NoteOffset
      TrackNoteOffsetTimelineMap: TrackEventTimelineMap<NoteOffset> }

module Song =
    let empty : Song =
        { Duration = 0.0
          Tracks = Map.empty
          ScaleTimeline = EventTimeline.empty
          TempoTimeline = StateTimeline.empty
          NoteOffset = NoteOffset.empty
          TrackNoteOffsetTimelineMap = Map.empty }
