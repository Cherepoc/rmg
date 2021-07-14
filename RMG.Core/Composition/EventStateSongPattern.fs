namespace RMG.CoreF.Composition

open RMG.CoreF

[<Sealed>]
type EventStateSongPattern
    private
    (
        duration: Duration,
        tracks: Map<TrackNumber, InstrumentTrack>,
        trackEventStatePatternTimeline: TrackEventStatePattern WithEvents EventTimeline,
        noteOffsets: Event list,
        flatSong: Song
    ) =
    member this.Duration = duration
    member this.Track = tracks
    member this.TrackEventStatePatternTimeline = trackEventStatePatternTimeline
    member this.NoteOffsets = noteOffsets
    member this.FlatSong = flatSong

    new(duration: Duration,
        tracksInput: InstrumentTrack seq,
        trackEventStatePatternTimelineInput: TrackEventStatePattern WithEvents Timeline,
        noteOffsets: Event seq) =
        let trackMap = tracksInput |> Seq.indexed |> Map.ofSeq

        let trackEventStatePatternTimeline =
            trackEventStatePatternTimelineInput |> EventTimeline.fromSequence

        let noteOffsets = noteOffsets |> List.ofSeq

        let trackNoteMap =
            seq {
                yield!
                    trackEventStatePatternTimeline
                    |> Timeline.collect
                        (fun (noteOffsets, pattern) ->
                            seq {
                                pattern.FlatTimelineMap
                                noteOffsets |> TrackEventStateTimelineMap.ofMultiple pattern.Duration
                            })

                yield!
                    noteOffsets
                    |> TrackEventStateTimelineMap.ofMultiple duration
                    |> Timeline.fromSingle
            }
            |> TrackEventStateTimelineMap.concat

        let flatSong : Song =
            {
                Duration = duration
                Tracks = trackMap
                TrackEventStateTimelineMap = trackNoteMap
            }

        EventStateSongPattern(duration, trackMap, trackEventStatePatternTimeline, noteOffsets, flatSong)

    new() = EventStateSongPattern(0.0, Map.empty, EventTimeline.empty, List.empty, Song.empty)
