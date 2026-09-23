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

- `/` is a page that generates a song, plays it in the browser, and lets you mix it: what each track
  plays, chosen or rolled, how loud, whether it is in the song at all, and which tracks are muted or
  soloed.
- `POST /api/songs/generate` returns a generated `.mid` file. It takes a JSON body, or none at all:

  ```json
  {
    "seed": 42,
    "volume": 0.8,
    "tracks": [
      { "channel": 1, "instrument": 40, "volume": 0.5 },
      { "channel": 10, "instrument": 16, "isEnabled": false }
    ]
  }
  ```

  `seed` picks the song, and without it a random seed is used. `volume` is how loud the whole song plays,
  from 0 to 1 of the volume it plays at unasked, and applies on top of every track's own.

  `tracks` says what to play each track with, and every channel left out is played as it was generated.
  Channels are counted from 1, instruments are the 0-127 of General MIDI, a track `volume` is again 0 to
  1, and `isEnabled: false` leaves the track out of the file rather than writing it silent. The body
  above plays channel 1 as a violin at half volume, drops the drums, and takes the rest down to 80%.

  The seed is reported in the `X-Song-Seed` response header and what every channel of the song plays in
  `X-Song-Instruments`, as `<channel>:<instrument>` pairs, switched off or not; the body is the file
  itself, named `song-<seed>.mid` like the ones the command line writes.
- `GET /api/soundfonts` lists the soundfonts the server offers.

The page plays songs with [spessasynth_lib](https://github.com/spessasus/spessasynth_lib), loaded from a
CDN, so the player needs an internet connection the first time it runs.

### The mix

The generator picks an instrument for each of the three pitched tracks at random, and the mixer on the
page lets you take it back: every channel has its instrument on show, the 128 General MIDI ones for the
pitched channels and the drum kits for channel 10. Each track also has a volume, there is one for the
whole song above them, and each can be switched off.

An instrument can also be rolled rather than chosen: every track has a roll of its own, and the button
above them rolls the whole song at once. A roll takes an instrument at random and never lands on the one
already playing; the drums are rolled from their kits, and the pitched tracks from the range in the
settings, which is collapsed at the bottom of the page. That range leaves out the sound effects General
MIDI ends with, which is what the generator picks from as well. Choosing an instrument by hand is not
bound by it.

All of it is part of the song rather than of the listening, so it is written into the file you download,
which is fetched again whenever you change something. Volumes are written as the channel volume the
track plays under, which leaves the dynamics of its notes alone, and a track switched off is not written
at all. The player's own volume is the other kind: it only says how loud the page is.

A change is heard at once, without interrupting what is playing. The seed still decides every note, so
the same seed and the same mix always give the same file.

An instrument only sounds as good as the soundfont behind it, and a soundfont that carries a single drum
kit answers every kit with that one.

### Exporting an MP3

**Export .mp3** renders the song as the mixer has it and encodes it in the browser, so the soundfont you
are listening to is the one you get, and nothing is uploaded. It renders with a synthesizer of its own,
which leaves whatever is playing alone, and it takes the same file the download offers, so the MP3 and
the `.mid` are the same song down to the channel volumes.

Rendering is faster than listening, and the encoding runs in a worker with the page reporting how far
along it is; both together take seconds rather than minutes for a song of a few minutes. The bitrate is
in the settings, at 192 kbps by default.

The encoder is [lamejs](https://www.npmjs.com/package/@breezystack/lamejs), which is **LGPL-3.0** where the rest of
this project is MIT. It is loaded unmodified from a CDN and nothing here is linked against it or derived
from it, which is the arrangement the LGPL is written for; the MP3 patents themselves expired in 2017.

### Soundfonts

Playback needs a soundfont, and none is shipped with the project: the good ones run to tens of megabytes,
and the best known ones are not all redistributable. There are two ways to get one onto the page.

**Bring your own.** Choose a `.sf2`, `.sf3`, `.sfogg` or `.dls` file on the page. It is read in the
browser and never uploaded.

**Put one on the server.** Everything with one of those extensions in `Rmg.WebApi/wwwroot/soundfonts` is
listed in a dropdown on the page and downloaded on demand. The directory is created on start and is
git-ignored; when it is empty the dropdown is hidden and the page just offers the file picker.

Serving a file to a browser is distribution, and this server binds `0.0.0.0`, so anything in that
directory is offered to the whole network. Check the licence before you put a soundfont there:

| Soundfont | Size | Licence | Can you serve it? |
| --- | --- | --- | --- |
| [FluidR3Mono_GM.sf3](https://github.com/musescore/MuseScore/blob/master/share/sound/FluidR3Mono_GM.sf3) | 24 MB | MIT | Yes, keep the copyright notices |
| [MuseScore MS Basic.sf3](https://github.com/musescore/MuseScore/blob/master/share/sound/MS%20Basic.sf3) | 51 MB | MIT | Yes, keep the copyright notices |
| [GeneralUser GS](https://www.schristiancollins.com/generaluser.php) | 32 MB | [GeneralUser GS v2.0](https://github.com/mrbumpy409/GeneralUser-GS/blob/main/documentation/LICENSE.txt) | Yes, and serve your own copy rather than hotlinking |
| [Timbres of Heaven](https://midkar.com/SoundFonts/index.html) | very large | All Rights Reserved | **No**, not without written permission |

FluidR3Mono is the one to start with: it is MIT, the same licence as this project, and the smallest of
the three. Its terms ask only that the acknowledgements and copyright notices travel with it, so keep
`FluidR3Mono_License.md` next to it.

Timbres of Heaven sounds better than any of them, and you may keep a copy in the directory for your own
use on a server nobody else can reach. It "may not be distributed on any site without the express written
permission of Don Allen, or Wayne Knazek, the Midkar.com site owner", so the moment anyone else can reach
the server you need that permission first. This is the same restriction that stopped MuseScore bundling
it.

### Keeping a soundfont in the browser

By default the soundfont you load is kept in this browser's IndexedDB and loads itself on your next
visit, which saves fetching tens of megabytes again. The checkbox on the page turns that off and drops
what was kept; the choice is remembered in `localStorage`. Nothing about it reaches the server.

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
