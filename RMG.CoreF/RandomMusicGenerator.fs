namespace RMG.CoreF

open RMG.CoreF.Composition

module RandomMusicGenerator =
    let generate (): SongPatternMap =
        let halfProbabilityFunction =
            Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.5, 0)

        let threeQuarterProbabilityFunction =
            Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.75, 0)

        let quarterProbabilityFunction =
            Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.25, 0)

        let eighthProbabilityFunction =
            Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.125, 0)

        let scaleOffsetProbabilityFunction = Generate.normalFloat (0.0, 0.1)

        let context = Generate.Context()
        let duration = 256.0
        let templateCount = 16
        let subTemplateCount = templateCount / 2

        let standardVelocity() = context |> Generate.float (0.875, 1.125)
        let standardDuration() = context |> Generate.float (0.8, 1.25)
        let standardOctaveOffset() = context |> Generate.intByRank (halfProbabilityFunction, -2, 2)

        let scale : Scale =
            { KeyOffsets = [ 0; 2; 3; 5; 7; 8; 10 ] }

        let tempo = context |> Generate.float (0.25, 1.0)

        let percussionInstrumentTrack =
            { Instruments = [
                { ArticulationCodes = [ 35uy; 36uy ] }
                { ArticulationCodes = [ 37uy; 38uy; 39uy; 40uy ] }
                { ArticulationCodes = [ 42uy; 44uy; 46uy ] } ]
              NoteOffset =
                  { Duration = 1.0
                    KeyOffset = 0
                    OctaveOffset = 0
                    ScaleOffset = 0.0
                    Velocity = standardVelocity() } }

        let pitchInstrumentTrackCount = context |> Generate.int (4, 8)

        let pitchInstrumentTracks =
            context
            |> Generate.sequence
                (fun context ->
                    let minOctave =
                        context
                        |> Generate.intByRank (halfProbabilityFunction, -2, 0)

                    let maxOctave =
                        minOctave
                        + (context
                           |> Generate.intByRank (halfProbabilityFunction, 2, 2 - minOctave))

                    { Instrument = { Code = byte (context |> Generate.int (0, 127)) }
                      MinOctaveOffset = minOctave
                      MaxOctaveOffset = maxOctave
                      NoteOffset =
                          { Duration = standardDuration()
                            KeyOffset = 0
                            OctaveOffset = standardOctaveOffset()
                            ScaleOffset = 0.0
                            Velocity = standardVelocity() } })
                pitchInstrumentTrackCount
            |> List.ofSeq

        let pitchInstrumentTrackIndex i = i + 1
        let percussionInstrumentTrackIndex  = 0

        let generateNote context =
            { Duration =
                  (context
                   |> Generate.rhythmValue (halfProbabilityFunction, 1.0, 2.0, 0.25, 2.0, 4))
                  / 4.0
              Velocity =
                  context
                  |> Generate.rhythmValue (threeQuarterProbabilityFunction, 0.5, 1.0, 0.75, 1.5, 5)
              OctaveOffset =
                  context
                  |> Generate.intByRank (eighthProbabilityFunction, -1, 1)
              ScaleOffset = float (context |> scaleOffsetProbabilityFunction)
              KeyOffset = 0 }

        let createPattern context =
            let duration : float =
                //2.0 ** (float (context |> Generate.int (0, 2)))
                1.0

            let offset =
                context
                |> Generate.rhythmValue (halfProbabilityFunction, 2.0, 4.0, 0.0, 4.0, 4)

            let period : float =
                (context
                 |> Generate.rhythmPeriod (quarterProbabilityFunction, 8))
                / (2.0
                   ** float (
                       context
                       |> Generate.intByRank (quarterProbabilityFunction, -1, 2)
                   ))

            let maxRank = 4

            let noteGenerator = fun context rank -> generateNote context

            let notes =
                context
                |> Generate.timeline noteGenerator (eighthProbabilityFunction, maxRank, offset, period, duration)

            CompositeEventPattern(duration, notes, Seq.empty, NoteOffset.merge)

        let commonPatterns =
            context
            |> Generate.sequence createPattern templateCount
            |> Seq.toArray

        let pitchInstrumentTrackPatterns =
            pitchInstrumentTracks
            |> Seq.map
                (fun _ ->
                    seq {
                        yield!
                            context
                            |> Generate.subSequence commonPatterns subTemplateCount

                        yield!
                            context
                            |> Generate.sequence createPattern subTemplateCount
                    }
                    |> Seq.toArray)
            |> Seq.indexed
            |> Map.ofSeq

        let percussionInstrumentTrackPatterns =
            percussionInstrumentTrack.Instruments
            |> Seq.map
                (fun _ ->
                    seq {
                        yield!
                            context
                            |> Generate.subSequence commonPatterns subTemplateCount

                        yield!
                            context
                            |> Generate.sequence createPattern subTemplateCount
                    }
                    |> Seq.toArray)
            |> Seq.indexed
            |> Map.ofSeq

        let createHigherPattern (sourcePatterns: seq<CompositeEventPattern<NoteOffset>>) context =
            let duration =
                //2.0 ** float (context |> Generate.int (2, 4))
                4.0

            let limitPatternCount = context |> Generate.int (1, 4)

            let generateNoteOffset () : NoteOffset =
                { Duration = 1.0
                  KeyOffset = 0
                  OctaveOffset = standardOctaveOffset()
                  ScaleOffset = context |> scaleOffsetProbabilityFunction
                  Velocity = standardVelocity() }

            let limitedPatterns =
                context
                |> Generate.subSequence sourcePatterns limitPatternCount

            let innerPatterns =
                context
                |> Generate.sequentialTimeline
                    (fun context ->
                        let item = context |> Generate.item limitedPatterns
                        (item, item.Duration))
                    duration
                |> Timeline.map (fun x -> (x, generateNoteOffset ()))

            { CompositePatternTimelineInput = innerPatterns
              TimelineInput = Seq.empty }
            |> CompositeEventPattern.fromInput duration NoteOffset.merge

        let commonHigherPatterns =
            context
            |> Generate.sequence (createHigherPattern commonPatterns) templateCount
            |> Seq.toArray

        let pitchInstrumentTrackHigherPatterns =
            pitchInstrumentTracks
            |> Seq.indexed
            |> Seq.map
                (fun (index, _) ->
                    let patterns =
                        seq {
                            yield!
                                context
                                |> Generate.subSequence commonHigherPatterns subTemplateCount

                            yield!
                                context
                                |> Generate.sequence
                                    (createHigherPattern pitchInstrumentTrackPatterns.[index])
                                    subTemplateCount
                        }
                        |> Seq.toArray

                    (index, patterns))
            |> Map.ofSeq

        let createPercussionPart context =
            let duration =
                //2.0 ** float (context |> Generate.int (2, 4))
                4.0

            let percussionTrackNumbers =
                context
                |> Generate.subSequence
                    (seq { 0 .. percussionInstrumentTrack.Instruments.Length - 1 })
                    percussionInstrumentTrack.Instruments.Length

            let generateNoteOffset () : NoteOffset =
                { Duration = 1.0
                  KeyOffset = 0
                  OctaveOffset = 0
                  ScaleOffset = context |> scaleOffsetProbabilityFunction
                  Velocity = 1.0 }

            let trackTimelines =
                percussionTrackNumbers
                |> Seq.map
                    (fun trackNumber ->
                        let limitPatternCount = context |> Generate.int (1, 4)

                        let limitedPatterns =
                            context
                            |> Generate.subSequence percussionInstrumentTrackPatterns.[trackNumber] limitPatternCount

                        let innerPatterns =
                            context
                            |> Generate.sequentialTimeline
                                (fun context ->
                                    let item = context |> Generate.item limitedPatterns

                                    let pattern =
                                        { CompositePatternTimelineInput =
                                              (item, NoteOffset.empty) |> Timeline.fromSingle
                                          TimelineInput = Seq.empty }
                                        |> CompositeEventPattern.fromInput item.Duration NoteOffset.merge

                                    (pattern, item.Duration))
                                duration
                            |> Timeline.map (fun x -> (x, generateNoteOffset ()))

                        (trackNumber, innerPatterns))

            { CompositeTrackPatternTimelineMap = seq {(percussionInstrumentTrackIndex, trackTimelines)}
              CompositeTrackPatternMapTimeline = Seq.empty }
            |> CompositeTrackEventPattern.fromInput duration NoteOffset.merge

        let percussionParts =
            context
            |> Generate.sequence createPercussionPart templateCount
            |> Seq.toArray

        let createPart context =
            let duration =
                //2.0 ** float (context |> Generate.int (4, 6))
                16.0

            let partTrackCount = context |> Generate.intByRank (threeQuarterProbabilityFunction, 1, 3)

            let generateNoteOffset () : NoteOffset =
                { Duration = standardDuration()
                  KeyOffset = 0
                  OctaveOffset = standardOctaveOffset()
                  ScaleOffset = context |> scaleOffsetProbabilityFunction
                  Velocity = standardVelocity() }

            let pitchInstrumentTrackNumbers =
                context
                |> Generate.subSequence (seq { 0 .. pitchInstrumentTrackCount - 1 }) partTrackCount

            let pitchInstrumentTrackTimelines =
                pitchInstrumentTrackNumbers
                |> Seq.map
                    (fun pitchInstrumentTrackNumber ->
                        let limitPatternCount = context |> Generate.int (2, 8)

                        let limitedPatterns =
                            context
                            |> Generate.subSequence
                                pitchInstrumentTrackHigherPatterns.[pitchInstrumentTrackNumber]
                                limitPatternCount

                        let innerPatterns =
                            context
                            |> Generate.sequentialTimeline
                                (fun context ->
                                    let item = context |> Generate.item limitedPatterns
                                    (item, item.Duration))
                                duration
                            |> Timeline.map (fun x -> (x, generateNoteOffset ()))

                        (pitchInstrumentTrackIndex pitchInstrumentTrackNumber, seq {(0, innerPatterns)}))


            let percussionPartTimeline =
                let limitPartCount = context |> Generate.int (2, 8)

                let limitedParts =
                    context
                    |> Generate.subSequence percussionParts limitPartCount

                context
                |> Generate.sequentialTimeline
                    (fun context ->
                        let item = context |> Generate.item limitedParts
                        (item, item.Duration))
                    duration
                |> Timeline.map (fun x -> (x, generateNoteOffset ()))

            { CompositeTrackPatternTimelineMap = pitchInstrumentTrackTimelines
              CompositeTrackPatternMapTimeline = percussionPartTimeline }
            |> CompositeTrackEventPattern.fromInput duration NoteOffset.merge

        let parts =
            context
            |> Generate.sequence createPart templateCount
            |> Seq.toArray

        let createHigherPart context =
            let duration =
                //2.0 ** float (context |> Generate.int (6, 8))
                64.0

            let limitPartCount = context |> Generate.int (1, 4)

            let generateNoteOffset () : NoteOffset =
                { Duration = standardDuration()
                  KeyOffset =
                      if Probability.test (context.GetProbability()) 0.75 then
                          context |> Generate.int (-6, 6)
                      else
                          0
                  OctaveOffset = standardOctaveOffset()
                  ScaleOffset = context |> scaleOffsetProbabilityFunction
                  Velocity = standardVelocity() }

            let limitedParts =
                context
                |> Generate.subSequence parts limitPartCount

            let innerParts =
                context
                |> Generate.sequentialTimeline
                    (fun context ->
                        let item = context |> Generate.item limitedParts
                        (item, item.Duration))
                    duration
                |> Timeline.map (fun x -> (x, generateNoteOffset ()))

            { CompositeTrackPatternTimelineMap = Seq.empty
              CompositeTrackPatternMapTimeline = innerParts }
            |> CompositeTrackEventPattern.fromInput duration NoteOffset.merge

        let generateNoteOffset () : NoteOffset =
            { Duration = standardDuration()
              KeyOffset = context |> Generate.int (-6, 6)
              OctaveOffset = standardOctaveOffset()
              ScaleOffset = context |> scaleOffsetProbabilityFunction
              Velocity = standardVelocity() }

        let songTimeline =
            context
            |> Generate.sequentialTimeline
                (fun context ->
                    let item = createHigherPart context
                    (item, item.Duration))
                (duration)
            |> Timeline.map (fun x -> (x, generateNoteOffset ()))

        let toStatePatternTimeline value =
            { TimelineInput = value |> Timeline.fromSingle
              PatternTimelineInput = Seq.empty }
            |> StatePattern.fromInput duration StateMerger.multiplicativeFloat
            |> Timeline.fromSingle

        let toEventPatternTimeline value =
            { EventPatternInput.TimelineInput = value |> Timeline.fromSingle
              EventPatternInput.PatternTimelineInput = Seq.empty }
            |> EventPattern.fromInput duration
            |> Timeline.fromSingle

        let tempoPatternTimeline = toStatePatternTimeline tempo
        let scalePatternTimeline = toEventPatternTimeline scale

        let tracks =
            seq<InstrumentTrack> {
                yield PercussionInstrumentTrack percussionInstrumentTrack
                yield!
                    pitchInstrumentTracks
                    |> Seq.map PitchInstrumentTrack
            }

        let noteOffset : NoteOffset =
            { Duration = context |> Generate.float (0.5, 2.0)
              KeyOffset = context |> Generate.int (-6, 6)
              OctaveOffset = 0
              ScaleOffset = 0.0
              Velocity = 1.0 }

        SongPatternMap(duration, tracks, songTimeline, noteOffset, tempoPatternTimeline, scalePatternTimeline)
