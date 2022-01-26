namespace RMG.Core

open RMG.Core.Composition

type StyleDefinition =
    {
        Identity: StyleIdentity;
        PatternDurationRankRange: int * int
    }

and StyleIdentity =
    | SharedTrackStyleIdentity of TrackNumber
    | NoteTrackStyleIdentity of TrackNumber
    | MultiTrackStyleIdentity of StyleDefinition list

type PatternStyleDefinition =
    {
        DurationRank: int;
        PeriodPrimeOffset: int;
        PeriodPowerOffset: int;
        PhaseRankOffset: int;
        ProbabilityPowerOffset: int
    }

type Style =
    {
        TrackStyleDefinition: StyleDefinition;
        ChildStyles: Style list;
        PatternDefinitions: (PatternStyleDefinition * EventStatePattern list) list;
        PartDefinitions: (PatternStyleDefinition * TrackEventStatePattern list) list
    }


module RandomMusicGenerator =
    let filterSharedTrackStyles (styles: Style list) : (TrackNumber * Style) list =
        styles
        |> List.choose
            (fun style ->
                match style.TrackStyleDefinition.Identity with
                | SharedTrackStyleIdentity trackNumber -> Some(trackNumber, style)
                | _ -> None)

    let filterNoteTrackStyles (styles: Style list) : (TrackNumber * Style) list =
        styles
        |> List.choose
            (fun style ->
                match style.TrackStyleDefinition.Identity with
                | NoteTrackStyleIdentity trackNumber -> Some(trackNumber, style)
                | _ -> None)

    let filterSingleTrackStyles (styles: Style list) : (TrackNumber * Style) list =
        styles
        |> List.choose
            (fun style ->
                match style.TrackStyleDefinition.Identity with
                | SharedTrackStyleIdentity trackNumber -> Some(trackNumber, style)
                | NoteTrackStyleIdentity trackNumber -> Some(trackNumber, style)
                | _ -> None)

    let filterMultiTrackStyles (styles: Style list) : (StyleDefinition list * Style) list =
        styles
        |> List.choose
            (fun style ->
                match style.TrackStyleDefinition.Identity with
                | MultiTrackStyleIdentity childStyleDefs -> Some(childStyleDefs, style)
                | _ -> None)

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
        let songDuration = 256.0
        let templateCount = 16
        let maxSubPatternCount = 4
        let pitchInstrumentTrackCount = context |> Generate.int (4, 8)
        let percussionInstrumentTrackCount = context |> Generate.int (4, 8)
        let partPitchInstrumentTrackCount () = context |> Generate.int (1, 4)
        let partPercussionInstrumentTrackCount () = context |> Generate.int (1, 4)

        let standardVelocity () = context |> Generate.float (0.875, 1.125)
        let standardDuration () = context |> Generate.float (0.8, 1.25)

        let standardPartCount () =
            context
            |> Generate.intByRank (threeQuarterProbabilityFunction 2, 1, maxSubPatternCount)

        let standardOffsets (min: int, max: int) : float list =
            context
            |> Generate.sequence (fun context -> context |> Generate.float (0.0, 1.0)) (context |> Generate.intByRank (halfProbabilityFunction 0, min, max))
            |> List.ofSeq

        let standardOctaveOffset () =
            context |> Generate.intByRank (halfProbabilityFunction 0, -2, 2)

        let standardArticulationOffset () =
            Probability.floatSpline 1.0 (context |> Generate.float (-1.0, 1.0))

        let scale: ScaleOffsets = [ 0; 2; 3; 5; 7; 8; 10 ]

        let tempo = context |> Generate.float (0.5, 1.0)

        let maxPeriodPrimeRank = 3

        let sharedStyleCount = 4
        let pitchInstrumentStyleCount = 8
        let percussionInstrumentStyleCount = 8
        let mutable styleIndex: TrackNumber = 0

        let nextStyleIndex () : int =
            let styleIndexTemp = styleIndex
            styleIndex <- styleIndex + 1
            styleIndexTemp

        let songStyleDef: StyleDefinition =
            {
                PatternDurationRankRange = 2, 3;
                Identity =
                    seq {
                        yield!
                            seq { 1 .. sharedStyleCount }
                            |> Seq.map
                                (fun _ ->
                                    {
                                        Identity = nextStyleIndex () |> SharedTrackStyleIdentity;
                                        PatternDurationRankRange = 1, 2
                                    })

                        yield!
                            seq { 1 .. pitchInstrumentStyleCount }
                            |> Seq.map
                                (fun _ ->
                                    {
                                        Identity = nextStyleIndex () |> NoteTrackStyleIdentity;
                                        PatternDurationRankRange = 0, 2
                                    })

                        yield
                            {
                                PatternDurationRankRange = 0, 2;
                                Identity =
                                    seq { 1 .. percussionInstrumentStyleCount }
                                    |> Seq.map
                                        (fun _ ->
                                            {
                                                Identity = nextStyleIndex () |> NoteTrackStyleIdentity;
                                                PatternDurationRankRange = 0, 0
                                            })
                                    |> List.ofSeq
                                    |> MultiTrackStyleIdentity
                            }
                    }
                    |> List.ofSeq
                    |> MultiTrackStyleIdentity
            }

        let generatePatternStyleDefinition (durationRank: int) : PatternStyleDefinition =
            let periodPrimeOffset =
                context
                |> Generate.intByRank (eighthProbabilityFunction 0, -maxPeriodPrimeRank, maxPeriodPrimeRank)

            let periodPowerOffset =
                context |> Generate.intByRank (eighthProbabilityFunction 0, -2, 1)

            let maxPhaseRank = 4

            let phaseRankOffset =
                context |> Generate.intByRank (eighthProbabilityFunction 0, 0, maxPhaseRank)

            let probabilityPowerOffset =
                context |> Generate.intByRank (eighthProbabilityFunction 0, 1, 4)

            {
                DurationRank = durationRank;
                PeriodPrimeOffset = periodPrimeOffset;
                PeriodPowerOffset = periodPowerOffset;
                PhaseRankOffset = phaseRankOffset;
                ProbabilityPowerOffset = probabilityPowerOffset
            }

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

        let getDuration (style: PatternStyleDefinition) : float = 4.0 ** (float style.DurationRank)

        let generateNotePatternFromStyle (style: PatternStyleDefinition) : EventStatePattern =
            let duration: float = getDuration style

            let phase =
                context
                |> Generate.rhythmValue (halfProbabilityFunction style.PhaseRankOffset, 0.0, 1.0, 0.0, 1.0, 4)

            let periodPrime =
                context
                |> Generate.pickRhythmPeriod (quarterProbabilityFunction style.PeriodPrimeOffset, maxPeriodPrimeRank)

            let periodPower =
                context
                |> Generate.intByRank (quarterProbabilityFunction style.PeriodPowerOffset, -2, 1)

            let period: float = periodPrime * (2.0 ** float periodPower)

            let maxRank = 2

            let noteGenerator = fun context _ -> generateNote context

            let noteProbabilityRank =
                (context
                 |> Generate.intByRank (quarterProbabilityFunction style.ProbabilityPowerOffset, 0, 14))
                + 2

            let noteProbabilityMultiplier = 1.0 / float noteProbabilityRank

            let noteProbabilityFunction =
                Probability.geometricIntProbabilityFunction (0.0, 15.0 / 16.0, noteProbabilityMultiplier, 0)

            let notes =
                Probability.rankTimeline maxRank phase period duration
                |> Generate.fromRankTimeline context noteGenerator noteProbabilityFunction

            let eventStateTimeline =
                EventStateTimelineMap.ofTimelines duration notes Timeline.empty

            EventStatePattern.ofTimelines duration eventStateTimeline Timeline.empty

        let generateHigherNotePatternFromStyle (style: PatternStyleDefinition) (sourcePatterns: EventStatePattern list) : EventStatePattern =
            let duration = getDuration style
            let maxRank = 2

            let limitPatternCount = standardPartCount ()

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

            let noteProbabilityOffset =
                context
                |> Generate.intByRank (quarterProbabilityFunction style.ProbabilityPowerOffset, 1, 8)

            let noteProbabilityMultiplier = (7.0 / 8.0) ** (float noteProbabilityOffset)

            let subPatternProbabilityFunction =
                Probability.geometricIntProbabilityFunction (0.0, 15.0 / 16.0, noteProbabilityMultiplier, 0)

            let phase =
                context
                |> Generate.rhythmValue (halfProbabilityFunction style.PhaseRankOffset, 0.0, duration, 0.0, duration, maxRank)

            let innerPatterns =
                Probability.rankTimeline maxRank phase duration duration
                |> Generate.fromRankTimeline
                    context
                    (fun context _ -> struct (noteOffset (), context |> Generate.item limitedPatterns))
                    subPatternProbabilityFunction

            EventStatePattern.ofTimelines duration (EventStateTimelineMap.empty duration) innerPatterns

        let generateStyleNotePatterns (durationRankStart: int, durationRankEnd: int) : (PatternStyleDefinition * EventStatePattern list) list =
            let mutable definitions: (PatternStyleDefinition * EventStatePattern list) list = List.empty

            for durationRank = durationRankStart to durationRankEnd do
                let patternStyleDefinition = generatePatternStyleDefinition durationRank
                let lastRankStyleDefinition = definitions |> List.tryHead

                let patternGenerator =
                    match lastRankStyleDefinition with
                    | Some (_, patterns) -> fun _ -> generateHigherNotePatternFromStyle patternStyleDefinition patterns
                    | _ -> fun _ -> generateNotePatternFromStyle patternStyleDefinition

                let patterns =
                    context |> Generate.sequence patternGenerator templateCount |> List.ofSeq

                definitions <- (patternStyleDefinition, patterns) :: definitions

            definitions |> List.rev

        let generateSharedPatternFromStyle (style: PatternStyleDefinition) : EventStatePattern =
            let duration: float = getDuration style

            let generateState (eventGenerator: Generate.Context -> int -> Event) : Event Timeline =
                let phase =
                    context
                    |> Generate.rhythmValue (halfProbabilityFunction style.PhaseRankOffset, 0.0, 1.0, 0.0, 1.0, 4)

                let periodPrime =
                    context
                    |> Generate.pickRhythmPeriod (quarterProbabilityFunction style.PeriodPrimeOffset, maxPeriodPrimeRank)

                let periodPower =
                    context
                    |> Generate.intByRank (quarterProbabilityFunction style.PeriodPowerOffset, -2, 1)

                let period: float = periodPrime * (2.0 ** float periodPower)

                let maxRank = 2

                Probability.rankTimeline maxRank phase period duration
                |> Generate.fromRankTimeline context eventGenerator (eighthProbabilityFunction 0)

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

        let generateHigherSharedPatternFromStyle (style: PatternStyleDefinition) (sourcePatterns: EventStatePattern list) : EventStatePattern =
            let duration = getDuration style
            let maxRank = 2

            let limitPatternCount = standardPartCount ()

            let limitedPatterns =
                context |> Generate.subSequence sourcePatterns limitPatternCount

            let noteOffset () : EventState =
                seq {
                    //OctaveOffsetEvent(standardOctaveOffset ())
                    //ChordScaleOffsetsEvent(standardOffsets (0, 1))
                    //ChordNoteOffsetsEvent(standardOffsets (0, 1))
                    //VelocityEvent(standardVelocity ())
                    ChordScaleOffsetsEvent(standardOffsets (1, 3))
                    ChordRootOffsetEvent(standardArticulationOffset ())
                    ArticulationOffsetEvent(standardArticulationOffset ())
                }
                |> EventState.ofSeq

            let noteProbabilityOffset =
                context
                |> Generate.intByRank (quarterProbabilityFunction style.ProbabilityPowerOffset, 1, 8)

            let noteProbabilityMultiplier = (7.0 / 8.0) ** (float noteProbabilityOffset)

            let subPatternProbabilityFunction =
                Probability.geometricIntProbabilityFunction (0.0, 15.0 / 16.0, noteProbabilityMultiplier, 0)

            let phase =
                context
                |> Generate.rhythmValue (halfProbabilityFunction style.PhaseRankOffset, 0.0, duration, 0.0, duration, maxRank)

            let innerPatterns =
                Probability.rankTimeline maxRank phase duration duration
                |> Generate.fromRankTimeline
                    context
                    (fun context _ -> struct (noteOffset (), context |> Generate.item limitedPatterns))
                    subPatternProbabilityFunction

            EventStatePattern.ofTimelines duration (EventStateTimelineMap.empty duration) innerPatterns

        let generateStyleSharedPatterns (durationRankStart: int, durationRankEnd: int) : (PatternStyleDefinition * EventStatePattern list) list =
            let mutable definitions: (PatternStyleDefinition * EventStatePattern list) list = List.empty

            for durationRank = durationRankStart to durationRankEnd do
                let patternStyleDefinition = generatePatternStyleDefinition durationRank
                let lastRankStyleDefinition = definitions |> List.tryHead

                let patternGenerator =
                    match lastRankStyleDefinition with
                    | Some (_, patterns) -> fun _ -> generateHigherSharedPatternFromStyle patternStyleDefinition patterns
                    | _ -> fun _ -> generateSharedPatternFromStyle patternStyleDefinition

                let patterns =
                    context |> Generate.sequence patternGenerator templateCount |> List.ofSeq

                definitions <- (patternStyleDefinition, patterns) :: definitions

            definitions |> List.rev

        let generatePartFromStyle (style: PatternStyleDefinition) (childStyles: Style list) : TrackEventStatePattern =
            let duration = getDuration style

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

            let singleTrackTimelines: Map<TrackNumber option, EventStatePattern WithEventState Timeline> =
                childStyles
                |> filterSingleTrackStyles
                |> Seq.map
                    (fun (childStyleNumber, childStyle) ->
                        let timeline =
                            childStyle.PatternDefinitions
                            |> List.tryFind (fun (partDef, _) -> partDef.DurationRank = style.DurationRank)
                            |> Option.map (fun (_, patterns) -> struct (noteOffset (), Generate.item patterns context) |> Timeline.ofSingle)
                            |> Option.defaultValue Timeline.empty

                        (Some childStyleNumber, timeline))
                |> Map.ofSeq

            let multiTrackTimelines: TrackEventStatePattern WithEventState Timeline =
                childStyles
                |> filterMultiTrackStyles
                |> Seq.choose
                    (fun (_, childStyle) ->
                        childStyle.PartDefinitions
                        |> List.tryFind (fun (partDef, _) -> partDef.DurationRank = style.DurationRank)
                        |> Option.map (fun (_, patterns) -> struct (noteOffset (), Generate.item patterns context) |> Timeline.itemOfSingle))
                |> Timeline.ofSeq

            TrackEventStatePattern.ofTimelineMaps duration None singleTrackTimelines multiTrackTimelines

        let generateHigherPartFromStyle (style: PatternStyleDefinition) (sourcePatterns: TrackEventStatePattern list) : TrackEventStatePattern =
            let duration = getDuration style

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

            let limitPartCount = standardPartCount ()

            let limitedParts =
                context |> Generate.subSequence sourcePatterns limitPartCount

            let innerParts =
                context
                |> Generate.sequentialTimeline
                    (fun context ->
                        let item = context |> Generate.item limitedParts
                        (struct (noteOffset (), item), item.Duration))
                    duration

            TrackEventStatePattern.ofTimelineMaps duration None Map.empty innerParts

        let generateStyleParts
            (durationRankStart: int, durationRankEnd: int)
            (childStyles: Style list)
            : (PatternStyleDefinition * TrackEventStatePattern list) list
            =
            let mutable definitions: (PatternStyleDefinition * TrackEventStatePattern list) list = List.empty

            for durationRank = durationRankStart to durationRankEnd do
                let patternStyleDefinition = generatePatternStyleDefinition durationRank
                let lastRankStyleDefinition = definitions |> List.tryHead

                let patternGenerator =
                    match lastRankStyleDefinition with
                    | Some (_, patterns) -> fun _ -> generateHigherPartFromStyle patternStyleDefinition patterns
                    | _ -> fun _ -> generatePartFromStyle patternStyleDefinition childStyles

                let patterns =
                    context |> Generate.sequence patternGenerator templateCount |> List.ofSeq

                definitions <- (patternStyleDefinition, patterns) :: definitions

            definitions |> List.rev

        let rec generateStyleFromDefinition (styleDef: StyleDefinition) : Style =
            match styleDef.Identity with
            | SharedTrackStyleIdentity _ ->
                {
                    TrackStyleDefinition = styleDef;
                    ChildStyles = List.empty;
                    PatternDefinitions = generateStyleSharedPatterns styleDef.PatternDurationRankRange;
                    PartDefinitions = List.empty
                }
            | NoteTrackStyleIdentity _ ->
                {
                    TrackStyleDefinition = styleDef;
                    ChildStyles = List.empty;
                    PatternDefinitions = generateStyleNotePatterns styleDef.PatternDurationRankRange;
                    PartDefinitions = List.empty
                }
            | MultiTrackStyleIdentity childStyleDefinitions ->
                let childStyles =
                    childStyleDefinitions |> List.map generateStyleFromDefinition

                {
                    TrackStyleDefinition = styleDef;
                    ChildStyles = childStyles;
                    PatternDefinitions = List.empty;
                    PartDefinitions = generateStyleParts styleDef.PatternDurationRankRange childStyles
                }

        let songStyle = generateStyleFromDefinition songStyleDef

        let sharedStyles: (TrackNumber * Style) list = songStyle.ChildStyles |> filterSharedTrackStyles

        let pitchInstrumentStyles: (TrackNumber * Style) list = songStyle.ChildStyles |> filterNoteTrackStyles

        let percussionInstrumentStyles: (TrackNumber * Style) list =
            songStyle.ChildStyles
            |> filterMultiTrackStyles
            |> List.collect (fun (_, style) -> filterNoteTrackStyles style.ChildStyles)

        let percussionInstruments: list<struct (PercussionInstrument * float)> =
            [
                ({ ArticulationCodes = [ 35uy; 36uy ] }, 1.0); // kick
                ({ ArticulationCodes = [ 37uy; 38uy; 40uy ] }, 1.0); // snare
                ({ ArticulationCodes = [ 42uy; 44uy; 46uy ] }, 1.0); // hi-hat
                ({
                     ArticulationCodes = [ 41uy; 43uy; 45uy; 47uy; 48uy; 50uy ]
                 },
                 0.2); // tom
                ({ ArticulationCodes = [ 51uy; 53uy; 59uy ] }, 0.2); // ride
                ({ ArticulationCodes = [ 49uy; 52uy; 55uy; 57uy ] }, 0.2); // cymbal
                ({ ArticulationCodes = [ 39uy ] }, 0.2); // clap
                ({ ArticulationCodes = [ 54uy ] }, 0.1); // tambourine
                ({ ArticulationCodes = [ 56uy ] }, 0.1); // cowbell
                ({ ArticulationCodes = [ 58uy ] }, 0.1); // vibraslap
                ({ ArticulationCodes = [ 60uy; 61uy ] }, 0.1); // bongo
                ({ ArticulationCodes = [ 62uy; 63uy; 64uy ] }, 0.1); // conga
                ({ ArticulationCodes = [ 65uy; 66uy ] }, 0.1); // timbale
                ({ ArticulationCodes = [ 67uy; 68uy ] }, 0.1); // agogo
                ({ ArticulationCodes = [ 69uy ] }, 0.1); // cabasa
                ({ ArticulationCodes = [ 70uy ] }, 0.1); // maracas
                ({ ArticulationCodes = [ 71uy; 72uy ] }, 0.1); // whistle
                ({ ArticulationCodes = [ 73uy; 74uy ] }, 0.1); // guiro
                ({ ArticulationCodes = [ 75uy ] }, 0.1); // claves
                ({ ArticulationCodes = [ 76uy; 77uy ] }, 0.1); // wood block
                ({ ArticulationCodes = [ 78uy; 79uy ] }, 0.1); // cuica
                ({ ArticulationCodes = [ 80uy; 81uy ] }, 0.1) // triangle
            ]

        let percussionInstrumentTracks =
            context
            |> Generate.subSequenceWeighted percussionInstruments percussionInstrumentTrackCount
            |> Seq.map
                (fun x ->
                    {
                        Instrument = x;
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
                        Instrument = { Code = byte (context |> Generate.int (0, 120)) };
                        MinOctaveOffset = minOctave;
                        MaxOctaveOffset = maxOctave;
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
                        let styleNumber, _ = context |> Generate.item pitchInstrumentStyles
                        (Some trackNumber, Some styleNumber))

            let percussionTrackCount = partPercussionInstrumentTrackCount ()

            let percussionTrackNumberMap =
                context
                |> Generate.subSequence numberedPercussionInstrumentTracks percussionTrackCount
                |> Seq.map
                    (fun (trackNumber, _) ->
                        let styleNumber, _ = context |> Generate.item percussionInstrumentStyles
                        (Some trackNumber, Some styleNumber))

            let sharedStyleNumber = context |> Generate.item (sharedStyles |> Seq.map fst)

            let trackNumberMap =
                seq {
                    yield (None, Some sharedStyleNumber)
                    yield! pitchTrackNumberMap
                    yield! percussionTrackNumberMap
                }
                |> Map.ofSeq

            let part = context |> Generate.item parts

            TrackEventStatePattern.ofTimelineMaps part.Duration (Some trackNumberMap) Map.empty (struct (EventState.empty, part) |> Timeline.ofSingle)

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

        let songTimeline: TrackEventStatePattern WithEventState Timeline =
            context
            |> Generate.sequentialTimeline
                (fun _ ->
                    let item = generatePart ()
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
