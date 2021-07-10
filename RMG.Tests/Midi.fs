namespace RMG.TestsF

open RMG.CoreF
open Xunit
open Rendering
open System.IO

type write() =
    [<Fact>]
    let write () =
        let renderedSong =
            {
                Duration = 10.0
                Tempo = seq { { Position = 0.0; Value = 1.0 } } |> EventTimeline.fromSequence
                Tracks =
                    seq {
                        {
                            Code = 0uy
                            IsPercussionTrack = false
                            Items =
                                seq {
                                    {
                                        Position = 1.0
                                        Value = { Offset = 0x40; Velocity = 1.0; Duration = 4.0 }
                                    }

                                    {
                                        Position = 5.0
                                        Value = { Offset = 0x40; Velocity = 1.0; Duration = 1.0 }
                                    }
                                }
                                |> EventTimeline.fromSequence
                        }
                    }
                    |> Seq.toList
            }

        let bytes = Midi.writeSong renderedSong

        File.WriteAllBytes("C:\\Projects\\RMG\\songs\\song1.mid", bytes)
