# RMG

Random Music Generator: generates random, reproducible MIDI songs.

Every song is built from a seed: 4-8 parts, each a sequence of 1-4 sections ordered by a brush (alternation,
ping-pong or random) over up to 3 distinct sections, which parts can share. Every section is played twice.
Each section has a percussion kit and three pitched tracks (chords, melody, bass), each with its own rhythm
and note patterns. A song has its own drums: one main snare (acoustic, electric, clap or sidestick, never mixed; a sidestick
can also join an acoustic or electric snare), and up to four percussion instruments, or none. The kit of a
section is picked from them by drum groups (kick, snare, timekeepers, toms, accents, percussion): kick and snare
always play, plus one or two other groups with only some of their drums.
The same seed always gives the same song.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

Any MIDI player or DAW can play the generated `.mid` files.

## Quick start

```
dotnet run --project Rmg -- --count 5 --seed 42
```

This writes five songs to `./songs` and prints the seed it used.

## Command line

```
dotnet run --project Rmg -- [options]

  -o, --output <dir>   Directory to write the songs to (default: ./songs)
  -n, --count <n>      Number of songs to generate, at least 1 (default: 1)
  -s, --seed <n>       Seed of the randomizer that provides a seed for every song
                       (default: random, printed so the run can be repeated)
  -h, --help           Show this help
```

`--seed` seeds one randomizer, and every song takes its own seed from it, so the same `--seed` and
`--count` always produce the same files. Files are named `song-<song seed>.mid`, and a batch
of `n` songs is a prefix of a batch of `n + k` songs with the same `--seed`.

The seed in a file name is that song's own seed. To recreate one song of a batch, rerun the batch with the
same `--seed` and a `--count` that reaches it.

Exit codes: `0` success, `1` at least one song failed (the failing seed is printed and the rest of the
batch still runs), `2` invalid arguments or output directory.

## Web service

```
dotnet run --project Rmg.WebApi
```

Listens on `http://localhost:5000`:

- `/midi` is a small page to generate and download a song.
- `GET /midi/generate` returns a newly generated random `.mid` file.

The web service always uses a random seed for now.

## How it works

```
seed -> SongGenerator -> Song -> Render -> RenderedSong -> Midi.Write -> .mid
```

- **Timelines and state.** Music is modelled as immutable timelines of events (notes) and of *state*
  (velocity, tempo, scale, chord, octave, ...). State kinds define their own default and how values
  combine, so state from the song, a section, a track group and a track is merged with a single rule.
- **Rhythm.** Rhythm patterns are generated from dyadic ranks: positions in a period ranked by how
  "strong" the beat is, and kept or dropped by a rank-weighted probability that scales with intensity.
- **Generators.** Randomness is expressed as small composable generators that take a seeded
  context, which is what makes every song reproducible.
- **Rendering.** `Render` turns the abstract song into concrete notes (pitch, velocity, duration),
  and `Midi` writes them as a standard MIDI file.

## Project layout

| Project | Contents |
|---|---|
| `Rmg.Core` | Timelines and state, probability generators, song composition, rendering, MIDI writer |
| `Rmg` | Command line application |
| `Rmg.WebApi` | Web service and page |
| `Rmg.Tests` | Tests ([TUnit](https://tunit.dev)) |

## Tests

```
dotnet run --project Rmg.Tests
```

## Licence

[MIT](LICENSE). The licence covers the source code. Generated songs are yours to use however you like.
