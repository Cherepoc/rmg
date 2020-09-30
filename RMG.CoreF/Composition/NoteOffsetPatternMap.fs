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
                  duration
                     (NoteOffsetTimelineMap.Blender.duration
                      |> TimelineBlender.toCombineFunction)
              |> Timeline.fromSequence
          KeyOffset =
              patternMap.KeyOffset
              |> Pattern.flatten
                  duration
                     (NoteOffsetTimelineMap.Blender.keyOffset
                      |> TimelineBlender.toCombineFunction)
              |> Timeline.fromSequence
          OctaveOffset =
              patternMap.OctaveOffset
              |> Pattern.flatten
                  duration
                     (NoteOffsetTimelineMap.Blender.octaveOffset
                      |> TimelineBlender.toCombineFunction)
              |> Timeline.fromSequence
          ScaleOffset =
              patternMap.ScaleOffset
              |> Pattern.flatten
                  duration
                     (NoteOffsetTimelineMap.Blender.scaleOffset
                      |> TimelineBlender.toCombineFunction)
              |> Timeline.fromSequence
          Volume =
              patternMap.Volume
              |> Pattern.flatten
                  duration
                     (NoteOffsetTimelineMap.Blender.volume
                      |> TimelineBlender.toCombineFunction)
              |> Timeline.fromSequence }
