namespace RMG.Core

open System
open RMG.Core.Operators

module Rendering =
    type RenderedNote = { Offset: int; Velocity: Volume; Duration: Duration }

    type RenderedTrack =
        {
            Code: InstrumentCode
            IsPercussionTrack: bool
            Items: Timeline<RenderedNote>
        }

    type RenderedSong = { Duration: Duration; Tempo: Tempo StateTimeline; Tracks: RenderedTrack list }
    
    let private multiplyOffsets (offsets: float seq): float =
        offsets |> Seq.fold (fun result offset -> result * offset) 1

    let private chooseIndexByOffset (length: int) (offset: float): int =
        if length <= 0 then raise (ArgumentOutOfRangeException("{Length cannot be zero or negative"))
        else (Math.Floor(offset * (float length)) |> int)
        
    let private chooseIndexByOffsets (length: int) (offsets: float seq): int =
        if length <= 0 then raise (ArgumentOutOfRangeException("{Length cannot be zero or negative"))
        else offsets |> Seq.sumBy (chooseIndexByOffset length)
    
    let private tryChooseIndexByOffset (length: int) (offset: float): int option =
        if length <= 0 then None
        else Some (chooseIndexByOffset length offset)
        
    let private breakIndexByLength (length: int) (index: int): int * int =
        if length <= 0 then raise (ArgumentOutOfRangeException("{Length cannot be zero or negative"))
        else
            let remainder = index %! length
            let cycles = (index - remainder) / length
            (cycles, remainder)

    let private breakOffsetsByLength (length: int) (offsets: float seq): option<int * int> =
        if length <= 0 then None
        else
            let offset =
                offsets
                |> Seq.choose (tryChooseIndexByOffset length)
                |> Seq.sum
            let remainder = offset %! length
            let cycles = (offset - remainder) / length
            Some (cycles, remainder)

    let private chooseItemByOffset<'T> (offset: float) (items: 'T list) : 'T option =
        tryChooseIndexByOffset items.Length offset
        |> Option.map (fun index -> items[index])

    let private chooseItemsByOffsets<'T when 'T: equality and 'T: comparison> (offsets: float list) (items: 'T list) : 'T list =
        offsets
        |> List.choose (fun offset -> items |> chooseItemByOffset offset)
        |> List.distinct
        |> List.sort

    let renderSong (song: Song) : RenderedSong =
        let chromaticScale : KeyOffset list = [ 0 .. Constants.notesInOctave - 1 ]

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
                    match effectiveNote.ScaleOffsets with
                    | [] -> chromaticScale
                    | _ -> effectiveNote.ScaleOffsets |> List.distinct
                    
                let chordRootOffsetInScaleOffsetIndex = chooseIndexByOffsets scaleOffsets.Length effectiveNote.ChordRootOffset
                
                // chord offsets are chosen from effective scale offsets
                let chordScaleOffsetsInScaleOffsetIndexes =
                    match effectiveNote.ChordScaleOffsets with
                    | [] -> scaleOffsets
                    | _ ->
                        effectiveNote.ChordScaleOffsets
                            |> List.map (chooseIndexByOffset scaleOffsets.Length)
                            |> List.distinct
                
                // note offsets are chosen from effective chord offsets
                effectiveNote.ChordNoteOffsets
                |> Seq.map (chooseIndexByOffset chordScaleOffsetsInScaleOffsetIndexes.Length)
                |> Seq.distinct
                |> Seq.map (fun chordNoteIndex ->
                    let chordOctaves, chordNoteIndex = breakIndexByLength chordScaleOffsetsInScaleOffsetIndexes.Length chordNoteIndex
                    let chordScaleIndex = chordRootOffsetInScaleOffsetIndex + chordScaleOffsetsInScaleOffsetIndexes[chordNoteIndex]
                    let scaleOctaves, scaleNoteIndex = breakIndexByLength scaleOffsets.Length chordScaleIndex
                    let scaleKeyOffset = scaleOffsets[scaleNoteIndex]
                    let octaveOffset = List.append effectiveNote.OctaveOffset [chordOctaves; scaleOctaves] |> List.sum
                    let keyOffset = List.append effectiveNote.KeyOffset [scaleKeyOffset] |> List.sum
                    let offset = fixOffset (keyOffset + octaveOffset * Constants.notesInOctave)
                    let renderedNote =
                            {
                                Offset = offset
                                Velocity = multiplyOffsets effectiveNote.Velocity
                                Duration = multiplyOffsets effectiveNote.Duration
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
                
                match effectiveNote.ArticulationOffset with
                | [] -> Seq.empty
                | _ ->
                    let articulationIndex = chooseIndexByOffsets articulationCodes.Length effectiveNote.ArticulationOffset
                    let _, fixedArticulationIndex = breakIndexByLength articulationCodes.Length articulationIndex
                    let renderedNote =
                        {
                            Offset = int articulationCodes[fixedArticulationIndex]
                            Velocity = multiplyOffsets effectiveNote.Velocity
                            Duration = multiplyOffsets effectiveNote.Duration
                        }
                    Seq.singleton (position, renderedNote)

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
