namespace RMG.Core.Generation

open RMG.Core
open RMG.Core.Composition

type TrackStyle =
    {
        TrackStyleDefinition: TrackStyleDefinition;
        ChildStyles: TrackStyle list;
        PatternDefinitions: (PatternGenerationDefinition * EventStatePattern list) list;
        PartDefinitions: (PatternGenerationDefinition * TrackEventStatePattern list) list
    }

type SongStyleInput = { TrackStyleDefinition: TrackStyleDefinition }

module TrackStyleGeneration =
    let filterSharedTrackStyles (styles: TrackStyle list) : (TrackNumber * TrackStyle) list =
        styles
        |> List.choose
            (fun style ->
                match style.TrackStyleDefinition.Identity with
                | SharedTrackStyleIdentity trackNumber -> Some(trackNumber, style)
                | _ -> None)

    let filterNoteTrackStyles (styles: TrackStyle list) : (TrackNumber * TrackStyle) list =
        styles
        |> List.choose
            (fun style ->
                match style.TrackStyleDefinition.Identity with
                | NoteTrackStyleIdentity trackNumber -> Some(trackNumber, style)
                | _ -> None)

    let filterSingleTrackStyles (styles: TrackStyle list) : (TrackNumber * TrackStyle) list =
        styles
        |> List.choose
            (fun style ->
                match style.TrackStyleDefinition.Identity with
                | SharedTrackStyleIdentity trackNumber -> Some(trackNumber, style)
                | NoteTrackStyleIdentity trackNumber -> Some(trackNumber, style)
                | _ -> None)

    let filterMultiTrackStyles (styles: TrackStyle list) : (TrackStyleDefinition list * TrackStyle) list =
        styles
        |> List.choose
            (fun style ->
                match style.TrackStyleDefinition.Identity with
                | MultiTrackStyleIdentity childStyleDefs -> Some(childStyleDefs, style)
                | _ -> None)

    let generateSongStyle (input: SongStyleInput) =
        let halfProbabilityFunction offset = Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.5, offset)

        let threeQuarterProbabilityFunction offset = Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.75, offset)

        let quarterProbabilityFunction offset = Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.25, offset)

        let eighthProbabilityFunction offset = Probability.geometricIntProbabilityFunction (0.0, 1.0, 0.125, offset)

        let context = Generate.Context()
        let templateCount = 16
        let maxSubPatternCount = 4

        let standardVelocity () = context |> Generate.float (0.875, 1.125)

        let standardDuration () = context |> Generate.float (0.8, 1.25)

        let standardPartCount () =
            context |> Generate.intByRank (threeQuarterProbabilityFunction 2, 1, maxSubPatternCount)

        let standardOffsets (min: int, max: int) : float list =
            context
            |> Generate.sequence (fun context -> context |> Generate.float (0.0, 1.0)) (context |> Generate.intByRank (halfProbabilityFunction 0, min, max))
            |> List.ofSeq

        let standardOctaveOffset () = context |> Generate.intByRank (halfProbabilityFunction 0, -2, 2)

        let standardArticulationOffset () = Probability.floatSpline 1.0 (context |> Generate.float (-1.0, 1.0))

        let maxPeriodPrimeRank = 3

        let generatePatternStyleDefinition (style: PatternGenerationDefinition) : PatternGenerationDefinition =
            let periodPrimeOffset =
                context
                |> Generate.intByRank (eighthProbabilityFunction style.PeriodPrimeOffset, -maxPeriodPrimeRank, maxPeriodPrimeRank)

            let periodPowerOffset =
                context |> Generate.intByRank (eighthProbabilityFunction style.PeriodPowerOffset, -2, 1)

            let phaseRankOffset =
                context |> Generate.intByRank (eighthProbabilityFunction style.PhaseRankOffset, 0, 4)

            let probabilityPowerOffset =
                context |> Generate.intByRank (eighthProbabilityFunction style.ProbabilityPowerOffset, 1, 4)

            {
                DurationRank = style.DurationRank;
                PeriodPrimeOffset = periodPrimeOffset;
                PeriodPowerOffset = periodPowerOffset;
                PhaseRankOffset = phaseRankOffset;
                ProbabilityPowerOffset = probabilityPowerOffset
            }

        let generatePatternStyleDefinitionForDuration (durationRank: int) : PatternGenerationDefinition =
            generatePatternStyleDefinition
                { PatternGenerationDefinition.defaultDefinition with
                    DurationRank = durationRank
                }

        let generateNote context : EventState =
            { EventState.empty with
                Duration = [(context |> Generate.rhythmValue (halfProbabilityFunction 0, 1.0, 2.0, 0.25, 2.0, 4)) / 4.0]
                Velocity = [context |> Generate.rhythmValue (threeQuarterProbabilityFunction 0, 0.5, 1.0, 0.75, 1.5, 5)]
                OctaveOffset = [context |> Generate.intByRank (eighthProbabilityFunction 0, -1, 1)]
                ChordNoteOffsets = standardOffsets (1, 1)
                ArticulationOffset = [standardArticulationOffset ()]
            }

        let getDuration (style: PatternGenerationDefinition) : float = 4.0 ** (float style.DurationRank)

        let generateNotePatternFromStyle (style: PatternGenerationDefinition) : EventStatePattern =
            let duration: float = getDuration style

            let phase =
                context
                |> Generate.rhythmValue (halfProbabilityFunction style.PhaseRankOffset, 0.0, 1.0, 0.0, 1.0, 4)

            let periodPrime =
                context
                |> Generate.pickRhythmPeriod (quarterProbabilityFunction style.PeriodPrimeOffset, maxPeriodPrimeRank)

            let periodPower =
                context |> Generate.intByRank (quarterProbabilityFunction style.PeriodPowerOffset, -2, 1)

            let period: float = periodPrime * (2.0 ** float periodPower)

            let maxRank = 2

            let noteGenerator = fun context _ -> generateNote context

            let noteProbabilityRank =
                (context |> Generate.intByRank (quarterProbabilityFunction style.ProbabilityPowerOffset, 0, 14)) + 2

            let noteProbabilityMultiplier = 1.0 / float noteProbabilityRank

            let noteProbabilityFunction =
                Probability.geometricIntProbabilityFunction (0.0, 15.0 / 16.0, noteProbabilityMultiplier, 0)

            let notes =
                Probability.rankTimeline maxRank phase period duration
                |> Generate.fromRankTimeline context noteGenerator noteProbabilityFunction

            let eventStateTimeline =
                { EventStateTimelineMap.emptyInput with
                    NoteTimeline = notes
                }
                |> EventStateTimelineMap.fromInput duration

            EventStatePattern.ofTimelines duration eventStateTimeline Timeline.empty

        let generateHigherNotePatternFromStyle (style: PatternGenerationDefinition) (sourcePatterns: EventStatePattern list) : EventStatePattern =
            let duration = getDuration style
            let maxRank = 2

            let limitPatternCount = standardPartCount ()

            let limitedPatterns = context |> Generate.subSequence sourcePatterns limitPatternCount

            let noteOffset () : EventState =
                { EventState.empty with
                    Velocity = [standardVelocity ()]
                    OctaveOffset = [standardOctaveOffset ()]
                    ChordScaleOffsets = standardOffsets (0, 1)
                    ChordNoteOffsets = standardOffsets (0, 1)
                }

            let noteProbabilityOffset =
                context |> Generate.intByRank (quarterProbabilityFunction style.ProbabilityPowerOffset, 1, 8)

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

        let generateStyleNotePatterns (durationRankStart: int, durationRankEnd: int) : (PatternGenerationDefinition * EventStatePattern list) list =
            let mutable definitions: (PatternGenerationDefinition * EventStatePattern list) list = List.empty

            for durationRank = durationRankStart to durationRankEnd do
                let patternStyleDefinition = generatePatternStyleDefinitionForDuration durationRank

                let lastRankStyleDefinition = definitions |> List.tryHead

                let patternGenerator =
                    match lastRankStyleDefinition with
                    | Some (_, patterns) -> fun _ -> generateHigherNotePatternFromStyle patternStyleDefinition patterns
                    | _ -> fun _ -> generateNotePatternFromStyle patternStyleDefinition

                let patterns = context |> Generate.sequence patternGenerator templateCount |> List.ofSeq

                definitions <- (patternStyleDefinition, patterns) :: definitions

            definitions |> List.rev

        let generateSharedPatternFromStyle (style: PatternGenerationDefinition) : EventStatePattern =
            let duration: float = getDuration style

            let generateState (eventGenerator: Generate.Context -> int -> 'T list) : 'T list Timeline =
                let phase =
                    context
                    |> Generate.rhythmValue (halfProbabilityFunction style.PhaseRankOffset, 0.0, 1.0, 0.0, 1.0, 4)

                let periodPrime =
                    context
                    |> Generate.pickRhythmPeriod (quarterProbabilityFunction style.PeriodPrimeOffset, maxPeriodPrimeRank)

                let periodPower =
                    context |> Generate.intByRank (quarterProbabilityFunction style.PeriodPowerOffset, -2, 1)

                let period: float = periodPrime * (2.0 ** float periodPower)

                let maxRank = 2

                Probability.rankTimeline maxRank phase period duration
                |> Generate.fromRankTimeline context eventGenerator (eighthProbabilityFunction 0)
                
            let eventStateTimeline =
                { EventStateTimelineMap.emptyInput with
                    ChordScaleOffsetsTimeline = generateState (fun _ _ -> standardOffsets (1, 3))
                    ChordRootOffsetTimeline = generateState (fun _ _ -> [standardArticulationOffset ()])
                    ArticulationOffsetTimeline = generateState (fun _ _ -> [standardArticulationOffset ()])
                }
                |> EventStateTimelineMap.fromInput duration

            EventStatePattern.ofTimelines duration eventStateTimeline Timeline.empty

        let generateHigherSharedPatternFromStyle (style: PatternGenerationDefinition) (sourcePatterns: EventStatePattern list) : EventStatePattern =
            let duration = getDuration style
            let maxRank = 2

            let limitPatternCount = standardPartCount ()

            let limitedPatterns = context |> Generate.subSequence sourcePatterns limitPatternCount

            let noteOffset () : EventState =
                { EventState.empty with
                    ChordScaleOffsets = standardOffsets (1, 3)
                    ChordRootOffset = [standardArticulationOffset ()]
                    ArticulationOffset = [standardArticulationOffset ()]
                }

            let noteProbabilityOffset =
                context |> Generate.intByRank (quarterProbabilityFunction style.ProbabilityPowerOffset, 1, 8)

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

        let generateStyleSharedPatterns (durationRankStart: int, durationRankEnd: int) : (PatternGenerationDefinition * EventStatePattern list) list =
            let mutable definitions: (PatternGenerationDefinition * EventStatePattern list) list = List.empty

            for durationRank = durationRankStart to durationRankEnd do
                let patternStyleDefinition = generatePatternStyleDefinitionForDuration durationRank

                let lastRankStyleDefinition = definitions |> List.tryHead

                let patternGenerator =
                    match lastRankStyleDefinition with
                    | Some (_, patterns) -> fun _ -> generateHigherSharedPatternFromStyle patternStyleDefinition patterns
                    | _ -> fun _ -> generateSharedPatternFromStyle patternStyleDefinition

                let patterns = context |> Generate.sequence patternGenerator templateCount |> List.ofSeq

                definitions <- (patternStyleDefinition, patterns) :: definitions

            definitions |> List.rev

        let generatePartFromStyle (style: PatternGenerationDefinition) (childStyles: TrackStyle list) : TrackEventStatePattern =
            let duration = getDuration style

            let noteOffset () =
                { EventState.empty with
                    Duration = [standardDuration ()]
                    OctaveOffset = [standardOctaveOffset ()]
                    ChordRootOffset = [standardArticulationOffset ()]
                    ChordScaleOffsets = standardOffsets (0, 1)
                    ChordNoteOffsets = standardOffsets (0, 1)
                    Velocity = [standardVelocity ()]
                    ArticulationOffset = [standardArticulationOffset ()]
                }

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

        let generateHigherPartFromStyle (style: PatternGenerationDefinition) (sourcePatterns: TrackEventStatePattern list) : TrackEventStatePattern =
            let duration = getDuration style

            let noteOffset () =
                { EventState.empty with
                    Duration = [standardDuration ()]
                    KeyOffset = [if context |> Generate.test 0.25 then context |> Generate.int (-6, 6) else 0]
                    OctaveOffset = [standardOctaveOffset ()]
                    ChordRootOffset = [standardArticulationOffset ()]
                    ChordScaleOffsets = standardOffsets (0, 1)
                    ChordNoteOffsets = standardOffsets (0, 1)
                    Velocity = [standardVelocity ()]
                    ArticulationOffset = [standardArticulationOffset ()]
                }

            let limitPartCount = standardPartCount ()

            let limitedParts = context |> Generate.subSequence sourcePatterns limitPartCount

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
            (childStyles: TrackStyle list)
            : (PatternGenerationDefinition * TrackEventStatePattern list) list =
            let mutable definitions: (PatternGenerationDefinition * TrackEventStatePattern list) list = List.empty

            for durationRank = durationRankStart to durationRankEnd do
                let patternStyleDefinition = generatePatternStyleDefinitionForDuration durationRank

                let lastRankStyleDefinition = definitions |> List.tryHead

                let patternGenerator =
                    match lastRankStyleDefinition with
                    | Some (_, patterns) -> fun _ -> generateHigherPartFromStyle patternStyleDefinition patterns
                    | _ -> fun _ -> generatePartFromStyle patternStyleDefinition childStyles

                let patterns = context |> Generate.sequence patternGenerator templateCount |> List.ofSeq

                definitions <- (patternStyleDefinition, patterns) :: definitions

            definitions |> List.rev

        let rec generateStyleFromDefinition (styleDef: TrackStyleDefinition) : TrackStyle =
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
                let childStyles = childStyleDefinitions |> List.map generateStyleFromDefinition

                {
                    TrackStyleDefinition = styleDef;
                    ChildStyles = childStyles;
                    PatternDefinitions = List.empty;
                    PartDefinitions = generateStyleParts styleDef.PatternDurationRankRange childStyles
                }

        generateStyleFromDefinition input.TrackStyleDefinition
