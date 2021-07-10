namespace RMG.CoreF.Composition

open RMG.CoreF

[<Sealed>]
type CompositeTrackEventPattern<'T>
    private
    (
        duration: Duration,
        compositeTrackPatternTimelineMap: TrackEventTimelineMap<CompositeEventPattern<'T> * 'T>,
        compositeTrackPatternMapTimeline: EventTimeline<CompositeTrackEventPattern<'T> * 'T>,
        flatTimeline: TrackEventTimelineMap<'T>
    ) =
    member this.Duration = duration
    member this.CompositeTrackPatternTimelineMap = compositeTrackPatternTimelineMap
    member this.CompositeTrackPatternMapTimeline = compositeTrackPatternMapTimeline
    member this.FlatTimelineMap = flatTimeline

    new(duration: Duration,
        compositeTrackPatternTimelineMap: TrackEventTimelineMapInput<CompositeEventPattern<'T> * 'T>,
        compositeTrackPatternMapTimeline: Timeline<CompositeTrackEventPattern<'T> * 'T>,
        timelineItemMerge: TimelineItemMerge<'T>) =
        let orderedTrackPatternTimelineMap =
            compositeTrackPatternTimelineMap |> TrackEventTimelineMap.fromSeq

        let orderedTrackPatternMapTimeline =
            compositeTrackPatternMapTimeline |> EventTimeline.fromSequence

        let flatTimelineMap =
            seq {
                yield
                    compositeTrackPatternTimelineMap
                    |> TrackEventTimelineMap.fromSeq
                    |> Map.map
                        (fun _ compositeTimeline ->
                            compositeTimeline
                            |> Timeline.map (fun (pattern, item) -> (pattern.FlatTimeline, item))
                            |> Timeline.mergeComposite timelineItemMerge
                            |> EventTimeline.fromSequence)
                    |> Timeline.itemFromSingle

                yield
                    compositeTrackPatternMapTimeline
                    |> Timeline.map (fun (pattern, item) -> (pattern.FlatTimelineMap, item))
                    |> TrackEventTimelineMap.mergeComposite timelineItemMerge
                    |> Timeline.itemFromSingle
            }
            |> TrackEventTimelineMap.merge

        CompositeTrackEventPattern(duration, orderedTrackPatternTimelineMap, orderedTrackPatternMapTimeline, flatTimelineMap)

    new() = CompositeTrackEventPattern(0.0, Map.empty, EventTimeline.empty, Map.empty)

[<NoComparison>]
[<NoEquality>]
type CompositeTrackEventPatternInput<'T> =
    {
        CompositeTrackPatternTimelineMap: TrackEventTimelineMapInput<CompositeEventPattern<'T> * 'T>
        CompositeTrackPatternMapTimeline: Timeline<CompositeTrackEventPattern<'T> * 'T>
    }

module CompositeTrackEventPattern =
    let empty<'T> = CompositeTrackEventPattern()

    let fromInput<'T> (duration: Duration) (merge: 'T * 'T -> 'T) (input: CompositeTrackEventPatternInput<'T>) =
        CompositeTrackEventPattern<'T>(duration, input.CompositeTrackPatternTimelineMap, input.CompositeTrackPatternMapTimeline, merge)
