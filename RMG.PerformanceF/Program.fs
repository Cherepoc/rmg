open BenchmarkDotNet.Running
open RMG.PerformanceF

[<EntryPoint>]
let main argv =
    let switch =
        BenchmarkSwitcher [| typeof<NoteOffsetBasedPatternBenchmark> |]

    switch.Run argv |> ignore
    0
