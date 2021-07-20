// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.IO
open RMG.CoreF

type GenerationInfo =
    {
        Generation: TimeSpan
        Rendering: TimeSpan
        MidiWrite: TimeSpan
    }

let generateSong () : byte array * GenerationInfo =
    let stopWatch = System.Diagnostics.Stopwatch.StartNew()

    let generatedSong = RandomMusicGenerator.generate ()
    let generationTime = stopWatch.Elapsed
    stopWatch.Restart()

    let renderedSong = Rendering.renderSong generatedSong.FlatSong
    let renderingTime = stopWatch.Elapsed
    stopWatch.Restart()

    let bytes = Midi.writeSong renderedSong
    let midiWriteTime = stopWatch.Elapsed
    stopWatch.Stop()

    let timeInfo =
        {
            Generation = generationTime
            Rendering = renderingTime
            MidiWrite = midiWriteTime
        }

    (bytes, timeInfo)

let reduceTimeInfo (func: float seq -> float) (input: GenerationInfo seq) : GenerationInfo =
    {
        Generation =
            input
            |> Seq.map (fun x -> x.Generation.TotalMilliseconds)
            |> func
            |> TimeSpan.FromMilliseconds
        Rendering =
            input
            |> Seq.map (fun x -> x.Rendering.TotalMilliseconds)
            |> func
            |> TimeSpan.FromMilliseconds
        MidiWrite =
            input
            |> Seq.map (fun x -> x.MidiWrite.TotalMilliseconds)
            |> func
            |> TimeSpan.FromMilliseconds
    }

[<EntryPoint>]
let main argv =
    let start = -20
    let count = 20

    //let options = JsonSerializerOptions()
    //options.WriteIndented <- true
    //File.WriteAllText($"C:\\Projects\\RMG\\songs\\rendered-song_{i}.json", JsonSerializer.Serialize(renderedSong, options))
    //File.WriteAllText($"C:\\Projects\\RMG\\songs\\generated-song_{i}.json", JsonSerializer.Serialize(generatedSong, options))

    let times =
        seq { start .. start + count - 1 }
        |> Seq.map
            (fun i ->
                let bytes, timeInfo = generateSong ()
                File.WriteAllBytes($"C:\\Projects\\RMG\\songs\\song_{i}.mid", bytes)
                printfn $"Generated song_{i}, time spent:"
                printfn $"{timeInfo}"
                timeInfo)
        |> Array.ofSeq

    let total = times |> reduceTimeInfo Seq.sum
    let avg = times |> reduceTimeInfo Seq.average

    printfn $"Generated {times.Length} songs, total time spent:"
    printfn $"{total}"
    printfn $"average time:"
    printfn $"{avg}"
    0
