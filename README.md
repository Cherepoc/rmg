# RMG

Random Music Generator: generates random, reproducible MIDI songs. The same seed always gives the same song in the
same version of RMG; a new version may turn a seed into a different song.

A song has 4-8 parts, each a sequence of 1-4 sections chosen from up to 3 distinct sections (in alternation,
ping-pong or random order), and no section follows itself, not even from one part to the next. Every section
is played twice and has a percussion kit and three pitched tracks (chords, melody, bass), each with its own
rhythm and note patterns. A song plays at its own tempo, from 90 to about 175 BPM, most often 120.

Each song picks its own drums: one main snare (acoustic, electric, clap or sidestick; a sidestick can also
join an acoustic or electric snare) and up to four other percussion instruments. Kick and snare always
play in a section, plus one or two other drum groups (timekeepers, toms, accents, percussion).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Internet access on the first build of `Rmg.WebApi`, which downloads a pinned
  [esbuild](https://esbuild.github.io) for bundling the page (linux-x64; elsewhere pass
  `-p:EsbuildPath=/path/to/esbuild`)

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

Every song takes its own seed from `--seed`, so the same `--seed` and `--count` always give the same
files, and a larger `--count` only adds songs to the end. Files are named `song-<song seed>.mid`.

Exit codes: `0` success, `1` at least one song failed (its seed is printed and the rest still run),
`2` invalid arguments or output directory.

## Web service

```
dotnet run --project Rmg.WebApi
```

