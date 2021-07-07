namespace RMG.CoreF

open RMG.CoreF.Composition

module RandomMusicGenerator =
    let generate () : SongPatternMap =
        let halfProbabilityFunction = Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.5, 0)

        let threeQuarterProbabilityFunction = Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.75, 0)

        let quarterProbabilityFunction = Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.25, 0)

        let eighthProbabilityFunction = Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.125, 0)

        let scaleOffsetProbabilityFunction = Generate.normalFloat (0.0, 0.1)

        let context = Generate.Context()
        let patternDuration = 1.0
        let higherPatternDuration = 4.0
        let partDuration = 16.0
        let higherPartDuration = 64.0
        let songDuration = 256.0
        let templateCount = 16
        let subTemplateCount = templateCount / 2
        let pitchInstrumentTrackCount = context |> Generate.int (4, 8)
        let percussionInstrumentTrackCount = context |> Generate.int (4, 8)
        let partPitchInstrumentTrackCount() = context |> Generate.int (1, 4)
        let partPercussionInstrumentTrackCount() = context |> Generate.int (1, 4)

        let standardVelocity () = context |> Generate.float (0.875, 1.125)
        let standardDuration () = context |> Generate.float (0.8, 1.25)
        let standardOctaveOffset () = context |> Generate.intByRank (halfProbabilityFunction, -2, 2)

        let scale : Scale = { KeyOffsets = [ 0; 2; 3; 5; 7; 8; 10 ] }

        let tempo = context |> Generate.float (0.5, 1.0)

        let percussionInstruments : list<PercussionInstrument * float> =
            [
                ({ ArticulationCodes = [ 35uy; 36uy ] }, 1.0) // kick
                ({ ArticulationCodes = [ 37uy; 38uy; 40uy ] }, 1.0) // snare
                ({ ArticulationCodes = [ 42uy; 44uy; 46uy ] }, 1.0) // hi-hat
                ({ ArticulationCodes = [ 41uy; 43uy; 45uy; 47uy; 48uy; 50uy ] }, 0.2) // tom
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
                        NoteOffset =
                            {
                                Duration = 1.0
                                KeyOffset = 0
                                OctaveOffset = 0
                                ScaleOffset = 0.0
                                Velocity = standardVelocity ()
                            }
                    })
                |> List.ofSeq

        let pitchInstrumentTracks =
            context
            |> Generate.sequence
                (fun context ->
                    let minOctave = context |> Generate.intByRank (halfProbabilityFunction, -2, 0)

                    let maxOctave =
                        minOctave + (context |> Generate.intByRank (halfProbabilityFunction, 1, 1 - minOctave))

                    {
                        Instrument = { Code = byte (context |> Generate.int (0, 127)) }
                        MinOctaveOffset = minOctave
                        MaxOctaveOffset = maxOctave
                        NoteOffset =
                            {
                                Duration = standardDuration ()
                                KeyOffset = 0
                                OctaveOffset = standardOctaveOffset ()
                                ScaleOffset = 0.0
                                Velocity = standardVelocity ()
                            }
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

        let generateNote context =
            {
                Duration = (context |> Generate.rhythmValue (halfProbabilityFunction, 1.0, 2.0, 0.25, 2.0, 4)) / 4.0
                Velocity = context |> Generate.rhythmValue (threeQuarterProbabilityFunction, 0.5, 1.0, 0.75, 1.5, 5)
                OctaveOffset = context |> Generate.intByRank (eighthProbabilityFunction, -1, 1)
                ScaleOffset = float (context |> scaleOffsetProbabilityFunction)
                KeyOffset = 0
            }

        let createPattern context =
            let duration : float = patternDuration

            let offset = context |> Generate.rhythmValue (halfProbabilityFunction, 2.0, 4.0, 0.0, 4.0, 4)

            let period : float =
                (context |> Generate.rhythmPeriod (quarterProbabilityFunction, 8))
                / (2.0 ** float (context |> Generate.intByRank (quarterProbabilityFunction, -1, 2)))

            let maxRank = 4

            let noteGenerator = fun context rank -> generateNote context

            let notes =
                context
                |> Generate.timeline noteGenerator (eighthProbabilityFunction, maxRank, offset, period, duration)

            CompositeEventPattern(duration, notes, Seq.empty, NoteOffset.merge)

        let commonPatterns = context |> Generate.sequence createPattern templateCount |> Seq.toArray

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

        let createHigherPattern (sourcePatterns: seq<CompositeEventPattern<NoteOffset>>) context =
            let duration = higherPatternDuration

            let limitPatternCount = context |> Generate.int (1, 4)

            let generateNoteOffset () : NoteOffset =
                {
                    Duration = 1.0
                    KeyOffset = 0
                    OctaveOffset = standardOctaveOffset ()
                    ScaleOffset = context |> scaleOffsetProbabilityFunction
                    Velocity = standardVelocity ()
                }

            let limitedPatterns = context |> Generate.subSequence sourcePatterns limitPatternCount

            let innerPatterns =
                context
                |> Generate.sequentialTimeline
                    (fun context ->
                        let item = context |> Generate.item limitedPatterns
                        (item, item.Duration))
                    duration
                |> Timeline.map (fun x -> (x, generateNoteOffset ()))

            { CompositePatternTimelineInput = innerPatterns; TimelineInput = Seq.empty }
            |> CompositeEventPattern.fromInput duration NoteOffset.merge

        let commonHigherPatterns =
            context |> Generate.sequence (createHigherPattern commonPatterns) templateCount |> Seq.toArray

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

        let createPercussionPart context =
            let duration = higherPatternDuration

            let partTrackCount = partPercussionInstrumentTrackCount()

            let percussionTracks =
                context
                |> Generate.subSequence numberedPercussionInstrumentTracks partTrackCount

            let generateNoteOffset () : NoteOffset =
                {
                    Duration = 1.0
                    KeyOffset = 0
                    OctaveOffset = 0
                    ScaleOffset = context |> scaleOffsetProbabilityFunction
                    Velocity = 1.0
                }

            let trackTimelines =
                percussionTracks
                |> Seq.map
                    (fun (trackNumber, _) ->
                        let limitPatternCount = context |> Generate.int (1, 4)

                        let limitedPatterns =
                            context |> Generate.subSequence percussionInstrumentTrackPatterns.[trackNumber] limitPatternCount

                        let innerPatterns =
                            context
                            |> Generate.sequentialTimeline
                                (fun context ->
                                    let item = context |> Generate.item limitedPatterns

                                    let pattern =
                                        {
                                            CompositePatternTimelineInput = (item, NoteOffset.empty) |> Timeline.fromSingle
                                            TimelineInput = Seq.empty
                                        }
                                        |> CompositeEventPattern.fromInput item.Duration NoteOffset.merge

                                    (pattern, item.Duration))
                                duration
                            |> Timeline.map (fun x -> (x, generateNoteOffset ()))

                        (trackNumber, innerPatterns))

            {
                CompositeTrackPatternTimelineMap = trackTimelines
                CompositeTrackPatternMapTimeline = Seq.empty
            }
            |> CompositeTrackEventPattern.fromInput duration NoteOffset.merge

        let percussionParts = context |> Generate.sequence createPercussionPart templateCount |> Seq.toArray

        let createPart context =
            let duration = partDuration

            let partTrackCount = partPitchInstrumentTrackCount()

            let generateNoteOffset () : NoteOffset =
                {
                    Duration = standardDuration ()
                    KeyOffset = 0
                    OctaveOffset = standardOctaveOffset ()
                    ScaleOffset = context |> scaleOffsetProbabilityFunction
                    Velocity = standardVelocity ()
                }

            let pitchInstrumentTracks = context |> Generate.subSequence numberedPitchInstrumentTracks partTrackCount

            let pitchInstrumentTrackTimelines =
                pitchInstrumentTracks
                |> Seq.map
                    (fun (pitchInstrumentTrackNumber, _) ->
                        let limitPatternCount = context |> Generate.int (2, 8)

                        let limitedPatterns =
                            context
                            |> Generate.subSequence pitchInstrumentTrackHigherPatterns.[pitchInstrumentTrackNumber] limitPatternCount

                        let innerPatterns =
                            context
                            |> Generate.sequentialTimeline
                                (fun context ->
                                    let item = context |> Generate.item limitedPatterns
                                    (item, item.Duration))
                                duration
                            |> Timeline.map (fun x -> (x, generateNoteOffset ()))

                        (pitchInstrumentTrackNumber, innerPatterns))


            let percussionPartTimeline =
                let limitPartCount = context |> Generate.int (2, 8)

                let limitedParts = context |> Generate.subSequence percussionParts limitPartCount

                context
                |> Generate.sequentialTimeline
                    (fun context ->
                        let item = context |> Generate.item limitedParts
                        (item, item.Duration))
                    duration
                |> Timeline.map (fun x -> (x, generateNoteOffset ()))

            {
                CompositeTrackPatternTimelineMap = pitchInstrumentTrackTimelines
                CompositeTrackPatternMapTimeline = percussionPartTimeline
            }
            |> CompositeTrackEventPattern.fromInput duration NoteOffset.merge

        let parts = context |> Generate.sequence createPart templateCount |> Seq.toArray

        let createHigherPart context =
            let duration = higherPartDuration

            let limitPartCount = context |> Generate.int (1, 4)

            let generateNoteOffset () : NoteOffset =
                {
                    Duration = standardDuration ()
                    KeyOffset = if context |> Generate.test 0.75 then context |> Generate.int (-6, 6) else 0
                    OctaveOffset = standardOctaveOffset ()
                    ScaleOffset = context |> scaleOffsetProbabilityFunction
                    Velocity = standardVelocity ()
                }

            let limitedParts = context |> Generate.subSequence parts limitPartCount

            let innerParts =
                context
                |> Generate.sequentialTimeline
                    (fun context ->
                        let item = context |> Generate.item limitedParts
                        (item, item.Duration))
                    duration
                |> Timeline.map (fun x -> (x, generateNoteOffset ()))

            {
                CompositeTrackPatternTimelineMap = Seq.empty
                CompositeTrackPatternMapTimeline = innerParts
            }
            |> CompositeTrackEventPattern.fromInput duration NoteOffset.merge

        let generateNoteOffset () : NoteOffset =
            {
                Duration = standardDuration ()
                KeyOffset = context |> Generate.int (-6, 6)
                OctaveOffset = standardOctaveOffset ()
                ScaleOffset = context |> scaleOffsetProbabilityFunction
                Velocity = standardVelocity ()
            }

        let songTimeline =
            context
            |> Generate.sequentialTimeline
                (fun context ->
                    let item = createHigherPart context
                    (item, item.Duration))
                (songDuration)
            |> Timeline.map (fun x -> (x, generateNoteOffset ()))

        let toStatePatternTimeline value =
            {
                TimelineInput = value |> Timeline.fromSingle
                PatternTimelineInput = Seq.empty
            }
            |> StatePattern.fromInput songDuration StateMerger.multiplicativeFloat
            |> Timeline.fromSingle

        let toEventPatternTimeline value =
            {
                EventPatternInput.TimelineInput = value |> Timeline.fromSingle
                EventPatternInput.PatternTimelineInput = Seq.empty
            }
            |> EventPattern.fromInput songDuration
            |> Timeline.fromSingle

        let tempoPatternTimeline = toStatePatternTimeline tempo
        let scalePatternTimeline = toEventPatternTimeline scale

        let noteOffset : NoteOffset =
            {
                Duration = context |> Generate.float (0.5, 2.0)
                KeyOffset = context |> Generate.int (-6, 6)
                OctaveOffset = 0
                ScaleOffset = 0.0
                Velocity = 1.0
            }

        SongPatternMap(songDuration, tracks, songTimeline, noteOffset, tempoPatternTimeline, scalePatternTimeline)
