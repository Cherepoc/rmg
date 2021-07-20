namespace RMG.CoreF

open RMG.CoreF

type TimeSignature = { Numerator: uint8; Denominator: uint8 }

[<NoEquality>]
[<NoComparison>]
type Song =
    {
        Duration: Duration
        Tracks: Map<TrackNumber, InstrumentTrack>
        TrackEventStateTimelineMap: TrackEventStateTimelineMap
    }