Listens on every interface, on port 5000 by default. See [Settings](#settings).

- `/` is a page that generates a song, plays it in the browser and lets you mix it. It loads a song
  and a soundfont as it opens, so you only need to press play.
- `POST /api/songs/generate` returns a `.mid` file. The JSON body is optional:

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

  - `seed` picks the song; without it a random one is used.
  - `volume` is the whole song's volume, 0 to 1, on top of each track's own.
  - `tracks` changes channels 1-16. Instruments are General MIDI 0-127, volumes are 0 to 1, and
    `isEnabled: false` leaves the track out of the file. Channels not listed play as generated.

  The example plays channel 1 as a violin at half volume, drops the drums and sets the song to 80%.

  The response headers carry the seed (`X-Song-Seed`) and every channel's instrument
  (`X-Song-Instruments`, as `<channel>:<instrument>` pairs). The file is named `song-<seed>.mid`.
- `GET /api/soundfonts` lists the soundfonts the server offers, with their licence files.
- `POST /api/tally` records one page event. See [Analytics](#analytics).
- `GET /api/tally/summary` returns the analytics summary, with the dashboard token.

The player ([spessasynth](https://github.com/spessasus/spessasynth_lib)) is loaded from a CDN, so the
page needs an internet connection.

### Autoplay

When a random song ends, the page generates and plays another. The checkbox under the player turns this
off, and the browser remembers the choice. Songs asked for by seed, by link or from the history just stop.

### History

**So far** lists the last fifty songs, newest first. Click one to play it again. It is kept in the browser;
**Clear** deletes it.

### Sharing

A link to a song is the page with its seed:

```
https://your-server/?seed=42
```

The address always follows the song that is playing. **Share** opens the phone's share sheet or copies the
link. The link carries only the seed, not the mix. Seeds are not kept stable across versions, so after an update
an old link, like the history, can play a different song.

### The mix

The mixer shows every channel's instrument, volume and on/off switch, plus a volume for the whole song.
Instruments can be chosen or rolled at random, per track or all at once. Random pitched instruments come
from the range in the settings, which by default leaves out the General MIDI sound effects; drums are
rolled from the drum kits.

The mix is written into the downloaded `.mid`: volumes as channel volumes, and tracks switched off are left
out. The player volume only affects playback on the page. Changes are heard straight away.

A soundfont with only one drum kit plays that kit for every kit choice.

### Exporting an MP3

**Export .mp3** renders the song with the current mix and soundfont and encodes it in the browser, in a
few seconds. Nothing is uploaded. The bitrate is in the settings (192 kbps by default).

The encoder is [lamejs](https://www.npmjs.com/package/@breezystack/lamejs) (LGPL-3.0), loaded unmodified
from a CDN.

### Soundfonts

Playback needs a soundfont. None is in the repository, since good ones are tens of megabytes and not all
can be redistributed.

- **From the server.** Every `.sf2`, `.sf3`, `.sfogg` or `.dls` file in `Rmg.WebApi/wwwroot/soundfonts`
  (or `SoundFontDirectory`) is listed on the page. The first one, or `DefaultSoundFont`, loads
  automatically. With none there, the page only offers the file picker.
- **Your own.** Choose a file in the soundfont section. It is read in the browser and never uploaded.
- **Kept in the browser.** The loaded soundfont is saved in IndexedDB and loads itself next time. The
  checkbox on the page turns this off.

Anything in the soundfont directory is served to everyone who can reach the server, so check the licence
first:

| Soundfont | Size | Licence | Can you serve it? |
| --- | --- | --- | --- |
| [MuseScore_General.sf3](https://ftp.osuosl.org/pub/musescore/soundfont/MuseScore_General/) | 38 MB | MIT | Yes, with its copyright notices |
| [FluidR3Mono_GM.sf3](https://github.com/musescore/MuseScore/blob/master/share/sound/FluidR3Mono_GM.sf3) | 24 MB | MIT | Yes, with its copyright notices |
| [MuseScore MS Basic.sf3](https://github.com/musescore/MuseScore/blob/master/share/sound/MS%20Basic.sf3) | 51 MB | MIT | Yes, with its copyright notices |
| [TimGM6mb.sf2](https://packages.debian.org/source/sid/timgm6mb-soundfont) | 6 MB | GPL-2.0 | Yes, with its licence |
| [GeneralUser GS](https://www.schristiancollins.com/generaluser.php) | 32 MB | [GeneralUser GS v2.0](https://github.com/mrbumpy409/GeneralUser-GS/blob/main/documentation/LICENSE.txt) | Yes, from your own copy, not hotlinked |
| [Timbres of Heaven](https://midkar.com/SoundFonts/index.html) | 285 MB `.7z` | All Rights Reserved | **No**, not without written permission |

MuseScore General is the recommended one. Take the `.sf3`; the `.sf2` is the same soundfont at 206 MB.

Timbres of Heaven sounds best, but may only be served with permission from its authors. You can use it
on a private server, and anyone can load it into the page with the file picker.

#### Licence files

A `.md` or `.txt` file ending in `License`, `Licence` or `Copyright` is the licence for every soundfont
whose name it starts: `MuseScore_General_License.md` covers `MuseScore_General.sf3`. A file named just
`License.md` covers the whole directory. The page links the licence next to the selected soundfont.

## How it works

```
seed -> SongGenerator -> Song -> Render -> RenderedSong -> Midi.Write -> .mid
```

- **Timelines and state.** Music is modelled as immutable timelines of events (notes) and of *state*
  (velocity, tempo, scale, chord, octave, ...). State kinds define their own default and how values
  combine, so state from the song, a section, a track group and a track is merged with a single rule.
  A timeline with nothing in it still keeps its duration: a bar in which nothing plays is still a bar.
  "No content" and "no duration" are separate checks: `Count == 0` for events, `IsDefault` for state
  and for the maps, and `Duration == 0` for length. Only zero-duration timelines are dropped when
  timelines are put one after another.
- **State kinds.** A kind's name is its identity: creating a second kind with a taken name throws.
  Every kind has a scope. Composition state, such as the rhythm settings, is read only while the
  song is generated; render state, such as velocity and the pitch offsets, is what the song keeps
  and `Render` reads. Some kinds are shared, because every track must see the same value at the same
  time: the key, the scale, the tempo and the chord shape. Only the layers all tracks share (the song,
  a section, a bar) may set them. A builder made for a layer only some tracks see
  (`new StateMapBuilder(layer, perTrack: true)`) checks every map it makes and throws if a shared
  kind is in it.
- **Pools.** Where a layer should pick one of several values rather than add to one, such as a chord
  shape, the layers add entries to a pool and draws to an index into it. A pool lists its entries in
  the order its layers are merged, and the index gathers around 0, so the first entries are the
  likeliest: a section's pool starts with the song's entries and adds its own after them.
- **Tracing.** To see why a value came out as it did, run the generation inside a `StateTrace`. It
  records every track's state for each bar pattern and the pool and index behind each chord shape, and
  `StateMap.Explain(kind)` lists what each layer contributed, such as the snare's +1 to its rhythm and
  the song's and a section's steps. Without a trace, states keep no record and it costs nothing.
- **Rhythm.** Rhythm patterns are generated from dyadic ranks: positions in a period ranked by how
  "strong" the beat is, and kept or dropped by a rank-weighted probability. Every layer, from the song
  to a bar, may move a track's rhythm settings a step, each with its own chance (`RhythmLayers`), so a
  drum keeps the rhythm it is given in about half of the bars. A period can be a tuplet's, such as a
  triplet's, which is not exact in binary, so rhythm positions are snapped to a grid of 1024 · 3 · 5 · 7
  ticks per beat (`TimelineGrid`), on which the dyadic subdivisions and the triplets, quintuplets and
  septuplets fall exactly; two ways to the same moment then give the same position.
- **Generators.** Randomness is expressed as small composable generators that take a seeded
  context, which is what makes every song reproducible.
- **Rendering.** `Render` turns the abstract song into concrete notes (pitch, velocity, duration),
  and `Midi` writes them as a standard MIDI file.
- **Scales.** A song is in one scale from start to end, in a random key. The scale is drawn from a
  weighted table of 7-note scales (`Scales`): natural minor and major are the most common (30% each),
  dorian and mixolydian occasional (12% each), and harmonic minor, phrygian and lydian rare (5–6%).
  Chord roots move by scale steps and chord heights sit between the qualities a 7-note scale gives,
  so scales of other sizes, such as pentatonic, are not in the table.
- **Chords.** A chord is the heights of its notes above the chord root, as fractions of an octave in
  pitch, so the same chord works in any scale: `Render` snaps every height to the nearest note of the
  scale, lowest first, and a note already taken goes to a free neighbour or is dropped. A height can
  sit between two qualities, such as a third between minor and major, and the scale decides, so a
  triad is Cm, D° or Eb in C minor and C major in C major pentatonic. Shapes come from a table ranked
  by unconventionality, from triads (0) to clusters and polychords (5); a song draws where its chords gather,
  how far and how evenly they stray and which way they lean, and a section moves that by up to a rank. A voicing
  step then inverts or opens the shape, and `Render` moves the whole chord into the track's range.
  The chord root, unlike the shape, is a fraction of the scale's note count, as it moves along the
  scale.
- **Progressions.** Every section has a home, the tonic most often (60%) and otherwise the relative
  key, IV or V, and a 4-bar progression of chord roots around it (`Progressions`): bar 1 is the home
  chord, bar 2 moves away, bar 3 prepares the cadence (ii or IV) and bar 4 is the cadence, which
  resolves to bar 1 as the pattern repeats. A root is drawn by how strongly the previous one leads to
  it, falling a fifth the most, and by how well it suits its bar. The cadence chords follow from the
  chords the scale builds around the home, so they suit the mode: V where it is major, ♭VII in
  mixolydian or natural minor, ♭II in phrygian, IV or ♭VII in dorian. Roots stay within 3 steps of the
  home, so the bass keeps to one register. The more unconventional a section's harmony, the looser it
  keeps to these rules, until every root is as likely. Where the chord on the fifth is minor and the
  seventh step sits a whole step below the home, as in natural minor, dorian and mixolydian, a
  cadence on the fifth raises that seventh for the bar (`StateKinds.RaisedScaleSteps`), so the chord
  turns major and leads home as in harmonic minor. The home bar plays a plain chord, a triad or a mild
  colour, and the cadence bar a chord with pull, a seventh most often, then a suspended chord, a triad
  or a ninth; a strange song keeps its strangeness at the cadence. The bars between pick from the
  section's pool of chord shapes.
- **Instruments.** Each pitched track has a role (`InstrumentRoles`): the chords are played by pianos,
  organs, guitars, strings or pads, the melody by keys, mallets, guitars, strings, brass, reeds, pipes
  or leads, and the bass by basses, with a few unusual choices weighted low. The melody never plays
  the chords' instrument. Drums and sound effects are never picked.
- **Offsets are rounded before they are summed.** Pitch offsets such as the chord root or the chord
  note are collections of fractional values, one from each layer (section, bar, note, ...). `Render`
  turns each value into a whole number of scale steps first and adds up the results, so it computes
  `round(a * n) + round(b * n)` rather than `round((a + b) * n)`. This is deliberate: a layer then
  shifts the tonality of everything beneath it by a whole number of steps, the same for every note of
  the pattern. If the values were summed first, the same layer offset could move one note by a step
  and leave the next one alone, depending on the note's own fraction. Each value goes to the nearest
  step (halves away from zero), so every step covers the same range and an offset of less than half a
  step either way leaves the note where it is. How often a track moves is set by scaling its offsets,
  not by the rounding.

## Project layout

| Project | Contents |
|---|---|
| `Rmg.Core` | Timelines and state, probability generators, song composition, rendering, MIDI writer |
| `Rmg` | Command line application |
| `Rmg.WebApi` | Web service and page |
| `Rmg.Tests` | Tests ([TUnit](https://tunit.dev)) |

The page's sources are in `Rmg.WebApi/Client`. Every build bundles and minifies them with esbuild into
`Rmg.WebApi/wwwroot`, which is build output apart from `soundfonts`: one JS and one CSS file for each
page, plus the MP3 worker, with source maps, in `wwwroot/assets/<hash>/`. The folder is named by a
hash of its contents, so the server has browsers keep the bundles for good and check the HTML every
time, and a changed bundle is always a new address. Each page carries the build's `git describe` in
`<meta name="version">`. The spessasynth libraries and the MP3 encoder are not bundled; they come
from the CDN as before.

## Tests

```
dotnet run --project Rmg.Tests
```

## Settings

Defaults are in [`Rmg.WebApi/appsettings.json`](Rmg.WebApi/appsettings.json). Environment variables
prefixed `RMG_` override them, and command-line options override both:

```
RMG_PORT=8080 dotnet run --project Rmg.WebApi
dotnet run --project Rmg.WebApi -- --port 8080
```

| Setting | |
|---|---|
| `Port` | Port to listen on. Default `5000` |
| `Address` | `any`, `loopback` or an IP address. Default `any` |
| `KnownProxies` | Reverse proxies to trust, comma separated. Loopback is always trusted |
| `SoundFontDirectory` | Where the soundfonts are. Blank means `wwwroot/soundfonts` |
| `DefaultSoundFont` | Soundfont the page loads first. Blank means the first one |
| `AnalyticsEnabled` | Whether to collect analytics. Default `true` |
| `StateDirectory` | Where the analytics are stored. Blank means beside the app |
| `DashboardToken` | Token for the analytics dashboard. Blank turns the dashboard off |
| `RetentionDays` | How long events are kept. Default `180` |
| `EventsPerVisitorPerDay` | Events accepted from one visitor per day. Default `300` |

An invalid value stops the server at start with a message saying which one.

Keep `DashboardToken` out of `appsettings.json`, which is in the repository; set it in the environment.
Machine-specific values also belong in the environment, since a deploy replaces `appsettings.json`.

## Analytics

The page records timings and how far people get, to find slow spots. `POST /api/tally` accepts an event
name from a fixed list and up to four numbers, stored in SQLite in the state directory. Nothing goes to
third parties.

- **Anonymous.** A visitor is an HMAC of their address and browser with a secret that changes daily,
  cut to 16 characters. The address is never stored, and no analytics data is kept in the browser.
- **Opt-out.** Browsers sending Global Privacy Control or Do Not Track send nothing.
- **Events:** `page_open`, `song_generated`, `song_failed`, `soundfont_ready`, `soundfont_failed`,
  `audio_ready`, `play`, `listened`, `mix_changed`, `download_mid`, `export_mp3` and `shared`. Others
  are dropped. Soundfonts are recorded by size and source, not name.

The script is `tally.js` and the path `/api/tally` because blocklists block `analytics.js` and
`/api/events` by name. Opting out still works as above.

Events older than `RetentionDays` are deleted on start. `AnalyticsEnabled: false` turns it all off.

### The dashboard

`/dashboard.html` shows visitors, load times, how far people got and which seeds were listened to most.
It needs `DashboardToken` (`RMG_DASHBOARDTOKEN`) and sends it as `Authorization: Bearer <token>`;
without a token the summary is not served.

```
RMG_DASHBOARDTOKEN=$(openssl rand -hex 24) ./deploy.sh --host your-vps
```

Put it in `deploy.env` to keep it between deploys. On the server the analytics live in `/var/lib/rmg`,
which deploys leave alone.

## Deploying

`deploy.sh` builds, tests, publishes and installs the app on a VPS as a systemd service. Copy
`deploy.env.example` to `deploy.env`, set your server in it, and run the first deploy with:

```
./deploy.sh --install
```

`--install` creates the `rmg` account, the directory and the systemd unit; later deploys are just
`./deploy.sh`. A deploy fails if `/api/soundfonts`, or any bundle the pages point at, does not answer after the
restart. Superseded bundle folders stay on the server for 7 days, for pages opened before the deploy.

The app is published as a single self-contained executable for `linux-x64` (about 50 MB), with only
`wwwroot` and the native SQLite library beside it, so the VPS needs no .NET. Set
`RMG_RUNTIME=linux-arm64` for ARM. Failing tests stop the deploy unless you pass `--skip-tests`.

The VPS downloads MuseScore General and TimGM6mb with their licences, if not already there
(`RMG_SOUNDFONTS` changes the list). Deploys never delete the soundfont directory.

The VPS needs `systemd`, `curl`, `rsync`, and SSH key access for an account that can `sudo systemctl`.

| Option | |
|---|---|
| `--host <user@host>` | Where to deploy, if not in `deploy.env` |
| `--path <dir>` | Where the app goes. Default `/opt/rmg` |
| `--service <name>` | Name of the systemd unit. Default `rmg` |
| `--port <port>` | Port to listen on. Default `5000` |
| `--address <where>` | `loopback`, `any` or an IP. Default `loopback` |
| `--install` | Create the account, directory and unit first |
| `--skip-tests` | Publish without testing |
| `--no-soundfont` | Leave the soundfont directory alone |

Re-run with `--install` after changing the port or address. Ports below 1024 work too; the unit grants
the permission for them.

### Behind a reverse proxy

The app has no authentication, so deploys listen on loopback only and expect a proxy in front. A complete
Caddyfile:

```
rmg.example.com {
    reverse_proxy 127.0.0.1:5000
}
```

The app trusts `X-Forwarded-*` headers only from a proxy on the same machine. Name a proxy on another host
with `RMG_KNOWNPROXIES=10.0.0.5` (comma separated for more).

To serve it under a path, use `handle_path`, which strips the prefix (`handle` does not):

```
example.com {
    handle_path /rmg/* {
        reverse_proxy 127.0.0.1:5000
    }
}
```

`--address any` listens on every interface instead; put a firewall in front of it.

## Licence

[MIT](LICENSE) for the source code. Generated songs are yours to use however you like.
