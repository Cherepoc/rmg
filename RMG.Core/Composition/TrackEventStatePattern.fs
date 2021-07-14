namespace RMG.CoreF.Composition

open RMG.CoreF

[<Sealed>]
type TrackEventStatePattern
    private
    (
        duration: Duration,
        trackPatternTimelineMap: Map<TrackNumber option, EventStatePattern WithEvents EventTimeline>,
        trackPatternMapTimeline: TrackEventStatePattern WithEvents EventTimeline,
        flatTimeline: TrackEventStateTimelineMap
    ) =
    member this.Duration = duration
    member this.TrackPatternTimelineMap = trackPatternTimelineMap
    member this.TrackPatternMapTimeline = trackPatternMapTimeline
    member this.FlatTimelineMap = flatTimeline

    new(duration: Duration,
        trackPatternTimelineMapInput: (TrackNumber option * EventStatePattern WithEvents Timeline) seq,
        trackPatternMapTimelineInput: TrackEventStatePattern WithEvents Timeline) =
        let trackPatternTimelineMap =
            trackPatternTimelineMapInput
            |> Seq.map (fun (trackNumber, timeline) -> (trackNumber, timeline |> EventTimeline.fromSequence))
            |> Map.ofSeq

        let trackPatternMapTimeline = trackPatternMapTimelineInput |> EventTimeline.fromSequence

        let flatTimelineMap =
            seq {
                yield!
                    trackPatternTimelineMap
                    |> Map.toSeq
                    |> Seq.map
                        (fun (trackNumber, combinedPatternTimeline) ->
                            (trackNumber,
                             combinedPatternTimeline
                             |> Timeline.map (fun (events, pattern) -> (events, pattern.FlatTimeline))
                             |> EventStateTimeline.concatCombined))

                yield!
                    trackPatternMapTimeline
                    |> Timeline.map (fun (events, pattern) -> (events, pattern.FlatTimelineMap))
                    |> TrackEventStateTimelineMap.concatCombined
                    |> Map.toSeq
            }
            |> Seq.map (fun (trackNumber, timeline) -> (trackNumber, timeline :> Event Timeline))
            |> TrackEventStateTimelineMap.ofSeq duration

        TrackEventStatePattern(duration, trackPatternTimelineMap, trackPatternMapTimeline, flatTimelineMap)

    new() = TrackEventStatePattern(0.0, Map.empty, EventTimeline.empty, Map.empty)

module TrackEventStatePattern =
    let empty = TrackEventStatePattern()
