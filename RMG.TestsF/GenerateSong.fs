namespace RMG.TestsF

open RMG.CoreF
open Xunit
open System.IO
open System.Text.Json

type GenerateSong() =
    [<Fact>]
    let write () =
        let generatedSong =
            RandomMusicGenerator.generate ()

        let renderedSong =
            Rendering.renderSong generatedSong.FlatSong

        let options = JsonSerializerOptions()
        options.WriteIndented <- true

        File.WriteAllText(
            "C:\\Projects\\RMG\\songs\\rendered-song.json",
            JsonSerializer.Serialize(renderedSong, options)
        )

        File.WriteAllText(
            "C:\\Projects\\RMG\\songs\\generated-song.json",
            JsonSerializer.Serialize(generatedSong, options)
        )

        let bytes = Midi.writeSong renderedSong

        File.WriteAllBytes("C:\\Projects\\RMG\\songs\\song.mid", bytes)
