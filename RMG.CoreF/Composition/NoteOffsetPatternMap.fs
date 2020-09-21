namespace RMG.CoreF.Composition

open RMG.CoreF

[<NoEquality>]
[<NoComparison>]
type NoteOffsetPatternMap =
    { Duration: Pattern<Duration>
      KeyOffset: Pattern<KeyOffset>
      OctaveOffset: Pattern<OctaveOffset>
      ScaleOffset: Pattern<ScaleOffset>
      Volume: Pattern<Volume> }

module NoteOffsetPatternMap =
    let empty: NoteOffsetPatternMap =
        { Duration = Pattern.empty
          KeyOffset = Pattern.empty
          OctaveOffset = Pattern.empty
          ScaleOffset = Pattern.empty
          Volume = Pattern.empty }

    let flatten (duration: Duration) (patternMap: NoteOffsetPatternMap): NoteOffsetTimelineMap =
        { Duration =
              patternMap.Duration
              |> Pattern.flatten
                  (duration,
                   NoteOffsetTimelineMap.Merger.duration
                   |> Timeline.Merger.toCombineFunction)
          KeyOffset =
              patternMap.KeyOffset
              |> Pattern.flatten
                  (duration,
                   NoteOffsetTimelineMap.Merger.keyOffset
                   |> Timeline.Merger.toCombineFunction)
          OctaveOffset =
              patternMap.OctaveOffset
              |> Pattern.flatten
                  (duration,
                   NoteOffsetTimelineMap.Merger.octaveOffset
                   |> Timeline.Merger.toCombineFunction)
          ScaleOffset =
              patternMap.ScaleOffset
              |> Pattern.flatten
                  (duration,
                   NoteOffsetTimelineMap.Merger.scaleOffset
                   |> Timeline.Merger.toCombineFunction)
          Volume =
              patternMap.Volume
              |> Pattern.flatten
                  (duration,
                   NoteOffsetTimelineMap.Merger.volume
                   |> Timeline.Merger.toCombineFunction) }
