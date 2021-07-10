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

    let private chooseItemByOffset<'T> (offset: float) (items: 'T list) : 'T option =
        match items with
        | [] -> None
        | items -> Some items.[Math.Floor(offset * (float items.Length)) |> int]

    let private chooseItemsByOffsets<'T when 'T: equality and 'T: comparison> (offsets: float list) (items: 'T list) : 'T list =
        offsets
        |> List.choose (fun offset -> items |> chooseItemByOffset offset)
        |> List.distinct
        |> List.sort

    let renderSong (song: Song) : RenderedSong =
        let chromaticScale : KeyOffset list = [ 0 .. Constants.notesInOctave - 1 ]

        let breakOctaves (value: float) : int * float =
            let octaves = Math.Floor value
            (int octaves, value - octaves)

        let renderPitchInstrumentTrack (track: PitchInstrumentTrack, notes: EventTimeline<NoteOffset>) : RenderedTrack =
            let minTrackNoteOffset =
                (track.MinOctaveOffset + Constants.zeroOctaveOffset) * Constants.notesInOctave

            let maxTrackNoteOffset =
                (track.MaxOctaveOffset + Constants.zeroOctaveOffset + 1)
                * Constants.notesInOctave

            let trackNoteOffsetWidth = maxTrackNoteOffset - minTrackNoteOffset

            let fixOffset (offset: int) : int =
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

            let renderNote ((position, noteOffset): TimelineItem<NoteOffset>) : seq<TimelineItem<RenderedNote>> =
                let scaleOffsets =
                    song.ScaleTimeline
                    |> EventTimeline.lastEffectiveValue position
                    |> Option.map (fun x -> x.KeyOffsets)
                    |> Option.defaultValue chromaticScale

                let chordRootOctaves, chordRootRemainder = breakOctaves noteOffset.ChordRootOffset

                let chordRootOffset =
                    scaleOffsets |> chooseItemByOffset chordRootRemainder |> Option.defaultValue 0

                scaleOffsets
                |> chooseItemsByOffsets noteOffset.ChordScaleOffsets
                |> chooseItemsByOffsets noteOffset.ChordNoteOffsets
                |> Seq.map
                    (fun chordOffset ->
                        let offset =
                            chordOffset
                            + chordRootOffset
                            + noteOffset.KeyOffset
                            + ((noteOffset.OctaveOffset + chordRootOctaves) * Constants.notesInOctave)
                            |> fixOffset

                        let renderedNote =
                            {
                                Offset = offset
                                Velocity = noteOffset.Velocity
                                Duration = noteOffset.Duration
                            }

                        (position, renderedNote))

            let renderedNotes =
                notes
                |> Timeline.trimDuration song.Duration
                |> Seq.collect renderNote
                |> EventTimeline.fromSequence

            {
                Code = track.Instrument.Code
                IsPercussionTrack = false
                Items = renderedNotes
            }

        let renderPercussionInstrumentTrack (track: PercussionInstrumentTrack, notes: EventTimeline<NoteOffset>) : EventTimeline<RenderedNote> =
            let articulationCodes = track.Instrument.ArticulationCodes

            let renderNotes ((position, noteOffset): TimelineItem<NoteOffset>) : TimelineItem<RenderedNote> seq =
                seq { articulationCodes |> chooseItemByOffset (noteOffset.ChordRootOffset %! 1.0) }
                |> Seq.choose id
                |> Seq.map
                    (fun offset ->
                        let renderedNote =
                            {
                                Offset = int offset
                                Velocity = noteOffset.Velocity
                                Duration = noteOffset.Duration
                            }

                        (position, renderedNote))

            notes
            |> Timeline.trimDuration song.Duration
            |> Seq.collect renderNotes
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
