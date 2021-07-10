namespace RMG.CoreF

open System
open RMG.CoreF.Operators

module Rendering =
    type RenderedNote = { Offset: int; Velocity: Volume; Duration: Duration }

    type RenderedTrack =
        {
            Code: InstrumentCode
            IsPercussionTrack: bool
            Items: EventTimeline<RenderedNote>
        }

    type RenderedSong =
        {
            Duration: Duration
            Tempo: EventTimeline<Tempo>
            Tracks: list<RenderedTrack>
        }

    let renderSong (song: Song) : RenderedSong =
        let renderPitchInstrumentTrack (track: PitchInstrumentTrack, notes: EventTimeline<NoteOffset>) : RenderedTrack =
            let minTrackNoteOffset =
                (track.MinOctaveOffset + Constants.zeroOctaveOffset) * Constants.notesInOctave

            let maxTrackNoteOffset =
                (track.MaxOctaveOffset + Constants.zeroOctaveOffset + 1)
                * Constants.notesInOctave

            let trackNoteOffsetWidth = maxTrackNoteOffset - minTrackNoteOffset

            let renderNote ((position, noteOffset): TimelineItem<NoteOffset>) : TimelineItem<RenderedNote> =
                let scale =
                    song.ScaleTimeline |> EventTimeline.lastEffectiveValue position

                let noteOffsetModulo = noteOffset.ScaleOffset %! 1.0

                let scaleOffset =
                    match scale with
                    | Some scale -> scale.KeyOffsets.[int (floor (noteOffsetModulo * (float scale.KeyOffsets.Length)))]
                    | _ -> int (floor (noteOffsetModulo * (float Constants.notesInOctave)))

                let offset =
                    scaleOffset
                    + noteOffset.KeyOffset
                    + (noteOffset.OctaveOffset * Constants.notesInOctave)

                let fixedOffset =
                    match offset with
                    | offset when offset >= maxTrackNoteOffset ->
                        let period =
                            int (ceil (double (offset - maxTrackNoteOffset) / double trackNoteOffsetWidth))

                        offset - period * trackNoteOffsetWidth
                    | offset when offset < minTrackNoteOffset ->
                        let period =
                            int (ceil (double (minTrackNoteOffset - offset) / double trackNoteOffsetWidth))

                        offset + period * trackNoteOffsetWidth
                    | _ -> offset

                let renderedNote =
                    {
                        Offset = fixedOffset
                        Velocity = noteOffset.Velocity
                        Duration = noteOffset.Duration
                    }

                (position, renderedNote)

            let renderedNotes =
                notes
                |> Timeline.trimDuration song.Duration
                |> Seq.map renderNote
                |> EventTimeline.fromSequence

            {
                Code = track.Instrument.Code
                IsPercussionTrack = false
                Items = renderedNotes
            }

        let renderPercussionInstrumentTrack (track: PercussionInstrumentTrack, notes: EventTimeline<NoteOffset>) : EventTimeline<RenderedNote> =
            let articulationCodes = track.Instrument.ArticulationCodes

            let renderNote ((position, noteOffset): TimelineItem<NoteOffset>) : TimelineItem<RenderedNote> =
                let noteOffsetModulo = noteOffset.ScaleOffset %! 1.0

                let offsetIndex = floor (noteOffsetModulo * (float articulationCodes.Length))

                let offset = articulationCodes.[int offsetIndex]

                let renderedNote =
                    {
                        Offset = int (offset)
                        Velocity = noteOffset.Velocity
                        Duration = noteOffset.Duration
                    }

                (position, renderedNote)

            notes
            |> Timeline.trimDuration song.Duration
            |> Seq.map renderNote
            |> EventTimeline.fromSequence

        let trackNumbers =
            seq {
                yield! song.Tracks |> Map.toSeq |> Seq.map fst

                yield! song.TrackNoteOffsetTimelineMap |> Map.toSeq |> Seq.map fst
            }
            |> Seq.distinct
            |> List.ofSeq

        let renderedPitchTracks =
            trackNumbers
            |> Seq.choose
                (fun trackNumber ->
                    let track = song.Tracks |> Map.tryFind trackNumber

                    let trackNoteOffsetTimelineMap = song.TrackNoteOffsetTimelineMap |> Map.tryFind trackNumber

                    match (track, trackNoteOffsetTimelineMap) with
                    | Some (PitchInstrumentTrack track), Some trackNoteOffsetTimelineMap -> Some(renderPitchInstrumentTrack (track, trackNoteOffsetTimelineMap))
                    | _ -> None)
            |> Seq.toList

        let renderedPercussionTrack =
            {
                Code = 0uy
                IsPercussionTrack = true
                Items =
                    trackNumbers
                    |> Seq.map
                        (fun trackNumber ->
                            let track = song.Tracks |> Map.tryFind trackNumber

                            let trackNoteOffsetTimelineMap = song.TrackNoteOffsetTimelineMap |> Map.tryFind trackNumber

                            match (track, trackNoteOffsetTimelineMap) with
                            | Some (PercussionInstrumentTrack track), Some trackNoteOffsetTimelineMap ->
                                renderPercussionInstrumentTrack (track, trackNoteOffsetTimelineMap)
                                |> Timeline.itemFromSingle
                            | _ -> EventTimeline.empty |> Timeline.itemFromSingle)
                    |> EventTimeline.merge
            }

        let renderedTracks =
            seq {
                yield! renderedPitchTracks
                yield renderedPercussionTrack
            }
            |> Seq.toList

        let maxVolume =
            renderedTracks
            |> Seq.collect (fun x -> x.Items)
            |> Seq.map (fun (_, value) -> value.Velocity)
            |> Seq.max

        let fixTrackVolume (timeline: Timeline<RenderedNote>) : EventTimeline<RenderedNote> =
            timeline
            |> Timeline.map (fun note -> { note with Velocity = note.Velocity / maxVolume })
            |> EventTimeline.fromSequence

        let fixedVolumeTracks =
            renderedTracks
            |> List.map (fun track -> { track with Items = fixTrackVolume track.Items })

        {
            Duration = song.Duration
            Tempo = song.TempoTimeline |> EventTimeline.fromSequence
            Tracks = fixedVolumeTracks
        }
