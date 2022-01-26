namespace RMG.Tests

open RMG.Core
open Xunit
open RMG.Tests.Assertions

type IntToBytes() =
    [<Fact>]
    let simpleNumber () =
        let expected = seq { 0xff }
        let result = Midi.intToBytes (0xffu, None, 8)
        result |> should beEquivalentTo expected

    [<Fact>]
    let complexNumber () =
        let expected =
            seq {
                0xf1
                0xf2
            }

        let result = Midi.intToBytes (0xf1f2u, None, 8)
        result |> should beEquivalentTo expected

    [<Fact>]
    let complexNumberWithLength () =
        let expected = seq { 0xf2 }
        let result = Midi.intToBytes (0xf1f2u, Some 1, 8)
        result |> should beEquivalentTo expected

    [<Fact>]
    let complexNumberWithLengthLonger () =
        let expected =
            seq {
                0xf2
                0xf3
            }

        let result = Midi.intToBytes (0xf1f2f3u, Some 2, 8)
        result |> should beEquivalentTo expected

    [<Fact>]
    let simpleNumberWithByteLength () =
        let expected =
            seq {
                0x01
                0x7f
            }

        let result = Midi.intToBytes (0xffu, None, 7)
        result |> should beEquivalentTo expected
