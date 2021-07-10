namespace RMG.CoreF.Composition

open RMG.CoreF

[<Sealed>]
type SongPatternMap
    private
    (
        duration: Duration,
        tracks: Map<TrackNumber, InstrumentTrack>,
        trackNotePatternMapTimeline: EventTimeline<CompositeTrackEventPattern<NoteOffset> * NoteOffset>,
        noteOffset: NoteOffset,
        tempoPatternTimeline: EventTimeline<StatePattern<Tempo>>,
        scalePatternTimeline: EventTimeline<EventPattern<Scale>>,
        flatSong: Song
    ) =
    member this.Duration = duration
    member this.Track = tracks
    member this.TrackNotePatternMapTimeline = trackNotePatternMapTimeline
    member this.NoteOffset = noteOffset
    member this.TempoPatternTimeline = tempoPatternTimeline
    member this.ScalePatternTimeline = scalePatternTimeline
    member this.FlatSong = flatSong

    new(duration: Duration,
        tracks: seq<InstrumentTrack>,
        trackNotePatternMapTimeline: Timeline<CompositeTrackEventPattern<NoteOffset> * NoteOffset>,
        noteOffset: NoteOffset,
        tempoPatternTimeline: Timeline<StatePattern<Tempo>>,
        scalePatternTimeline: Timeline<EventPattern<Scale>>) =
        let trackMap = tracks |> Seq.indexed |> Map.ofSeq

        let orderedTrackNotePatternMapTimeline = trackNotePatternMapTimeline |> EventTimeline.fromSequence

        let trackNoteMap =
            orderedTrackNotePatternMapTimeline
            |> Timeline.map (fun (pattern, offset) -> (pattern.FlatTimelineMap, offset))
            |> TrackEventTimelineMap.mergeComposite NoteOffset.merge
            |> TrackEventTimelineMap.map (fun x -> NoteOffset.merge (x, noteOffset))

        let orderedTempoPatternTimeline = tempoPatternTimeline |> EventTimeline.fromSequence

        let tempoTimeline =
            orderedTempoPatternTimeline
            |> Timeline.map (fun x -> x.FlatTimeline)
            |> StateTimeline.merge StateMerger.multiplicativeFloat

        let orderedScalePatternTimeline = scalePatternTimeline |> EventTimeline.fromSequence

        let scaleTimeline =
            orderedScalePatternTimeline
            |> Timeline.map (fun x -> x.FlatTimeline)
            |> EventTimeline.merge

        let flatSong : Song =
            {
                Duration = duration
                Tracks = trackMap
                ScaleTimeline = scaleTimeline
                TempoTimeline = tempoTimeline
                NoteOffset = noteOffset
                TrackNoteOffsetTimelineMap = trackNoteMap
            }

        SongPatternMap(duration, trackMap, orderedTrackNotePatternMapTimeline, noteOffset, orderedTempoPatternTimeline, orderedScalePatternTimeline, flatSong)

    new() = SongPatternMap(0.0, Map.empty, EventTimeline.empty, NoteOffset.empty, EventTimeline.empty, EventTimeline.empty, Song.empty)
