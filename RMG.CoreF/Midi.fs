namespace RMG.CoreF

open System
open RMG.CoreF.Rendering

module Midi =
    let private ticksPerQuarterNote = 96u

    type private MidiEvent = { Delta: uint32; Bytes: seq<byte> }

    let rec intToBytes (value: uint32, length: int option, byteLength: int): seq<byte> =
        let mask = 0xffffffffu >>> (32 - byteLength)
        let shiftedValue = value >>> byteLength

        let nextLength =
            match length with
            | Some length -> Some(length - 1)
            | _ -> None

        let shouldContinue =
            match nextLength with
            | Some length -> length > 0
            | None -> shiftedValue > 0u

        seq {
            if shouldContinue
            then yield! intToBytes (shiftedValue, nextLength, byteLength)

            yield byte (value &&& mask)
        }

    let private calculateAbsoluteDelta (value: Position): uint32 =
        uint32 (round (value * (double ticksPerQuarterNote)))

    let private channelMidiEventHeader (channel: byte, eventType: byte): byte = (eventType <<< 4) ||| channel

    let private noteOn (channel: byte, note: byte, volume: Volume): seq<byte> =
        seq {
            channelMidiEventHeader (channel, 0x09uy)
            note
            byte (volume * 127.0)
        }

    let private noteOff (channel: byte, note: byte): seq<byte> =
        seq {
            channelMidiEventHeader (channel, 0x08uy)
            note
            0x40uy
        }

    let private programChange (channel: byte, program: byte) =
        seq {
            channelMidiEventHeader (channel, 0x0cuy)
            program
        }

    let private timeSignature (numerator: byte, denominator: byte) =
        seq {
            0xffuy
            0x58uy
            0x04uy
            numerator
            byte (Math.Log(double denominator, 2.0))
            0x18uy
            0x08uy
        }

    let private tempo (tempo: Tempo) =
        seq {
            0xffuy
            0x51uy
            0x03uy
            yield! intToBytes (uint32 (0.5 * 1_000_000.0 / tempo), Some 3, 8)
        }

    let private endOfTrack =
        seq {
            0xffuy
            0x2fuy
            0x00uy
        }

    let private variableLength (value: uint32) =
        let bytes =
            intToBytes (value, None, 7) |> Seq.toArray

        seq {
            yield! bytes
                   |> Seq.take (bytes.Length - 1)
                   |> Seq.map (fun x -> x ||| 0x80uy)
            yield! bytes |> Seq.skip (bytes.Length - 1)
        }

    let writeSong (song: RenderedSong): array<byte> =
        let durationDelta = calculateAbsoluteDelta song.Duration

        let writeTrack (events: seq<MidiEvent>) =
            let trackBytes =
                seq {
                    yield { Delta = 0u; Bytes = Seq.empty }
                    yield! events
                    yield { Delta = durationDelta
                            Bytes = endOfTrack }
                }
                |> Seq.sortBy (fun item -> item.Delta)
                |> Seq.pairwise
                |> Seq.collect (fun (previousItem, item) ->
                    seq {
                        yield! variableLength (item.Delta - previousItem.Delta)
                        yield! item.Bytes
                    })
                |> Seq.toArray

            seq {
                0x4Duy
                0x54uy
                0x72uy
                0x6Buy
                yield! intToBytes (uint32 trackBytes.Length, Some 4, 8)
                yield! trackBytes
            }

        let writeSystemTrack =
            let events =
                seq {
                    { Delta = 0u
                      Bytes = timeSignature (4uy, 4uy) }
                    yield! song.Tempo
                           |> Seq.map (fun item ->
                               { Delta = calculateAbsoluteDelta item.Position
                                 Bytes = tempo item.Value })
                }

            writeTrack events

        let writeNoteTrack (track: RenderedTrack, index: byte) =
            let fixedIndex = if index >= 10uy then index + 1uy else index
            let events =
                seq {
                    yield { Delta = 0u
                            Bytes = programChange (fixedIndex, byte track.Code) }
                    yield! track.Items
                           |> Seq.collect (fun item ->
                               let noteOffPosition =
                                   calculateAbsoluteDelta (item.Position + item.Value.Duration)

                               seq {
                                   { Delta = calculateAbsoluteDelta item.Position
                                     Bytes = noteOn (fixedIndex, byte item.Value.Offset, item.Value.Volume) }
                                   { Delta = if noteOffPosition <= durationDelta then noteOffPosition else durationDelta
                                     Bytes = noteOff (fixedIndex, byte item.Value.Offset) }
                               })
                }

            writeTrack events

        seq {
            0x4Duy
            0x54uy
            0x68uy
            0x64uy
            0x00uy
            0x00uy
            0x00uy
            0x06uy
            0x00uy
            0x01uy
            yield! intToBytes (uint32 (song.Tracks.Length + 1), Some 2, 8)
            yield! intToBytes (ticksPerQuarterNote, Some 2, 8)
            yield! writeSystemTrack
            yield! song.Tracks
                   |> Seq.indexed
                   |> Seq.collect (fun (index, track) -> writeNoteTrack (track, byte index))
        }
        |> Seq.toArray
