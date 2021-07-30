namespace RMG.CoreF

open RMG.CoreF.Composition

module RandomMusicGenerator =
    let generate () : EventStateSongPattern =
        let halfProbabilityFunction offset =
            Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.5, offset)

        let threeQuarterProbabilityFunction offset =
            Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.75, offset)

        let quarterProbabilityFunction offset =
            Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.25, offset)

        let eighthProbabilityFunction offset =
            Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.125, offset)

        let context = Generate.Context()
        let patternDuration = 1.0
        let higherPatternDuration = 4.0
        let partDuration = 16.0
        let higherPartDuration = 64.0
        let songDuration = 256.0
        let templateCount = 16
        let subTemplateCount = templateCount / 2
        let maxSubPatternCount = 4
        let pitchInstrumentTrackCount = context |> Generate.int (4, 8)
        let percussionInstrumentTrackCount = context |> Generate.int (4, 8)
        let partPitchInstrumentTrackCount () = context |> Generate.int (1, 4)
        let partPercussionInstrumentTrackCount () = context |> Generate.int (1, 4)

        let standardVelocity () = context |> Generate.float (0.875, 1.125)
        let standardDuration () = context |> Generate.float (0.8, 1.25)
        let standardPartCount () = context |> Generate.intByRank (halfProbabilityFunction 2, 1, maxSubPatternCount)

        let standardOffsets (min: int, max: int) : float list =
            context
            |> Generate.sequence (fun context -> context |> Generate.float (0.0, 1.0)) (context |> Generate.intByRank (halfProbabilityFunction 0, min, max))
            |> List.ofSeq

        let standardOctaveOffset () =
            context |> Generate.intByRank (halfProbabilityFunction 0, -2, 2)

        let standardArticulationOffset () = context |> Generate.normalFloat (0.0, 0.1)

        let scale : ScaleOffsets = [ 0; 2; 3; 5; 7; 8; 10 ]

        let tempo = context |> Generate.float (0.5, 1.0)

        let percussionInstruments : list<struct (PercussionInstrument * float)> =
            [
                ({ ArticulationCodes = [ 35uy; 36uy ] }, 1.0) // kick
                ({ ArticulationCodes = [ 37uy; 38uy; 40uy ] }, 1.0) // snare
                ({ ArticulationCodes = [ 42uy; 44uy; 46uy ] }, 1.0) // hi-hat
                ({
                     ArticulationCodes = [ 41uy; 43uy; 45uy; 47uy; 48uy; 50uy ]
                 },
                 0.2) // tom
                ({ ArticulationCodes = [ 51uy; 53uy; 59uy ] }, 0.2) // ride
                ({ ArticulationCodes = [ 49uy; 52uy; 55uy; 57uy ] }, 0.2) // cymbal
                ({ ArticulationCodes = [ 39uy ] }, 0.2) // clap
                ({ ArticulationCodes = [ 54uy ] }, 0.1) // tambourine
                ({ ArticulationCodes = [ 56uy ] }, 0.1) // cowbell
                ({ ArticulationCodes = [ 58uy ] }, 0.1) // vibraslap
                ({ ArticulationCodes = [ 60uy; 61uy ] }, 0.1) // bongo
                ({ ArticulationCodes = [ 62uy; 63uy; 64uy ] }, 0.1) // conga
                ({ ArticulationCodes = [ 65uy; 66uy ] }, 0.1) // timbale
                ({ ArticulationCodes = [ 67uy; 68uy ] }, 0.1) // agogo
                ({ ArticulationCodes = [ 69uy ] }, 0.1) // cabasa
                ({ ArticulationCodes = [ 70uy ] }, 0.1) // maracas
                ({ ArticulationCodes = [ 71uy; 72uy ] }, 0.1) // whistle
                ({ ArticulationCodes = [ 73uy; 74uy ] }, 0.1) // guiro
                ({ ArticulationCodes = [ 75uy ] }, 0.1) // claves
                ({ ArticulationCodes = [ 76uy; 77uy ] }, 0.1) // wood block
                ({ ArticulationCodes = [ 78uy; 79uy ] }, 0.1) // cuica
                ({ ArticulationCodes = [ 80uy; 81uy ] }, 0.1) // triangle
            ]

        let percussionInstrumentTracks =
            context
            |> Generate.subSequenceWeighted percussionInstruments percussionInstrumentTrackCount
            |> Seq.map
                (fun x ->
                    {
                        Instrument = x
                        EventState =
                            seq {
                                VelocityEvent(standardVelocity ())
                                ArticulationOffsetEvent(standardArticulationOffset ())
                            }
                            |> EventState.ofSeq
                    })
            |> List.ofSeq

        let pitchInstrumentTracks =
            context
            |> Generate.sequence
                (fun context ->
                    let minOctave =
                        context |> Generate.intByRank (halfProbabilityFunction 0, -3, -1)

                    let maxOctave =
                        minOctave
                        + (context |> Generate.intByRank (halfProbabilityFunction 0, 1, 0 - minOctave))

                    {
                        Instrument = { Code = byte (context |> Generate.int (0, 127)) }
                        MinOctaveOffset = minOctave
                        MaxOctaveOffset = maxOctave
                        EventState =
                            seq {
                                DurationEvent(standardDuration ())
                                VelocityEvent(standardVelocity ())
                                OctaveOffsetEvent(standardOctaveOffset ())
                                ChordNoteOffsetsEvent(standardOffsets (0, 2))
                            }
                            |> EventState.ofSeq
                    })
                pitchInstrumentTrackCount
            |> List.ofSeq

        let tracks =
            seq {
                yield! pitchInstrumentTracks |> Seq.map PitchInstrumentTrack
                yield! percussionInstrumentTracks |> Seq.map PercussionInstrumentTrack
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

        let generateNote context : EventState =
            seq {
                DurationEvent(
                    (context
                     |> Generate.rhythmValue (halfProbabilityFunction 0, 1.0, 2.0, 0.25, 2.0, 4))
                    / 4.0
                )

                VelocityEvent(
                    context
                    |> Generate.rhythmValue (threeQuarterProbabilityFunction 0, 0.5, 1.0, 0.75, 1.5, 5)
                )

                OctaveOffsetEvent(context |> Generate.intByRank (eighthProbabilityFunction 0, -1, 1))
                ChordNoteOffsetsEvent(standardOffsets (1, 1))
                ArticulationOffsetEvent(standardArticulationOffset ())
            }
            |> EventState.ofSeq

        let createPattern context : EventStatePattern =
            let duration : float = patternDuration

            let offset =
                context
                |> Generate.rhythmValue (halfProbabilityFunction 0, 0.0, 1.0, 0.0, 1.0, 4)

            let period : float =
                (context |> Generate.pickRhythmPeriod (quarterProbabilityFunction 0, 3))
                * (2.0
                   ** float (context |> Generate.intByRank (quarterProbabilityFunction 0, -2, 1)))

            let maxRank = 4

            let noteGenerator = fun context rank -> generateNote context

            let notes =
                context
                |> Generate.timeline noteGenerator (eighthProbabilityFunction 0, maxRank, offset, period, duration)
            let eventStateTimeline = EventStateTimelineMap.ofTimelines duration notes Timeline.empty

            EventStatePattern.ofTimelines duration eventStateTimeline Timeline.empty

        let commonPatterns =
            context |> Generate.sequence createPattern templateCount |> Seq.toArray

        let pitchInstrumentTrackPatterns =
            numberedPitchInstrumentTracks
            |> Seq.map
                (fun (trackNumber, _) ->
                    let patterns =
                        seq {
                            yield! context |> Generate.subSequence commonPatterns subTemplateCount

                            yield! context |> Generate.sequence createPattern subTemplateCount
                        }
                        |> Seq.toArray

                    (trackNumber, patterns))
            |> Map.ofSeq

        let percussionInstrumentTrackPatterns =
            numberedPercussionInstrumentTracks
            |> Seq.map
                (fun (trackNumber, _) ->
                    let patterns =
                        seq {
                            yield! context |> Generate.subSequence commonPatterns subTemplateCount

                            yield! context |> Generate.sequence createPattern subTemplateCount
                        }
                        |> Seq.toList

                    (trackNumber, patterns))
            |> Map.ofSeq

        let createHigherPattern (sourcePatterns: EventStatePattern seq) context : EventStatePattern =
            let duration = higherPatternDuration

            let limitPatternCount = standardPartCount()

            let limitedPatterns =
                context |> Generate.subSequence sourcePatterns limitPatternCount

            let noteOffset () : EventState =
                seq {
                    OctaveOffsetEvent(standardOctaveOffset ())
                    ChordScaleOffsetsEvent(standardOffsets (0, 1))
                    ChordNoteOffsetsEvent(standardOffsets (0, 1))
                    VelocityEvent(standardVelocity ())
                }
                |> EventState.ofSeq

            let innerPatterns =
                context
                |> Generate.sequentialTimeline
                    (fun context ->
                        let item = context |> Generate.item limitedPatterns
                        (struct (noteOffset (), item), item.Duration))
                    duration

            EventStatePattern.ofTimelines duration (EventStateTimelineMap.empty duration) innerPatterns

        let commonHigherPatterns =
            context
            |> Generate.sequence (createHigherPattern commonPatterns) templateCount
            |> Seq.toArray

        let pitchInstrumentTrackHigherPatterns =
            numberedPitchInstrumentTracks
            |> Seq.map
                (fun (trackNumber, _) ->
                    let patterns =
                        seq {
                            yield! context |> Generate.subSequence commonHigherPatterns subTemplateCount

                            yield!
                                context
                                |> Generate.sequence (createHigherPattern pitchInstrumentTrackPatterns.[trackNumber]) subTemplateCount
                        }
                        |> Seq.toArray

                    (trackNumber, patterns))
            |> Map.ofSeq

        let createPercussionPart context : TrackEventStatePattern =
            let duration = higherPatternDuration

            let partTrackCount = partPercussionInstrumentTrackCount ()

            let percussionTracks =
                context
                |> Generate.subSequence numberedPercussionInstrumentTracks partTrackCount

            let noteOffset () =
                seq { ArticulationOffsetEvent(standardArticulationOffset ()) }
                |> EventState.ofSeq

            let trackTimelines =
                percussionTracks
                |> Seq.map
                    (fun (trackNumber, _) ->
                        let limitPatternCount = standardPartCount()

                        let limitedPatterns =
                            context
                            |> Generate.subSequence percussionInstrumentTrackPatterns.[trackNumber] limitPatternCount

                        let innerPatterns =
                            context
                            |> Generate.sequentialTimeline
                                (fun context ->
                                    let item = context |> Generate.item limitedPatterns

                                    let pattern =
                                        EventStatePattern.ofTimelines
                                            item.Duration
                                            (EventStateTimelineMap.empty item.Duration)
                                            (struct (EventState.empty, item) |> Timeline.ofSingle)

                                    (struct (noteOffset (), pattern), item.Duration))
                                duration

                        (Some trackNumber, innerPatterns))
                |> Map.ofSeq

            TrackEventStatePattern.ofTimelineMaps duration trackTimelines Timeline.empty

        let percussionParts =
            context |> Generate.sequence createPercussionPart templateCount |> Seq.toArray

        let createPart context : TrackEventStatePattern =
            let duration = partDuration

            let partTrackCount = partPitchInstrumentTrackCount ()

            let pitchInstrumentTracks =
                context |> Generate.subSequence numberedPitchInstrumentTracks partTrackCount

            let noteOffset () =
                seq {
                    DurationEvent(standardDuration ())
                    OctaveOffsetEvent(standardOctaveOffset ())
                    ChordRootOffsetEvent(standardArticulationOffset ())
                    ChordScaleOffsetsEvent(standardOffsets (0, 1))
                    ChordNoteOffsetsEvent(standardOffsets (0, 1))
                    VelocityEvent(standardVelocity ())
                    ArticulationOffsetEvent(standardArticulationOffset ())
                }
                |> EventState.ofSeq

            let pitchInstrumentTrackTimelines =
                pitchInstrumentTracks
                |> Seq.map
                    (fun (pitchInstrumentTrackNumber, _) ->
                        let limitPatternCount = standardPartCount()

                        let limitedPatterns =
                            context
                            |> Generate.subSequence pitchInstrumentTrackHigherPatterns.[pitchInstrumentTrackNumber] limitPatternCount

                        let innerPatterns =
                            context
                            |> Generate.sequentialTimeline
                                (fun context ->
                                    let item = context |> Generate.item limitedPatterns
                                    (struct (noteOffset (), item), item.Duration))
                                duration

                        (Some pitchInstrumentTrackNumber, innerPatterns))


            let percussionPartTimeline =
                let limitPartCount = standardPartCount()

                let limitedParts =
                    context |> Generate.subSequence percussionParts limitPartCount

                context
                |> Generate.sequentialTimeline
                    (fun context ->
                        let item = context |> Generate.item limitedParts
                        (struct (EventState.empty, item), item.Duration))
                    duration

            let state =
                let generateState (eventGenerator: Generate.Context -> int -> Event) : Event Timeline =
                    let period : float =
                        (context |> Generate.pickRhythmPeriod (quarterProbabilityFunction 0, 3))
                        / (4.0 ** float (context |> Generate.intByRank (halfProbabilityFunction 1, 1, 4)))

                    let maxRank = 4

                    context
                    |> Generate.timeline eventGenerator (eighthProbabilityFunction 0, maxRank, 0.0, period, duration)

                let stateTimeline =
                    seq {
                        yield! generateState (fun _ _ -> ChordScaleOffsetsEvent(standardOffsets (1, 3)))
                        yield! generateState (fun _ _ -> ChordRootOffsetEvent(standardArticulationOffset ()))
                        yield! generateState (fun _ _ -> ArticulationOffsetEvent(standardArticulationOffset ()))
                    }
                    |> Timeline.ofSeq
                let eventStateTimeline =
                    EventStateTimelineMap.ofTimelines duration Timeline.empty stateTimeline

                EventStatePattern.ofTimelines duration eventStateTimeline Timeline.empty

            let trackTimelines =
                seq {
                    yield (None, struct (EventState.empty, state) |> Timeline.ofSingle)
                    yield! pitchInstrumentTrackTimelines
                }
                |> Map.ofSeq

            TrackEventStatePattern.ofTimelineMaps duration trackTimelines percussionPartTimeline

        let parts =
            context |> Generate.sequence createPart templateCount |> Seq.toArray

        let createHigherPart context : TrackEventStatePattern =
            let duration = higherPartDuration

            let noteOffset () =
                seq {
                    DurationEvent(standardDuration ())

                    KeyOffsetEvent(
                        if context |> Generate.test 0.25 then
                            context |> Generate.int (-6, 6)
                        else
                            0
                    )

                    OctaveOffsetEvent(standardOctaveOffset ())
                    ChordRootOffsetEvent(standardArticulationOffset ())
                    ChordScaleOffsetsEvent(standardOffsets (0, 1))
                    ChordNoteOffsetsEvent(standardOffsets (0, 1))
                    VelocityEvent(standardVelocity ())
                    ArticulationOffsetEvent(standardArticulationOffset ())
                }
                |> EventState.ofSeq

            let limitPartCount = standardPartCount()

            let limitedParts = context |> Generate.subSequence parts limitPartCount

            let innerParts =
                context
                |> Generate.sequentialTimeline
                    (fun context ->
                        let item = context |> Generate.item limitedParts
                        (struct (noteOffset (), item), item.Duration))
                    duration

            TrackEventStatePattern.ofTimelineMaps duration Map.empty innerParts

        let noteOffset () =
            seq {
                DurationEvent(standardDuration ())
                KeyOffsetEvent(context |> Generate.int (-6, 6))
                OctaveOffsetEvent(standardOctaveOffset ())
                ChordRootOffsetEvent(standardArticulationOffset ())
                ChordScaleOffsetsEvent(standardOffsets (0, 1))
                ChordNoteOffsetsEvent(standardOffsets (0, 1))
                VelocityEvent(standardVelocity ())
                ArticulationOffsetEvent(standardArticulationOffset ())
            }
            |> EventState.ofSeq

        let songTimeline : TrackEventStatePattern WithEventState Timeline =
            context
            |> Generate.sequentialTimeline
                (fun context ->
                    let item = createHigherPart context
                    ((noteOffset (), item), item.Duration))
                songDuration

        let songNoteOffsets =
            seq {
                DurationEvent(context |> Generate.float (0.5, 2.0))
                KeyOffsetEvent(context |> Generate.int (-6, 6))
                ScaleOffsetsEvent(scale)
                TempoEvent(tempo)
            }
            |> EventState.ofSeq

        let trackMap = tracks |> List.indexed |> Map.ofSeq

        EventStateSongPattern.ofTimelines songDuration trackMap songTimeline songNoteOffsets
