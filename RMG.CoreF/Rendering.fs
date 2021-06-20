namespace RMG.CoreF

open RMG.CoreF.Operators

module Rendering =
    type RenderedNote =
        { Offset: int
          Volume: Volume
          Duration: Duration }

    type RenderedTrack =
        { Code: InstrumentCode
          Items: EventTimeline<RenderedNote> }

    type RenderedSong =
        { Duration: Duration
          Tempo: EventTimeline<Tempo>
          PitchInstrumentTracks: list<RenderedTrack>
          PercussionTimeline: EventTimeline<RenderedNote> }

    let renderSong (song: Song): RenderedSong =
        let renderPitchInstrumentTrack (track: PitchInstrumentTrack, notes: NoteOffsetStateBasedEventTimeline<NoteOffset>): RenderedTrack =
            let trackNoteOffsetStateTimeline =
                seq {
                    { Position = 0.0
                      Value = song.NoteOffsetTimelineMap }

                    { Position = 0.0
                      Value = notes.NoteOffsetStateTimelineMap }
                }
                |> NoteOffsetStateTimelineMap.merge

            let minTrackNoteOffset =
                (track.MinOctaveOffset + Constants.zeroOctaveOffset)
                * Constants.notesInOctave

            let maxTrackNoteOffset =
                (track.MaxOctaveOffset
                 + Constants.zeroOctaveOffset
                 + 1)
                * Constants.notesInOctave

            let trackNoteOffsetWidth = maxTrackNoteOffset - minTrackNoteOffset

            let renderNote (item: TimelineItem<NoteOffset>): TimelineItem<RenderedNote> =
                let effectiveNoteOffset =
                    trackNoteOffsetStateTimeline
                    |> NoteOffsetStateTimelineMap.effective item.Position

                let noteOffset =
                    effectiveNoteOffset |> NoteOffset.merge item.Value

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
                            int
                                (ceil
                                    (double (offset - maxTrackNoteOffset)
                                     / double trackNoteOffsetWidth))

                        offset - period * trackNoteOffsetWidth
                    | offset when offset < minTrackNoteOffset ->
                        let period =
                            int
                                (ceil
                                    (double (minTrackNoteOffset - offset)
                                     / double trackNoteOffsetWidth))

                        offset + period * trackNoteOffsetWidth
                    | _ -> offset

                { Position = item.Position
                  Value =
                      { Offset = fixedOffset
                        Volume = noteOffset.Volume
                        Duration = noteOffset.Duration } }

            let renderedNotes =
                notes.Timeline
                |> Seq.where (fun x -> x.Position < song.Duration)
                |> Seq.map renderNote
                |> EventTimeline.fromSequence

            { Code = track.Instrument.Code
              Items = renderedNotes }

        let renderPercussionInstrumentTrack (track: PercussionInstrumentTrack, notes: NoteOffsetStateBasedEventTimeline<NoteOffset>): EventTimeline<RenderedNote> =
            let trackNoteOffsetStateTimeline =
                seq {
                    { Position = 0.0
                      Value = song.NoteOffsetTimelineMap }

                    { Position = 0.0
                      Value = notes.NoteOffsetStateTimelineMap }
                }
                |> NoteOffsetStateTimelineMap.merge

            let articulationCodes = track.Instrument.ArticulationCodes

            let renderNote (item: TimelineItem<NoteOffset>): TimelineItem<RenderedNote> =
                let effectiveNoteOffset =
                    trackNoteOffsetStateTimeline
                    |> NoteOffsetStateTimelineMap.effective item.Position

                let noteOffset =
                    effectiveNoteOffset |> NoteOffset.merge item.Value

                let noteOffsetModulo = noteOffset.ScaleOffset %! 1.0
                let offset = articulationCodes.[int (floor (noteOffsetModulo * (float articulationCodes.Length)))]

                { Position = item.Position
                  Value =
                      { Offset = int(offset)
                        Volume = noteOffset.Volume
                        Duration = noteOffset.Duration } }

            notes.Timeline
            |> Seq.where (fun x -> x.Position < song.Duration)
            |> Seq.map renderNote
            |> EventTimeline.fromSequence

        let trackNumbers =
            seq {
                yield!
                    song.Tracks
                    |> Map.toSeq
                    |> Seq.map fst

                yield!
                    song.TrackNoteOffsetTimelineMap
                    |> Map.toSeq
                    |> Seq.map fst
            }
            |> Seq.distinct
            |> List.ofSeq

        let renderedPitchTracks =
            trackNumbers
            |> Seq.choose (fun trackNumber ->
                let track = song.Tracks |> Map.tryFind trackNumber

                let trackNoteOffsetTimelineMap =
                    song.TrackNoteOffsetTimelineMap
                    |> Map.tryFind trackNumber

                match (track, trackNoteOffsetTimelineMap) with
                | Some(PitchInstrumentTrack track), Some trackNoteOffsetTimelineMap -> Some(renderPitchInstrumentTrack (track, trackNoteOffsetTimelineMap))
                | _ -> None)
            |> Seq.toList

        let renderedPercussionTimeline =
            trackNumbers
            |> Seq.map (fun trackNumber ->
                let track = song.Tracks |> Map.tryFind trackNumber

                let trackNoteOffsetTimelineMap =
                    song.TrackNoteOffsetTimelineMap
                    |> Map.tryFind trackNumber

                match (track, trackNoteOffsetTimelineMap) with
                | Some(PercussionInstrumentTrack track), Some trackNoteOffsetTimelineMap ->
                    {Position = 0.0; Value = renderPercussionInstrumentTrack (track, trackNoteOffsetTimelineMap)}
                | _ -> {Position = 0.0; Value = EventTimeline.empty})
            |> EventTimeline.merge

        let maxVolume =
            renderedPitchTracks
            |> Seq.collect (fun track -> track.Items)
            |> Seq.map (fun noteItem -> noteItem.Value.Volume)
            |> Seq.max

        let fixedVolumeTracks =
            renderedPitchTracks
            |> List.map (fun track ->
                { track with
                      Items =
                          track.Items
                          |> Timeline.map (fun note ->
                              { note with
                                    Volume = note.Volume / maxVolume })
                          |> EventTimeline.fromSequence })

        { Duration = song.Duration
          Tempo = song.TempoTimeline |> EventTimeline.fromSequence
          PitchInstrumentTracks = fixedVolumeTracks
          PercussionTimeline = renderedPercussionTimeline }
