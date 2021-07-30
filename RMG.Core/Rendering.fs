namespace RMG.CoreF

open System
open RMG.CoreF.Operators

module Rendering =
    type RenderedNote = { Offset: int; Velocity: Volume; Duration: Duration }

    type RenderedTrack =
        {
            Code: InstrumentCode
            IsPercussionTrack: bool
            Items: Timeline<RenderedNote>
        }

    type RenderedSong = { Duration: Duration; Tempo: Tempo StateTimeline; Tracks: RenderedTrack list }

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

        let sharedEventTimeline =
            song.TrackEventStateTimelineMap
            |> Map.tryFind None
            |> Option.defaultValue (EventStateTimelineMap.empty song.Duration)

        let getEffectiveNote
            (noteEventState: EventState)
            (trackEventState: EventState)
            (position: Position)
            (trackEventTimeline: EventStateTimelineMap)
            : EventState =
            noteEventState
            |> EventState.merge (trackEventTimeline |> EventStateTimelineMap.effectiveState position)
            |> EventState.merge (sharedEventTimeline |> EventStateTimelineMap.effectiveState position)
            |> EventState.merge trackEventState

        let renderPitchInstrumentTrack (track: PitchInstrumentTrack, eventTimeline: EventStateTimelineMap) : RenderedTrack =
            let minTrackNoteOffset = (track.MinOctaveOffset + Constants.zeroOctaveOffset) * Constants.notesInOctave

            let maxTrackNoteOffset =
                (track.MaxOctaveOffset + Constants.zeroOctaveOffset + 1) * Constants.notesInOctave

            let trackNoteOffsetWidth = maxTrackNoteOffset - minTrackNoteOffset

            let fixOffset (offset: int) : int =
                match offset with
                | offset when offset >= maxTrackNoteOffset ->
                    let period = int (ceil (double (offset - maxTrackNoteOffset) / double trackNoteOffsetWidth))

                    offset - period * trackNoteOffsetWidth
                | offset when offset < minTrackNoteOffset ->
                    let period = int (ceil (double (minTrackNoteOffset - offset) / double trackNoteOffsetWidth))

                    offset + period * trackNoteOffsetWidth
                | _ -> offset

            let renderNote ((position, eventState): EventState TimelineItem) : seq<RenderedNote TimelineItem> =
                let effectiveNote = getEffectiveNote eventState track.EventState position eventTimeline

                let scaleOffsets =
                    if effectiveNote.ScaleOffsets |> List.isEmpty then
                        chromaticScale
                    else
                        effectiveNote.ScaleOffsets

                effectiveNote.ChordScaleOffsets
                |> chooseItemsByOffsets effectiveNote.ChordNoteOffsets
                |> Seq.map
                    (fun chordNoteOffset ->
                        let chordNoteOctaves, chordNoteRemainder = breakOctaves (effectiveNote.ChordRootOffset + chordNoteOffset)

                        let scaleOffset = scaleOffsets |> chooseItemByOffset chordNoteRemainder |> Option.defaultValue 0

                        let offset =
                            scaleOffset
                            + effectiveNote.KeyOffset
                            + ((effectiveNote.OctaveOffset + chordNoteOctaves) * Constants.notesInOctave)
                            |> fixOffset

                        let renderedNote =
                            {
                                Offset = offset
                                Velocity = effectiveNote.Velocity
                                Duration = effectiveNote.Duration
                            }

                        (position, renderedNote))

            let renderedNotes =
                eventTimeline.NoteTimeline
                |> Timeline.trimDuration song.Duration
                |> Seq.collect renderNote
                |> Timeline.ofSeq

            {
                Code = track.Instrument.Code
                IsPercussionTrack = false
                Items = renderedNotes
            }

        let renderPercussionInstrumentTrack (track: PercussionInstrumentTrack, eventTimeline: EventStateTimelineMap) : RenderedNote Timeline =
            let articulationCodes = track.Instrument.ArticulationCodes

            let renderNotes ((position, eventState): EventState TimelineItem) : RenderedNote TimelineItem seq =
                let effectiveNote = getEffectiveNote eventState track.EventState position eventTimeline

                seq { articulationCodes |> chooseItemByOffset (effectiveNote.ArticulationOffset %! 1.0) }
                |> Seq.choose id
                |> Seq.map
                    (fun offset ->
                        let renderedNote =
                            {
                                Offset = int offset
                                Velocity = effectiveNote.Velocity
                                Duration = effectiveNote.Duration
                            }

                        (position, renderedNote))

            eventTimeline.NoteTimeline
            |> Timeline.trimDuration song.Duration
            |> Seq.collect renderNotes
            |> Timeline.ofSeq

        let trackNumbers = song.Tracks |> Map.toSeq |> Seq.map fst |> Seq.distinct |> List.ofSeq

        let renderedPitchTracks =
            trackNumbers
            |> Seq.choose
                (fun trackNumber ->
                    let track = song.Tracks |> Map.tryFind trackNumber

                    let trackNoteOffsetTimelineMap = song.TrackEventStateTimelineMap |> Map.tryFind (Some trackNumber)

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

                            let trackNoteOffsetTimelineMap = song.TrackEventStateTimelineMap |> Map.tryFind (Some trackNumber)

                            match (track, trackNoteOffsetTimelineMap) with
                            | Some (PercussionInstrumentTrack track), Some trackNoteOffsetTimelineMap ->
                                renderPercussionInstrumentTrack (track, trackNoteOffsetTimelineMap) |> Timeline.itemOfSingle
                            | _ -> Timeline.empty |> Timeline.itemOfSingle)
                    |> Timeline.ofSeq
                    |> Timeline.concat
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
            |> Seq.map (fun struct (_, value) -> value.Velocity)
            |> Seq.max

        let fixTrackVolume (timeline: RenderedNote Timeline) : RenderedNote Timeline =
            timeline |> Timeline.map (fun note -> { note with Velocity = note.Velocity / maxVolume })

        let fixedVolumeTracks =
            renderedTracks |> List.map (fun track -> { track with Items = fixTrackVolume track.Items })

        {
            Duration = song.Duration
            Tempo = sharedEventTimeline.TempoTimeline
            Tracks = fixedVolumeTracks
        }
