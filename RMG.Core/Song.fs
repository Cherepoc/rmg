namespace RMG.Core

open RMG.Core

type TimeSignature = { Numerator: uint8; Denominator: uint8 }

[<NoEquality>]
[<NoComparison>]
type Song =
    {
        Duration: Duration
        Tracks: Map<TrackNumber, InstrumentTrack>
        TrackEventStateTimelineMap: TrackEventStateTimelineMap
    }
