namespace RMG.Core.Generation

open RMG.Core

type TrackStyleDefinition = { Identity: TrackStyleIdentity; PatternDurationRankRange: int * int }

and TrackStyleIdentity =
    | SharedTrackStyleIdentity of TrackNumber
    | NoteTrackStyleIdentity of TrackNumber
    | MultiTrackStyleIdentity of TrackStyleDefinition list

type SongStyleDefinitionInput =
    {
        SharedStyleCount: int;
        PitchInstrumentStyleCount: int;
        PercussionInstrumentStyleCount: int
    }

module TrackStyleDefinition =
    let generateSongStyleDefinition (input: SongStyleDefinitionInput) : TrackStyleDefinition =
        let mutable styleIndex: TrackNumber = 0

        let nextStyleIndex () : int =
            let styleIndexTemp = styleIndex
            styleIndex <- styleIndex + 1
            styleIndexTemp

        {
            PatternDurationRankRange = 2, 3;
            Identity =
                seq {
                    yield!
                        seq { 1 .. input.SharedStyleCount }
                        |> Seq.map
                            (fun _ ->
                                {
                                    Identity = nextStyleIndex () |> SharedTrackStyleIdentity;
                                    PatternDurationRankRange = 1, 2
                                })

                    yield!
                        seq { 1 .. input.PitchInstrumentStyleCount }
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
                                seq { 1 .. input.PercussionInstrumentStyleCount }
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
