namespace RMG.TestsF

open RMG.CoreF
open Xunit
open System.IO
open System.Text.Json

type GenerateSong() =
    [<Fact>]
    let write () =
        let start = 5400
        let count = 100

        let options = JsonSerializerOptions()
        options.WriteIndented <- true

        for i = start to start + count - 1 do
            let generatedSong = RandomMusicGenerator.generate ()

            let renderedSong = Rendering.renderSong generatedSong.FlatSong

            let bytes = Midi.writeSong renderedSong

            File.WriteAllBytes($"C:\\Projects\\RMG\\songs\\song_{i}.mid", bytes)
