namespace RMG.Core.Generation

type PatternGenerationDefinition =
    {
        DurationRank: int;
        PeriodPrimeOffset: int;
        PeriodPowerOffset: int;
        PhaseRankOffset: int;
        ProbabilityPowerOffset: int
    }

module PatternGenerationDefinition =
    let defaultDefinition: PatternGenerationDefinition =
        {
            DurationRank = 0;
            PeriodPrimeOffset = 0;
            PeriodPowerOffset = 0;
            PhaseRankOffset = 0;
            ProbabilityPowerOffset = 0
        }

    let yieldPatternStyleDefinition (style: PatternGenerationDefinition) : int seq =
        seq {
            style.DurationRank
            style.PeriodPrimeOffset
            style.PeriodPowerOffset
            style.PhaseRankOffset
            style.ProbabilityPowerOffset
        }

    let scorePatternStyleDefinition (referenceStyle: PatternGenerationDefinition) (style: PatternGenerationDefinition) : int =
        Seq.zip (yieldPatternStyleDefinition style) (yieldPatternStyleDefinition referenceStyle)
        |> Seq.sumBy (fun (referenceValue, value) -> pown (abs (referenceValue - value)) 2)

    let scorePatternStyleDefinitionZip (referenceStyles: PatternGenerationDefinition seq) (styles: PatternGenerationDefinition seq) : int =
        Seq.zip referenceStyles styles
        |> Seq.sumBy (fun (referenceStyle, style) -> scorePatternStyleDefinition referenceStyle style)

    let weightPatternStyleDefinitionListByScore<'T>
        (referenceStyle: PatternGenerationDefinition)
        (items: (PatternGenerationDefinition * 'T) list)
        : struct ((PatternGenerationDefinition * 'T) * float) seq =
        items
        |> Seq.map
            (fun (style, item) ->
                let weight = 1.0 / float (scorePatternStyleDefinition referenceStyle style)
                struct ((style, item), weight))
