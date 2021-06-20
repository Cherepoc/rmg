namespace RMG.CoreF

open RMG.CoreF.Composition

module RandomMusicGenerator =
    let generate () =
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
        let duration = 512.0
        let templateCount = 16
        let subTemplateCount = templateCount / 2

        let scale: Scale =
            { KeyOffsets = [ 0; 2; 3; 5; 7; 8; 10 ] }

        let tempo = context |> Generate.float (0.75, 1.25)

        let percussionInstrumentTracks =
            [
                {
                    Instrument = {ArticulationCodes = [35uy; 36uy]}
                    NoteOffset =
                      { Duration = 1.0
                        KeyOffset = 0
                        OctaveOffset = 0
                        ScaleOffset = 0.0
                        Volume = context |> Generate.float (0.75, 1.5) }
                };
                {
                    Instrument = {ArticulationCodes = [37uy; 38uy; 39uy; 40uy]}
                    NoteOffset =
                      { Duration = 1.0
                        KeyOffset = 0
                        OctaveOffset = 0
                        ScaleOffset = 0.0
                        Volume = context |> Generate.float (0.75, 1.5) }
                };
                {
                    Instrument = {ArticulationCodes = [42uy; 44uy; 46uy]}
                    NoteOffset =
                      { Duration = 1.0
                        KeyOffset = 0
                        OctaveOffset = 0
                        ScaleOffset = 0.0
                        Volume = context |> Generate.float (0.75, 1.5) }
                }
            ]

        let percussionInstrumentTrackCount = percussionInstrumentTracks.Length

        let pitchInstrumentTrackCount = context |> Generate.int (4, 8)

        let pitchInstrumentTracks =
            context
            |> Generate.sequence (fun context ->
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
                      { Duration = context |> Generate.float (0.5, 2.0)
                        KeyOffset = 0
                        OctaveOffset = context |> Generate.int (-2, 2)
                        ScaleOffset = 0.0
                        Volume = context |> Generate.float (0.75, 1.5) } }) pitchInstrumentTrackCount
            |> List.ofSeq

        let pitchInstrumentTrackIndex i = i
        let percussionInstrumentTrackIndex i = pitchInstrumentTrackCount + i

        let createPattern context =
            let duration: float =
                2.0 ** (float (context |> Generate.int (0, 2)))

            let offset =
                context
                |> Generate.rhythmValue (halfProbabilityFunction, 2.0, 4.0, 0.0, 4.0, 4)

            let period: float =
                (context
                 |> Generate.rhythmPeriod (halfProbabilityFunction, 8))
                / (2.0
                   ** float
                       (context
                        |> Generate.intByRank (quarterProbabilityFunction, -1, 2)))

            let maxRank = 4

            let noteGenerator =
                fun context rank ->
                    { Duration =
                          (context
                           |> Generate.rhythmValue (halfProbabilityFunction, 1.0, 2.0, 0.0, 2.0, 4))
                          / 4.0
                      Volume =
                          context
                          |> Generate.rhythmValue (threeQuarterProbabilityFunction, 0.5, 1.0, 0.75, 1.5, 5)
                      OctaveOffset =
                          context
                          |> Generate.intByRank (eighthProbabilityFunction, -1, 1)
                      ScaleOffset =
                          float (context |> scaleOffsetProbabilityFunction)
                      KeyOffset = 0 }

            let notes =
                context
                |> Generate.timeline noteGenerator (quarterProbabilityFunction, maxRank, offset, period, duration)

            EventPattern(duration, notes, Seq.empty)

        let commonPatterns =
            context
            |> Generate.sequence createPattern templateCount
            |> Seq.toArray

        let pitchInstrumentTrackPatterns =
            pitchInstrumentTracks
            |> Seq.map (fun _ ->
                seq {
                    yield! context
                           |> Generate.subSequence commonPatterns subTemplateCount
                    yield! context
                           |> Generate.sequence createPattern subTemplateCount
                }
                |> Seq.toArray)
            |> Seq.indexed
            |> Map.ofSeq

        let percussionInstrumentTrackPatterns =
            percussionInstrumentTracks
            |> Seq.map (fun _ ->
                seq {
                    yield! context
                           |> Generate.subSequence commonPatterns subTemplateCount
                    yield! context
                           |> Generate.sequence createPattern subTemplateCount
                }
                |> Seq.toArray)
            |> Seq.indexed
            |> Map.ofSeq

        let createHigherPattern (sourcePatterns: seq<EventPattern<NoteOffset>>) context =
            let duration =
                2.0 ** float (context |> Generate.int (2, 4))

            let limitPatternCount = context |> Generate.int (1, 4)

            let limitedPatterns =
                context
                |> Generate.subSequence sourcePatterns limitPatternCount

            let innerPatterns =
                context
                |> Generate.sequentialTimeline (fun context ->
                    let item = context |> Generate.item limitedPatterns
                    (item, item.Duration)) duration

            { PatternTimelineInput = innerPatterns
              NoteOffsetStatePatternMapPatternTimelineInput = Seq.empty
              NoteOffsetBasedPatternTimelineInput = Seq.empty }
            |> NoteOffsetStateBasedEventPattern.fromInput duration

        let commonHigherPatterns =
            context
            |> Generate.sequence (createHigherPattern commonPatterns) templateCount
            |> Seq.toArray

        let pitchInstrumentTrackHigherPatterns =
            pitchInstrumentTracks
            |> Seq.indexed
            |> Seq.map (fun (index, _) ->
                let patterns =
                    seq {
                        yield! context
                               |> Generate.subSequence commonHigherPatterns subTemplateCount
                        yield! context
                               |> Generate.sequence (createHigherPattern pitchInstrumentTrackPatterns.[index]) subTemplateCount
                    }
                    |> Seq.toArray

                (index, patterns))
            |> Map.ofSeq

        let createPercussionPart context =
            let duration = 2.0 ** float (context |> Generate.int (2, 4))
            let partTrackCount = context |> Generate.int (1, 4)
            let percussionTrackNumbers =
                context
                |> Generate.subSequence (seq { 0 .. percussionInstrumentTracks.Length - 1 }) percussionInstrumentTracks.Length
            let trackTimelines =
                percussionTrackNumbers
                |> Seq.map (fun trackNumber ->
                    let limitPatternCount = context |> Generate.int (1, 4)
                    let limitedPatterns =
                        context
                        |> Generate.subSequence percussionInstrumentTrackPatterns.[trackNumber] limitPatternCount

                    let innerPatterns =
                        context
                        |> Generate.sequentialTimeline (fun context ->
                            let item = context |> Generate.item limitedPatterns
                            let pattern =
                                { PatternTimelineInput = [{ Position = 0.0; Value = item }]
                                  NoteOffsetStatePatternMapPatternTimelineInput = Seq.empty
                                  NoteOffsetBasedPatternTimelineInput = Seq.empty }
                                |> NoteOffsetStateBasedEventPattern.fromInput item.Duration
                            (pattern, item.Duration)) duration

                    (percussionInstrumentTrackIndex trackNumber, innerPatterns))

            { TrackPatternTimelineMapInput = trackTimelines
              NoteOffsetStatePatternMapPatternTimelineInput = []
              TrackNoteOffsetStateBasedEventPatternMapTimelineInput = Seq.empty }
            |> TrackNoteOffsetStateBasedEventPatternMap.fromInput duration

        let percussionParts =
            context
            |> Generate.sequence createPercussionPart templateCount
            |> Seq.toArray

        let partNoteOffsetState (period, duration) =
            let generateFloatNoteOffsetTimeline (itemFunction: Generate.Context -> int -> float): Timeline<float> =
                let probabilityFunction =
                    Probability.geometricIntProbabilityFunction (0.0, 0.5, 0.5, 0)

                context
                |> Generate.timeline itemFunction (probabilityFunction, 2, 0.0, period, duration)

            let generateIntNoteOffsetTimeline (itemFunction: Generate.Context -> int -> int): Timeline<int> =
                let probabilityFunction =
                    Probability.geometricIntProbabilityFunction (0.0, 0.5, 0.5, 0)

                context
                |> Generate.timeline itemFunction (probabilityFunction, 2, 0.0, period, duration)

            let noteOffsetState =
                { DurationPatternInput =
                      { TimelineInput =
                            generateFloatNoteOffsetTimeline (fun context rank -> context |> Generate.float (0.5, 2.0))
                        PatternTimelineInput = Seq.empty }
                  KeyOffsetPatternInput =
                      { TimelineInput =
                            generateIntNoteOffsetTimeline (fun context rank -> context |> Generate.int (-6, 6))
                        PatternTimelineInput = Seq.empty }
                  OctaveOffsetPatternInput =
                      { TimelineInput =
                            generateIntNoteOffsetTimeline (fun context rank ->
                                context
                                |> Generate.intByRank (halfProbabilityFunction, -2, 2))
                        PatternTimelineInput = Seq.empty }
                  ScaleOffsetPatternInput =
                      { TimelineInput =
                            generateFloatNoteOffsetTimeline (fun context rank ->
                                context |> scaleOffsetProbabilityFunction)
                        PatternTimelineInput = Seq.empty }
                  VolumePatternInput =
                      { TimelineInput = Seq.empty
                        //generateFloatNoteOffsetTimeline (fun context rank -> context |> Generate.float (0.75, 1.5))
                        PatternTimelineInput = Seq.empty } }
                |> NoteOffsetStatePatternMap.fromSourceInput duration

            { EventPatternInput.TimelineInput =
                  seq {
                      { Position = 0.0
                        Value = noteOffsetState }
                  }
              EventPatternInput.PatternTimelineInput = Seq.empty }
            |> EventPattern.fromInput duration

        let createPart context =
            let duration =
                2.0 ** float (context |> Generate.int (4, 6))

            let partTrackCount = context |> Generate.int (1, 4)

            let pitchInstrumentTrackNumbers =
                context
                |> Generate.subSequence (seq { 0 .. pitchInstrumentTrackCount - 1 }) partTrackCount

            let pitchInstrumentTrackTimelines =
                pitchInstrumentTrackNumbers
                |> Seq.map (fun pitchInstrumentTrackNumber ->
                    let limitPatternCount = context |> Generate.int (2, 8)

                    let limitedPatterns =
                        context
                        |> Generate.subSequence pitchInstrumentTrackHigherPatterns.[pitchInstrumentTrackNumber] limitPatternCount

                    let innerPatterns =
                        context
                        |> Generate.sequentialTimeline (fun context ->
                            let item = context |> Generate.item limitedPatterns
                            (item, item.Duration)) duration

                    (pitchInstrumentTrackIndex pitchInstrumentTrackNumber, innerPatterns))

            let percussionPartTimeline =
                let limitPartCount = context |> Generate.int (2, 8)
                let limitedParts =
                    context
                    |> Generate.subSequence percussionParts limitPartCount
                context
                    |> Generate.sequentialTimeline (fun context ->
                        let item = context |> Generate.item limitedParts
                        (item, item.Duration)) duration

            let noteOffsetStatePattern = partNoteOffsetState (16.0, duration)

            { TrackPatternTimelineMapInput = pitchInstrumentTrackTimelines
              NoteOffsetStatePatternMapPatternTimelineInput =
                  seq {
                      { Position = 0.0
                        Value = noteOffsetStatePattern }
                  }
              TrackNoteOffsetStateBasedEventPatternMapTimelineInput = percussionPartTimeline }
            |> TrackNoteOffsetStateBasedEventPatternMap.fromInput duration

        let parts =
            context
            |> Generate.sequence createPart templateCount
            |> Seq.toArray

        let createHigherPart context =
            let duration =
                2.0 ** float (context |> Generate.int (6, 8))

            let limitPartCount = context |> Generate.int (1, 4)

            let limitedParts =
                context
                |> Generate.subSequence parts limitPartCount

            let innerParts =
                context
                |> Generate.sequentialTimeline (fun context ->
                    let item = context |> Generate.item limitedParts
                    (item, item.Duration)) duration

            let noteOffsetStatePattern = partNoteOffsetState (16.0, duration)

            { TrackPatternTimelineMapInput = Seq.empty
              NoteOffsetStatePatternMapPatternTimelineInput =
                  seq {
                      { Position = 0.0
                        Value = noteOffsetStatePattern }
                  }
              TrackNoteOffsetStateBasedEventPatternMapTimelineInput = innerParts }
            |> TrackNoteOffsetStateBasedEventPatternMap.fromInput duration

        let songTimeline =
            context
            |> Generate.sequentialTimeline (fun context ->
                let item = createHigherPart context
                (item, item.Duration)) (duration)

        let toStatePatternTimeline value =
            seq {
                { Position = 0.0
                  Value =
                      { TimelineInput = seq { { Position = 0.0; Value = value } }
                        PatternTimelineInput = Seq.empty }
                      |> StatePattern.fromInput duration StateMerger.multiplicativeFloat }
            }

        let toEventPatternTimeline value =
            seq {
                { Position = 0.0
                  Value =
                      { EventPatternInput.TimelineInput = seq { { Position = 0.0; Value = value } }
                        EventPatternInput.PatternTimelineInput = Seq.empty }
                      |> EventPattern.fromInput duration }
            }

        let tempoPatternTimeline = toStatePatternTimeline tempo
        let scalePatternTimeline = toEventPatternTimeline scale

        let tracks = seq<InstrumentTrack> {
            yield! pitchInstrumentTracks |> Seq.map PitchInstrumentTrack
            yield! percussionInstrumentTracks |> Seq.map PercussionInstrumentTrack
        }

        SongPatternMap(duration, tracks, songTimeline, tempoPatternTimeline, scalePatternTimeline)
