namespace RMG.Core.Composition

open RMG.Core

[<Sealed>]
type EventStateSongPattern
    private
    (
        duration: Duration,
        tracks: Map<TrackNumber, InstrumentTrack>,
        trackEventStatePatternTimeline: TrackEventStatePattern WithEventState Timeline,
        eventState: EventState,
        flatSong: Song
    ) =
    member this.Duration = duration
    member this.Track = tracks
    member this.TrackEventStatePatternTimeline = trackEventStatePatternTimeline
    member this.EventState = eventState
    member this.FlatSong = flatSong

    static member internal ofTimelineMapsUnsafe
        (duration: Duration)
        (tracks: Map<TrackNumber, InstrumentTrack>)
        (trackEventStatePatternTimeline: TrackEventStatePattern WithEventState Timeline)
        (eventState: EventState)
        (flatSong: Song)
        : EventStateSongPattern =
        EventStateSongPattern(duration, tracks, trackEventStatePatternTimeline, eventState, flatSong)

module EventStateSongPattern =
    let ofTimelines
        (duration: Duration)
        (tracks: Map<TrackNumber, InstrumentTrack>)
        (trackEventStatePatternTimeline: TrackEventStatePattern WithEventState Timeline)
        (eventState: EventState)
        : EventStateSongPattern =
        let trackNoteMap =
            trackEventStatePatternTimeline
            |> Timeline.map (fun struct (eventState, pattern) -> struct (eventState, pattern.FlatTimelineMap))
            |> TrackEventStateTimelineMap.concatCombined duration
            |> TrackEventStateTimelineMap.shiftState eventState

        let flatSong : Song =
            {
                Duration = duration
                Tracks = tracks
                TrackEventStateTimelineMap = trackNoteMap
            }

        EventStateSongPattern.ofTimelineMapsUnsafe duration tracks trackEventStatePatternTimeline eventState flatSong
