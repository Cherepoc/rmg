# RMG

Random Music Generator: generates random, reproducible MIDI songs. The same seed always gives the same song in the
same version of RMG; a new version may turn a seed into a different song.

A song has an unconventionality, from 0, the plainest, to 1, the wildest, and a value of it for each facet of the
music (its feel, groove, fills, form, chords, progression, scale, melody and sound), drawn around the song's, so that
a song can be plain in one way and wild in another. Every choice leans by its facet: a plain song is in one of the
forms songs are written in, in four, in major or minor, its band playing straight through; a wild one takes any
meter, feel, scale or chord, changes key and tempo, plays polymeters and solos.

A song is a sequence of sections, most often verse, chorus and bridge with a pre-chorus now and then, now and then an
intro and an outro of their own. Its parts are drums and up to eight pitched parts: chords, melody, bass, a pad, a
counter-melody, a riff (with a twin that harmonises it), a rhythm part and a solo part. Sections leave parts out,
bring them in phrase by phrase, hold some back until a later section, double lines, play solos and fills, and change
key or tempo, each by a chance of its own.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Internet access on the first build of `Rmg.WebApi`, which downloads a pinned
  [esbuild](https://esbuild.github.io) for bundling the page (linux-x64; elsewhere pass
  `-p:EsbuildPath=/path/to/esbuild`)

Any MIDI player or DAW can play the generated `.mid` files.

## Quick start

```
dotnet run --project Rmg -- --count 5 --seed 1A
```

This writes five songs to `./songs` and prints the seed it used.

## Command line

```
dotnet run --project Rmg -- [options]

  -o, --output <dir>   Directory to write the songs to (default: ./songs)
  -n, --count <n>      Number of songs to generate, at least 1 (default: 1)
  -s, --seed <seed>    Seed of the randomizer that provides a seed for every song, in letters
                       and digits as the page writes seeds (default: random, printed so the
                       run can be repeated)
  --version            Print RMG's songs' version and commit
  --fingerprint        Print the fingerprint of the songs of the corpus's seeds, which changes
                       when the songs do
  -h, --help           Show this help
```

Every song takes its own seed from `--seed`, so the same `--seed` and `--count` always give the same
files, and a larger `--count` only adds songs to the end. Files are named `song-<song seed>.mid`.

Exit codes: `0` success, `1` at least one song failed (its seed is printed and the rest still run),
`2` invalid arguments or output directory.

## Versions

The songs' version, such as `0.5.001`, is in `VERSION`. `deploy.sh` compares the songs' fingerprint
(`--fingerprint`) with the one recorded there and, when the songs have changed, bumps the last number and
commits the file; raise the first two by hand for a change worth naming. The version is written into every
MIDI file, with the commit and the seed, shown on the page, and stored with the page's analytics, so that a
seed listened to or rated is known by the songs it was heard as. The deploy refuses a working tree with
changes that are not committed.

## Web service

```
dotnet run --project Rmg.WebApi
```

