namespace RMG.CoreF

open System
open RMG.CoreF.Operators

module Rendering =
    type RenderedNote =
        { Offset: int
          Velocity: Volume
          Duration: Duration }

    type RenderedTrack = { Code: InstrumentCode; Items: EventTimeline<RenderedNote>; IsPercussionTrack: Boolean }

    type RenderedSong =
        { Duration: Duration
          Tempo: EventTimeline<Tempo>
          Tracks: list<RenderedTrack> }

    let renderSong (song: Song) : RenderedSong =
        let renderPitchInstrumentTrack (track: PitchInstrumentTrack, notes: SubTrackEventTimelineMap<NoteOffset>) : RenderedTrack =
            let minTrackNoteOffset =
                (track.MinOctaveOffset + Constants.zeroOctaveOffset)
                * Constants.notesInOctave

            let maxTrackNoteOffset =
                (track.MaxOctaveOffset
                 + Constants.zeroOctaveOffset
                 + 1)
                * Constants.notesInOctave

            let trackNoteOffsetWidth = maxTrackNoteOffset - minTrackNoteOffset

            let renderNote (item: TimelineItem<NoteOffset>) : TimelineItem<RenderedNote> =
                let noteOffset = item.Value

                let scale =
                    song.ScaleTimeline
                    |> EventTimeline.lastEffectiveValue item.Position

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
                            int (
                                ceil (
                                    double (offset - maxTrackNoteOffset)
                                    / double trackNoteOffsetWidth
                                )
                            )

                        offset - period * trackNoteOffsetWidth
                    | offset when offset < minTrackNoteOffset ->
                        let period =
                            int (
                                ceil (
                                    double (minTrackNoteOffset - offset)
                                    / double trackNoteOffsetWidth
                                )
                            )

                        offset + period * trackNoteOffsetWidth
                    | _ -> offset

                { Position = item.Position
                  Value =
                      { Offset = fixedOffset
                        Velocity = noteOffset.Velocity
                        Duration = noteOffset.Duration } }

            let renderedNotes =
                notes
                |> Map.toSeq
                |> Seq.collect snd
                |> Seq.where (fun x -> x.Position < song.Duration)
                |> Seq.map renderNote
                |> EventTimeline.fromSequence

            { Code = track.Instrument.Code; Items = renderedNotes; IsPercussionTrack = false }

        let renderPercussionInstrumentTrack
            (
                track: PercussionInstrumentTrack,
                timelineMap: SubTrackEventTimelineMap<NoteOffset>
            ) : RenderedTrack =
            let articulationCodeMap =
                track.Instruments
                |> Seq.map (fun x -> x.ArticulationCodes)
                |> Seq.indexed
                |> Map.ofSeq

            let renderSubTrack (timeline: EventTimeline<NoteOffset>, articulationCodes: list<ArticulationCode>): Timeline<RenderedNote> =
                let renderNote (item: TimelineItem<NoteOffset>) : TimelineItem<RenderedNote> =
                    let noteOffset = item.Value

                    let noteOffsetModulo = noteOffset.ScaleOffset %! 1.0

                    let offsetIndex =
                        floor (
                            noteOffsetModulo
                            * (float articulationCodes.Length)
                        )

                    let offset = articulationCodes.[int offsetIndex]

                    { Position = item.Position
                      Value =
                          { Offset = int (offset)
                            Velocity = noteOffset.Velocity
                            Duration = noteOffset.Duration } }

                timeline
                |> Seq.where (fun x -> x.Position < song.Duration)
                |> Seq.map renderNote

            let notes =
                articulationCodeMap
                |> Map.toSeq
                |> Seq.map fst
                |> Seq.collect (fun subTrackIndex ->
                    let subTrackArticulationCodes = articulationCodeMap.[subTrackIndex]
                    match timelineMap |> Map.tryFind subTrackIndex with
                    | Some subTrackTimeline -> renderSubTrack (subTrackTimeline, subTrackArticulationCodes)
                    | _ -> Seq.empty)
                |> EventTimeline.fromSequence

            {Code = 0uy; Items = notes; IsPercussionTrack = true}

        let trackNumbers =
            seq {
                yield! song.Tracks |> Map.toSeq |> Seq.map fst

                yield!
                    song.TrackNoteOffsetTimelineMap
                    |> Map.toSeq
                    |> Seq.map fst
            }
            |> Seq.distinct
            |> List.ofSeq

        let renderedTracks =
            trackNumbers
            |> Seq.choose
                (fun trackNumber ->
                    let track = song.Tracks |> Map.tryFind trackNumber

                    let trackNoteOffsetTimelineMap =
                        song.TrackNoteOffsetTimelineMap
                        |> Map.tryFind trackNumber

                    match (track, trackNoteOffsetTimelineMap) with
                    | Some (PitchInstrumentTrack track), Some trackNoteOffsetTimelineMap ->
                        Some(renderPitchInstrumentTrack (track, trackNoteOffsetTimelineMap))
                    | Some (PercussionInstrumentTrack track), Some trackNoteOffsetTimelineMap ->
                        Some(renderPercussionInstrumentTrack (track, trackNoteOffsetTimelineMap))
                    | _ -> None)
            |> Seq.toList

        let maxVelocity =
            renderedTracks
            |> Seq.collect (fun x -> x.Items)
            |> Seq.map (fun x -> x.Value.Velocity)
            |> Seq.max

        let fixTrackVelocity (timeline: Timeline<RenderedNote>) : EventTimeline<RenderedNote> =
            timeline
            |> Timeline.map
                (fun note ->
                    { note with
                          Velocity = note.Velocity / maxVelocity })
            |> EventTimeline.fromSequence

        let fixedVelocityTracks =
            renderedTracks
            |> List.map (fun renderedTrack -> {renderedTrack with Items = fixTrackVelocity renderedTrack.Items})

        { Duration = song.Duration
          Tempo = song.TempoTimeline |> EventTimeline.fromSequence
          Tracks = fixedVelocityTracks }
