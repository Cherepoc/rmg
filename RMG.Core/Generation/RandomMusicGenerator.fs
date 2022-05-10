namespace RMG.Core.Generation

open RMG.Core
open RMG.Core.Composition

module RandomMusicGenerator =
    let generate () : EventStateSongPattern =
        let halfProbabilityFunction offset =
            Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.5, offset)

        let quarterProbabilityFunction offset =
            Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.25, offset)

        let context = Generate.Context()
        let songDuration = 256.0
        let pitchInstrumentTrackCount = context |> Generate.int (4, 8)
        let percussionInstrumentTrackCount = context |> Generate.int (4, 8)
        let partPitchInstrumentTrackCount () = context |> Generate.int (1, 4)
        let partPercussionInstrumentTrackCount () = context |> Generate.int (1, 4)

        let standardVelocity () =
            context |> Generate.float (0.875, 1.125)

        let standardDuration () = context |> Generate.float (0.8, 1.25)

        let standardOffsets (min: int, max: int) : float list =
            context
            |> Generate.sequence
                (fun context -> context |> Generate.float (0.0, 1.0))
                (context
                 |> Generate.intByRank (halfProbabilityFunction 0, min, max))
            |> List.ofSeq

        let standardOctaveOffset () =
            context
            |> Generate.intByRank (halfProbabilityFunction 0, -2, 2)

        let standardArticulationOffset () =
            Probability.floatSpline 1.0 (context |> Generate.float (-1.0, 1.0))

        let scale: ScaleOffsets list = [ 0; 2; 3; 5; 7; 8; 10 ]

        let tempo = context |> Generate.float (0.5, 1.0)

        let songStyleDef: TrackStyleDefinition =
            TrackStyleDefinition.generateSongStyleDefinition
                { SharedStyleCount = 4
                  PitchInstrumentStyleCount = 8
                  PercussionInstrumentStyleCount = 8 }

        let songStyle =
            TrackStyleGeneration.generateSongStyle { TrackStyleDefinition = songStyleDef }

        let sharedStyles: (TrackNumber * TrackStyle) list =
            songStyle.ChildStyles
            |> TrackStyleGeneration.filterSharedTrackStyles

        let pitchInstrumentStyles: (TrackNumber * TrackStyle) list =
            songStyle.ChildStyles
            |> TrackStyleGeneration.filterNoteTrackStyles

        let percussionInstrumentStyles: (TrackNumber * TrackStyle) list =
            songStyle.ChildStyles
            |> TrackStyleGeneration.filterMultiTrackStyles
            |> List.collect (fun (_, style) -> TrackStyleGeneration.filterNoteTrackStyles style.ChildStyles)

        let weightedPercussionInstrumentDefinitions: struct (PercussionInstrumentDefinition * float) seq =
            InstrumentDefinition.percussionInstrumentDefinitions
            |> Seq.map (fun x -> struct (x, x.Weight))

        let percussionInstruments: (PercussionInstrumentDefinition * PercussionInstrumentTrack) list =
            context
            |> Generate.subSequenceWeighted weightedPercussionInstrumentDefinitions percussionInstrumentTrackCount
            |> Seq.map
                (fun instrumentDefinition ->
                    let articulationCount =
                        context
                        |> Generate.intByRank (
                            quarterProbabilityFunction instrumentDefinition.ArticulationCodes.Length,
                            1,
                            instrumentDefinition.ArticulationCodes.Length
                        )

                    let articulationCodes =
                        context
                        |> Generate.subSequence instrumentDefinition.ArticulationCodes articulationCount
                        |> List.ofSeq

                    let instrumentTrack =
                        { Instrument = { ArticulationCodes = articulationCodes }
                          EventState =
                              { EventState.empty with
                                  Velocity = [standardVelocity ()]
                                  ArticulationOffset = [standardArticulationOffset ()]
                              }}

                    (instrumentDefinition, instrumentTrack))
            |> List.ofSeq

        let pitchInstrumentTracks =
            context
            |> Generate.sequence
                (fun context ->
                    let minOctave =
                        context
                        |> Generate.intByRank (halfProbabilityFunction 0, -3, -1)

                    let maxOctave =
                        minOctave
                        + (context
                           |> Generate.intByRank (halfProbabilityFunction 0, 1, 0 - minOctave))

                    {
                        Instrument = { Code = byte (context |> Generate.int (0, 120)) }
                        MinOctaveOffset = minOctave
                        MaxOctaveOffset = maxOctave
                        EventState =
                        { EventState.empty with
                            Duration = [standardDuration ()]
                            Velocity = [standardVelocity ()]
                            OctaveOffset = [standardOctaveOffset ()]
                            ChordNoteOffsets = standardOffsets (0, 2)
                        }
                    })
                pitchInstrumentTrackCount
            |> List.ofSeq

        let tracks =
            seq {
                yield!
                    pitchInstrumentTracks
                    |> Seq.map PitchInstrumentTrack

                yield!
                    percussionInstruments
                    |> Seq.map
                        (fun (_, percussionInstrumentTrack) -> PercussionInstrumentTrack percussionInstrumentTrack)
            }
            |> List.ofSeq

        let numberedPitchInstrumentTracks =
            tracks
            |> List.indexed
            |> List.filter
                (fun (_, track) ->
                    match track with
                    | PitchInstrumentTrack _ -> true
                    | _ -> false)

        let numberedPercussionInstrumentTracks =
            tracks
            |> List.indexed
            |> List.filter
                (fun (_, track) ->
                    match track with
                    | PercussionInstrumentTrack _ -> true
                    | _ -> false)

        let parts =
            let _, parts = songStyle.PartDefinitions |> List.last
            context |> Generate.subSequence parts 4

        let generatePart () : TrackEventStatePattern =
            let pitchTrackCount = partPitchInstrumentTrackCount ()

            let pitchTrackNumberMap =
                context
                |> Generate.subSequence numberedPitchInstrumentTracks pitchTrackCount
                |> Seq.map
                    (fun (trackNumber, _) ->
                        let styleNumber, _ =
                            context |> Generate.item pitchInstrumentStyles

                        (Some trackNumber, Some styleNumber))

            let percussionTrackCount = partPercussionInstrumentTrackCount ()

            let percussionTrackNumberMap =
                context
                |> Generate.subSequence numberedPercussionInstrumentTracks percussionTrackCount
                |> Seq.map
                    (fun (trackNumber, _) ->
                        let styleNumber, _ =
                            context
                            |> Generate.item percussionInstrumentStyles

                        (Some trackNumber, Some styleNumber))

            let sharedStyleNumber =
                context
                |> Generate.item (sharedStyles |> Seq.map fst)

            let trackNumberMap =
                seq {
                    yield (None, Some sharedStyleNumber)
                    yield! pitchTrackNumberMap
                    yield! percussionTrackNumberMap
                }
                |> Map.ofSeq

            let part = context |> Generate.item parts

            TrackEventStatePattern.ofTimelineMaps
                part.Duration
                (Some trackNumberMap)
                Map.empty
                (struct (EventState.empty, part)
                 |> Timeline.ofSingle)

        let noteOffset () =
            { EventState.empty with
                Duration = [standardDuration ()]
                KeyOffset = [context |> Generate.int (-6, 6)]
                OctaveOffset = [standardOctaveOffset ()]
                ChordRootOffset = [standardArticulationOffset ()]
                ChordScaleOffsets = standardOffsets (0, 1)
                ChordNoteOffsets = standardOffsets (0, 1)
                Velocity = [standardVelocity ()]
                ArticulationOffset = [standardArticulationOffset ()]
            }

        let songTimeline: TrackEventStatePattern WithEventState Timeline =
            context
            |> Generate.sequentialTimeline
                (fun _ ->
                    let item = generatePart ()
                    ((noteOffset (), item), item.Duration))
                songDuration

        let songNoteOffsets =
            { EventState.empty with
                Duration = [context |> Generate.float (0.5, 2.0)]
                KeyOffset = [context |> Generate.int (-6, 6)]
                ScaleOffsets = scale
                Tempo = [tempo]
            }

        let trackMap = tracks |> List.indexed |> Map.ofSeq

        EventStateSongPattern.ofTimelines songDuration trackMap songTimeline songNoteOffsets