Listens on every interface, on port 5000 by default. See [Settings](#settings).

- `/` is a page that generates a song, plays it in the browser and lets you set and mix it.
- `POST /api/songs/generate` returns a `.mid` file. Every field of the JSON body is optional; an amount is a step from
  0 to 63, and anything left out is the song's own:

  ```json
  {
    "seed": "1A",
    "unconventionality": 40,
    "facets": { "chords": 50 },
    "tempo": 3,
    "parts": {
      "melody": { "instrument": 40, "volume": 32 },
      "drum": { "isOn": false }
    },
    "volume": 50
  }
  ```

  The seed is in letters and digits, all 64 bits of it; without one a random song is made. A part, by its role's name,
  may be given in or out (`plays`), an instrument (General MIDI 0-127), a volume and a pan, or left out of the file
  (`isOn: false`); `drumSetup` and `drumGroups` do the same for the drums, and `key`, `tempo` and `meter` give those
  (the tempos and meters by their place in `GET /api/songs/options`). The response carries the seed (`X-Song-Seed`),
  the songs' version (`X-Song-Version`) and everything the song drew or was given, with every part's channel
  (`X-Song-Settings`). The file is named `song-<seed>.mid`. The server makes as many songs at once as it has cores;
  the rest wait their turn, a client at a time, and are turned away with 429 or 503 when too many wait.
- `GET /api/songs/options` lists the tempos and meters a song may be given.
- `GET /api/version` returns the songs' version and commit.
- `GET /api/soundfonts` lists the soundfonts the server offers, with their licence files.
- `POST /api/tally` records one page event, and `GET /api/tally/summary` returns the analytics, with the dashboard
  token. See [Analytics](#analytics).

The player ([spessasynth](https://github.com/spessasus/spessasynth_lib)) is loaded from a CDN, so the page needs an
internet connection.

### The page

- **Playing.** The page loads a song and a soundfont as it opens, so you only need to press play. When a random song
  ends it plays another, unless the autoplay switch beside **Generate Next** is off. **Previous** goes back down the
  list of songs so far, and a seed typed under the downloads plays that song.
- **Song settings.** The unconventionality and every facet, the tempo, key and meter, every part (as drawn, in or
  out) and the drums can be given or left to the song. Changing one remakes the song from the same seed, going on
  from the same place. What changes only how it sounds (a part's instrument, volume and pan, the drum groups, the
  volume) is heard at once.
- **Rating.** Like or dislike a song; the ratings go with the analytics, by the songs' version.
- **So far** lists the last fifty songs, newest first, kept in the browser; a song gone back to keeps its place.
- **Sharing.** The address always follows the song and its settings, `?song=<seed>&settings=<settings>`, so it is
  itself a link; **Share** opens the share sheet or copies it. Seeds are not kept stable across versions, so after
  an update an old link can play a different song.
- **Downloads.** The `.mid` carries the mix. **Download .mp3** renders the song, at the bitrate beside it, with the mix and the soundfont in the
  browser, with [lamejs](https://www.npmjs.com/package/@breezystack/lamejs) (LGPL-3.0, from a CDN); nothing is uploaded.

## Soundfonts

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

### Licence files

A `.md` or `.txt` file ending in `License`, `Licence` or `Copyright` is the licence for every soundfont
whose name it starts: `MuseScore_General_License.md` covers `MuseScore_General.sf3`. A file named just
`License.md` covers the whole directory. The page links the licence next to the selected soundfont.

## How it works

```
seed -> SongGenerator -> Song -> Render -> RenderedSong -> Midi.Write -> .mid
```

- **State decides, `Realizer` realizes.** Music is immutable timelines of events (notes) and of state (velocity,
  tempo, key, scale, chord, a note's scale step, ...), layered from the song to a section, a track, a bar and a note;
  every state kind says how its layers combine. Generation decides the notes as this state; `Realizer` only chooses
  their register from what came before (a chord's voicing, a line's octave), and `Render` plays them.
- **Stages and streams.** `SongGenerator` runs the stages in turn: the tracks, the structure, every section
  (`SectionGenerator`, `PatternGenerator`), the form (`SongFormGenerator`), and then edits of the song put together,
  such as the fills, the lines placed over the whole song, the key changes, the polymeters, the solos and the
  expression. Every stage, section and decision draws from a random sequence of its own (`SongStream`), so that a
  change to one leaves what the others draw as it was; `CorpusFingerprintTest` shows when the songs change.
- **Conventionality.** A choice's options carry their weights at the plain end, the middle and the wild end
  (`ByConvention`), and lean by energy or anything else through their odds (`Tilt`); a lean is data on the option.
- **Tracing.** Inside a `StateTrace`, generation records what it decided as values at named points (`TracePoints`),
  which the tests read; without one it records nothing.

The principles the code keeps to are in [`CLAUDE.md`](CLAUDE.md), and the plans and the decisions behind them in
[`ROADMAP.md`](ROADMAP.md).

## Project layout

| Project | Contents |
|---|---|
| `Rmg.Core` | Timelines and state, probability generators, song composition, rendering, MIDI writer |
| `Rmg` | Command line application |
| `Rmg.WebApi` | Web service and page |
| `Rmg.Tests` | Tests ([TUnit](https://tunit.dev)) |
| `Rmg.Tests.Page` | The page driven in a browser ([Playwright](https://playwright.dev/dotnet/)) |

The page's sources are in `Rmg.WebApi/Client`. Every build bundles and minifies them with esbuild into
`Rmg.WebApi/wwwroot`, which is build output apart from `soundfonts`: one JS and one CSS file for each
page, plus the MP3 worker, with source maps, in `wwwroot/assets/<hash>/`. The folder is named by a
hash of its contents, so the server has browsers keep the bundles for good and check the HTML every
time, and a changed bundle is always a new address. Each page carries the build's `git describe` in
`<meta name="version">`. The spessasynth libraries and the MP3 encoder are not bundled; they come
from the CDN as before.

## Tests

```
dotnet run --project Rmg.Tests -c Release
```

Tests that look at whole songs share them: `TestCorpus` generates each seed once, with its rendering and the trace of
its generation. A song's `Map` says where its intro, sections and ending are, so tests read positions from it, and
the trace's values (`StateTraceEntry.Value`), not its words. Explicit tests report what the corpus measures, such as
how the solos play (`SoloTest`) or how the sections differ (`SectionDynamicsTest`); run one class, its reports
included, with:

```
dotnet run --project Rmg.Tests -c Release -- --treenode-filter "/*/*/SoloTest/*" --output Detailed
```

The page is tested as it is used: `Rmg.Tests.Page` starts the server as it is built, on a port and with analytics of
its own, and drives its pages in Playwright's headless Chromium, which its first run installs into
`~/.cache/ms-playwright`. The page loads its synthesizer from the CDN, so the tests want the network. They take about
15 s:

```
dotnet run --project Rmg.Tests.Page -c Release
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
  `audio_ready`, `play`, `listened`, `mix_changed`, `download_mid`, `export_mp3`, `shared` and `rated`, each
  with the song's seed, version and settings. Others are dropped. Soundfonts are recorded by size and source,
  not name.

The script is `tally.js` and the path `/api/tally` because blocklists block `analytics.js` and
`/api/events` by name. Opting out still works as above.

Events older than `RetentionDays` are deleted on start. `AnalyticsEnabled: false` turns it all off.

### The dashboard

`/dashboard.html` shows visitors, load times, how far people got, the songs listened to and rated by version
and by how plain or wild they are, and the server: how long songs took to make and how many waited.
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
