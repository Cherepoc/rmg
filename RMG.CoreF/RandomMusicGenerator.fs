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

        let context = Generate.Context()
        let duration = 512.0
        let templateCount = 16

        let scale: Scale =
            { KeyOffsets = [ 0; 2; 3; 5; 7; 8; 10 ] }

        let tempo = context |> Generate.float (0.75, 1.5)

        let trackCount = context |> Generate.int (4, 8)

        let tracks =
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
                        ScaleOffset = 0
                        Volume = context |> Generate.float (0.75, 1.5) } }) trackCount

        let patterns =
            context
            |> Generate.sequence (fun context ->
                let duration: float =
                    2.0 ** (float (context |> Generate.int (0, 2)))

                let offset =
                    context
                    |> Generate.rhythmValue (halfProbabilityFunction, 2.0, 4.0, 0.0, 4.0, 4)

                let period: float =
                    (context
                     |> Generate.rhythmPeriod (threeQuarterProbabilityFunction, 8))
//                    / (2.0
//                       ** float
//                           (context
//                            |> Generate.intByRank (threeQuarterProbabilityFunction, -1, 1)))

                let maxRank = 4

                let noteGenerator =
                    fun context rank ->
                        { Duration =
                              (context
                              |> Generate.rhythmValue (halfProbabilityFunction, 1.0, 2.0, 0.0, 2.0, 4)) / 4.0
                          Volume =
                              context
                              |> Generate.rhythmValue (threeQuarterProbabilityFunction, 0.5, 1.0, 0.75, 1.5, 5)
                          OctaveOffset =
                              context
                              |> Generate.intByRank (eighthProbabilityFunction, -1, 1)
                          ScaleOffset =
                              context
                              |> Generate.intByRank (halfProbabilityFunction, -2, 2)
                          KeyOffset = 0 }

                let notes =
                    context
                    |> Generate.timeline noteGenerator (quarterProbabilityFunction, maxRank, offset, period, duration)

                EventPattern<'T>(duration, notes, Seq.empty)) templateCount
            |> Seq.toArray

        let higherPatterns =
            context
            |> Generate.sequence (fun context ->
                let duration =
                    2.0 ** float (context |> Generate.int (2, 4))

                let limitPatternCount = context |> Generate.int (1, 4)

                let limitedPatterns =
                    context
                    |> Generate.subSequence patterns limitPatternCount

                let innerPatterns =
                    context
                    |> Generate.sequentialTimeline (fun context ->
                        let item = context |> Generate.item limitedPatterns
                        (item, item.Duration)) duration

                { PatternTimelineInput = innerPatterns
                  NoteOffsetStatePatternMapPatternTimelineInput = Seq.empty
                  NoteOffsetBasedPatternTimelineInput = Seq.empty }
                |> NoteOffsetStateBasedEventPattern.fromInput duration) templateCount
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
                            generateFloatNoteOffsetTimeline (fun context rank -> context |> Generate.float (0.75, 1.5))
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
                            generateIntNoteOffsetTimeline (fun context rank ->
                                context
                                |> Generate.intByRank (halfProbabilityFunction, -2, 2))
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

        let parts =
            context
            |> Generate.sequence (fun context ->
                let duration =
                    2.0 ** float (context |> Generate.int (4, 6))

                let partTrackCount = context |> Generate.int (1, 4)

                let trackNumbers =
                    context
                    |> Generate.subSequence (seq { 0 .. trackCount - 1 }) partTrackCount

                let trackTimelines =
                    trackNumbers
                    |> Seq.map (fun trackNumber ->
                        let limitPatternCount = context |> Generate.int (2, 8)

                        let limitedPatterns =
                            context
                            |> Generate.subSequence higherPatterns limitPatternCount

                        let innerPatterns =
                            context
                            |> Generate.sequentialTimeline (fun context ->
                                let item = context |> Generate.item limitedPatterns
                                (item, item.Duration)) duration

                        (trackNumber, innerPatterns))

                let noteOffsetStatePattern = partNoteOffsetState (16.0, duration)

                { TrackPatternTimelineMapInput = trackTimelines
                  NoteOffsetStatePatternMapPatternTimelineInput =
                      seq {
                          { Position = 0.0
                            Value = noteOffsetStatePattern }
                      }
                  TrackNoteOffsetStateBasedEventPatternMapTimelineInput = Seq.empty }
                |> TrackNoteOffsetStateBasedEventPatternMap.fromInput duration) templateCount
            |> Seq.toArray

        let higherParts =
            context
            |> Generate.sequence (fun context ->
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
                |> TrackNoteOffsetStateBasedEventPatternMap.fromInput duration) templateCount
            |> Seq.toArray

        let songTimeline =
            context
            |> Generate.sequentialTimeline (fun context ->
                let item = context |> Generate.item higherParts
                (item, item.Duration)) (duration)

        let toStatePatternTimeline value =
            seq {
                { Position = 0.0
                  Value =
                      { TimelineInput = seq { { Position = 0.0; Value = value } }
                        PatternTimelineInput = Seq.empty }
                      |> StatePattern.fromInput duration StateMerger.multiplicative }
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

        SongPatternMap(duration, tracks, songTimeline, tempoPatternTimeline, scalePatternTimeline)
