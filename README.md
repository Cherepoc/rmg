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

Listens on every interface, on port 5000 unless something says otherwise. See [Settings](#settings).


- `/` is a page that generates a song, plays it in the browser, and lets you mix it: what each track
  plays, chosen or rolled, how loud, whether it is in the song at all, and which tracks are muted or
  soloed. It has a song and a soundfont before you touch it: opening the page generates one and fetches
  the other, both at once, so the only thing left to do is press play. The soundfont section keeps to
  its own line until you open it.
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
- `GET /api/soundfonts` lists the soundfonts the server offers, each with the licence file beside it.
- `POST /api/tally` takes one thing the page did. See [Analytics](#analytics).
- `GET /api/tally/summary` reports them, to whoever holds the dashboard token.

The page plays songs with [spessasynth_lib](https://github.com/spessasus/spessasynth_lib), loaded from a
CDN, so the player needs an internet connection the first time it runs.

### One song after another

A rolled song that runs out is followed by another: the page generates one and plays it, the way a video
site goes on to the next. **When a random song ends, generate another and play it**, under the player,
turns that off, and this browser remembers it.

Only a rolled song leads to another. One asked for by seed, or arrived at by a link, is the song that was
asked for, so it ends where it ends. Whatever plays next arrives with its own mix, as a rolled song
always does, so the mixer is not carried over.

### Coming back to a song

**So far** is every song this page has had, newest first, whichever way it came about: rolled, typed,
followed from a link, or rolled by the one before it running out. Click one and you get that song back.

A seed already on the list is not put on it twice, so coming back to a song leaves the list as it was.
A song from the list is one asked for by seed, like any other, so it arrives as it was generated rather
than as the mixer was left, and it ends where it ends rather than leading to another.

The list is kept in this browser, so it is still there on the next visit, up to the last fifty songs.
**Clear** is rid of it, here and in the browser both: it is the one thing this page keeps that says what
anybody listened to.

### Sharing a song

A song is its seed, so a link to one is this page with the seed on it:

```
https://your-server/?seed=42
```

Follow it and you get that song rather than a rolled one. The address on the page follows whatever is
playing, so it is always a link to what you are hearing, and a refresh gives the same song back. **Share**
hands it over: the share sheet on a phone, the clipboard everywhere else, and the link itself in the
status line if the browser will allow neither.

The link carries the seed and nothing else, so what arrives is the song as it was generated, not the
mixer as you left it. A link with something that is not a seed on it says so and plays another song.

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

**Let the page take one.** Whatever the server offers first, in the order `GET /api/soundfonts` lists
them, is fetched as the page opens and needs no asking. A soundfont this browser kept from a previous
visit is preferred to it, since that costs nothing to load.

**Bring your own.** Open the soundfont section and choose a `.sf2`, `.sf3`, `.sfogg` or `.dls` file, or
another of the ones the server offers. Your own file is read in the browser and never uploaded.

**Put one on the server.** Everything with one of those extensions in `Rmg.WebApi/wwwroot/soundfonts` is
listed in a dropdown on the page and downloaded on demand. The directory is created on start and is
git-ignored; when it is empty the dropdown is hidden and the page just offers the file picker.

Serving a file to a browser is distribution, and this server binds `0.0.0.0`, so anything in that
directory is offered to the whole network. Check the licence before you put a soundfont there:

| Soundfont | Size | Licence | Can you serve it? |
| --- | --- | --- | --- |
| [MuseScore_General.sf3](https://ftp.osuosl.org/pub/musescore/soundfont/MuseScore_General/) | 38 MB | MIT | Yes, keep the copyright notices |
| [FluidR3Mono_GM.sf3](https://github.com/musescore/MuseScore/blob/master/share/sound/FluidR3Mono_GM.sf3) | 24 MB | MIT | Yes, keep the copyright notices |
| [MuseScore MS Basic.sf3](https://github.com/musescore/MuseScore/blob/master/share/sound/MS%20Basic.sf3) | 51 MB | MIT | Yes, keep the copyright notices |
| [GeneralUser GS](https://www.schristiancollins.com/generaluser.php) | 32 MB | [GeneralUser GS v2.0](https://github.com/mrbumpy409/GeneralUser-GS/blob/main/documentation/LICENSE.txt) | Yes, and serve your own copy rather than hotlinking |
| [Timbres of Heaven](https://midkar.com/SoundFonts/index.html) | 285 MB `.7z` | All Rights Reserved | **No**, not without written permission |

MuseScore General is the one to start with, and is what the page names as its default: it is MIT, the
same licence as this project, and it is FluidR3Mono with the weaker instruments replaced, so it sounds
better for the 14 MB it adds. Take the `.sf3`, not the `.sf2`, which is the same soundfont uncompressed
at 206 MB. Its terms ask that the acknowledgements and copyright notices travel with it, which is what
the licence files below are for.

#### Licence files

A soundfont ships its notice beside it, and the library picks that up: a file ending in `License`,
`Licence` or `Copyright` with a `.md` or `.txt` extension is read as the licence for every soundfont
whose name it starts. `MuseScore_General_License.md` is the licence for `MuseScore_General.sf3`, and
`FluidR3Mono_License.md` covers `FluidR3Mono_GM.sf3` as well. A file called nothing but `License.md`
stands for the whole directory, and a more particular one always wins over it.

The page links whatever it finds next to the soundfont in the dropdown, so the notice reaches whoever
downloads the file. Download the licence along with the soundfont and this takes care of itself;
`README` and `Changelog` files beside it are left alone.

Timbres of Heaven sounds better than any of them, and you may keep a copy in the directory for your own
use on a server nobody else can reach. It "may not be distributed on any site without the express written
permission of Don Allen, or Wayne Knazek, the Midkar.com site owner", so the moment anyone else can reach
the server you need that permission first. This is the same restriction that stopped MuseScore bundling
it. The page points visitors at it all the same, since linking is not distributing and Midkar gives it
away freely: it downloads as a 285 MB `.7z`, and the `.sf2` inside is what the file picker takes.

### Changing the soundfont while it plays

Loading a sound bank puts every channel back to where it started, so a soundfont swapped under a song
already playing would leave the whole thing sounding like a piano while the mixer still showed what it
was meant to be. The mix is put back afterwards: every channel is told what it plays, not only the ones
chosen on the page, since what the song itself asked for is as lost as anything picked by hand, and the
song will not say it again until it comes round. Only a choice made on the page is locked, so a channel
the song owns is still the song's to set when it starts over.

### When the audio starts

A browser allows no audio until the page has been interacted with, and a synthesizer built any earlier
never reports itself ready. So the page fetches the song and the soundfont straight away, which needs no
permission, and hands both to the audio stack on the first click or keypress. Pressing play is such a
click, which is why play is live from the start: the press that asks for sound is the same press that
allows it.

Nothing that can be done without sound waits for it. The mixer is built from the channels the server
reports in `X-Song-Instruments`, so it is on the page as soon as there is a song, and everything on it
works with no audio stack in existence: the file offered for download is fetched again with the mix as
you change it, whether or not a note has ever been played. What was set beforehand is put onto the
player when the song reaches it, and only what you actually changed, so a channel you left alone still
plays the instrument and volume the song was written with.

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

## Settings

Everything the server is told lives in [`Rmg.WebApi/appsettings.json`](Rmg.WebApi/appsettings.json),
which carries the defaults and explains each one. Three layers, each beating the one before:

| | for | example |
|---|---|---|
| `appsettings.json` | what is the same everywhere, kept in the repository | `"Port": 5000` |
| the environment, `RMG_` prefixed | what differs per machine, and secrets | `RMG_PORT=8080` |
| the command line | the once-off | `--port 8080` |

| Setting | |
|---|---|
| `Port` | The port to listen on. Default `5000` |
| `Address` | `any`, `loopback`, or an address of your own. Default `any` |
| `KnownProxies` | Reverse proxies to believe, comma separated. Loopback always is |
| `StateDirectory` | Where the analytics are kept. Blank puts them beside the app |
| `DashboardToken` | What the analytics dashboard asks for. Blank does not serve it |
| `RetentionDays` | How long an event is kept. Default `180` |

```
dotnet run --project Rmg.WebApi -- --port 8080     # the command line
RMG_PORT=8080 dotnet run --project Rmg.WebApi      # the environment
```

A value that is not a setting stops the server with a line saying which one and why, rather than a
stack trace on the first request that needs it.

**The token does not belong in `appsettings.json`**, which is in the repository. It is there as a blank
with a note; the real one goes in the environment. A deploy writes the environment to
`/etc/<service>/<service>.env` on the server, readable by root alone.

`appsettings.json` ships with the app, so a deploy replaces whatever is on the server: change it here,
in the repository, and let the environment carry what is particular to a machine.

## Analytics

The page counts how long things took and how far people got, so the slow parts can be found. It is
first party and nothing leaves the server: `POST /api/tally` takes a name from a closed list and at
most four numbers, and the rows go into a SQLite file in the state directory.

**Nobody is identified.** A visitor is `HMAC(secret, day)` over the address and the browser, shortened
to sixteen characters. The address itself is never written down, the salt changes every day so the same
person tomorrow is a new visitor, and nothing at all is stored in the browser. That, rather than a
banner, is what keeps this on the right side of the line: there is nothing on the visitor's machine to
ask about, and no personal data kept to need a lawful basis for. The footer says so in plain words.

The events are `page_open`, `song_generated`, `song_failed`, `soundfont_ready`, `soundfont_failed`,
`audio_ready`, `play`, `listened`, `mix_changed`, `download_mid`, `export_mp3` and `shared`. A
`page_open` also says whether the visitor arrived on a shared link or came fresh. Anything else is
dropped without a word. A soundfont is counted by its size and where it came from, never by its name.

**Opting out works.** A browser sending Global Privacy Control, or Do Not Track, is not counted at all:
`tally.js` sends nothing and the page behaves exactly the same.

The file is `tally.js` and the path is `/api/tally` rather than the obvious names, because blocklists
match `analytics.js` and `/api/events` on sight and this was being dropped for its spelling rather than
for what it does. The point of the names is to stop a first-party counter being mistaken for a
third-party tracker, not to get past anybody: someone who means to opt out says so above, and is
honoured.

Rows are kept for `RetentionDays`, 180 by default, and pruned on start. One visitor may write
`EventsPerVisitorPerDay` events, 300 by default, which is far more than the page ever sends and far
less than a script would like to.

`AnalyticsEnabled: false` turns the whole thing off: no `/api/tally` to post to, no database, no state
directory. The page carries on exactly as it is and quietly drops what it would have sent.

### The dashboard

`/dashboard.html` shows it: how many visitors, how long the soundfont took to arrive, how long the page
waited to be touched, how far down the page people got, and which seeds held attention.

It is off unless a `DashboardToken` is set, which in practice means `RMG_DASHBOARDTOKEN`. Without it the server does not serve the summary at all,
which is the right way round for traffic figures. With it, `GET /api/tally/summary` wants
`Authorization: Bearer <token>` and the page asks for the token once, keeping it in that browser.

```
RMG_DASHBOARDTOKEN=$(openssl rand -hex 24) ./deploy.sh --host your-vps
```

Put it in `deploy.env` to keep it between deploys. The analytics live in the systemd `StateDirectory`,
which is `/var/lib/rmg` and is the only place the service can write, so a deploy replaces the app and
leaves the figures alone.

## Deploying

`deploy.sh` builds, tests, publishes and puts the app on a VPS behind systemd. Copy `deploy.env.example`
to `deploy.env`, name your server in it, and the first deploy is:

```
./deploy.sh --install
```

`--install` creates a `rmg` system account, the directory and the systemd unit; every deploy after that
is just `./deploy.sh`. It stops the service, syncs the published files, starts it again and waits for
`/api/soundfonts` to answer, so a deploy that does not come back up fails loudly rather than quietly.

The publish is self-contained for `linux-x64`, around 100 MB, so the VPS needs no .NET installed. Set
`RMG_RUNTIME=linux-arm64` for an ARM server. The tests run before the publish, and a failure stops the
deploy: `--skip-tests` if you mean it.

The soundfont is downloaded by the VPS rather than pushed from your machine, because it is tens of
megabytes and the server has the better uplink. It is downloaded only if it is not already there, with
its licence file first, and the sync never deletes that directory, so what you put there stays put.
`--no-soundfont` leaves it alone entirely.

Requirements on the VPS: `systemd`, `curl`, `rsync`, and an account you can reach with key auth that
can `sudo systemctl`. The app itself runs as `rmg`, which owns nothing it can write to.

| Option | |
|---|---|
| `--host <user@host>` | Where to deploy, if it is not in `deploy.env` |
| `--path <dir>` | Where the app goes. Default `/opt/rmg` |
| `--service <name>` | Name of the systemd unit. Default `rmg` |
| `--port <port>` | Port the service listens on. Default `5000` |
| `--address <where>` | `loopback`, `any`, or an IP. Default `loopback` |
| `--install` | Create the account, directory and unit first |
| `--skip-tests` | Publish without testing |
| `--no-soundfont` | Leave the soundfont directory alone |

The port and address go into the systemd unit as `RMG_PORT` and `RMG_ADDRESS`, so re-run `--install`
after changing either. A port below 1024 gets `CAP_NET_BIND_SERVICE` in the unit, since the service
otherwise runs unprivileged.

### Behind a reverse proxy

The app has no authentication of any kind, so a deploy listens on loopback only and expects something
in front of it. A whole Caddyfile:

```
rmg.example.com {
    reverse_proxy 127.0.0.1:5000
}
```

Caddy gets the certificate itself, and sends `X-Forwarded-For`, `X-Forwarded-Proto` and
`X-Forwarded-Host`, which the app reads, so the request is seen as the browser made it rather than as
the proxy relayed it. Those headers are believed **only from a proxy on this machine**: anything else
that can reach the port could otherwise claim any address it liked. A proxy on another host has to be
named, as `RMG_KNOWNPROXIES=10.0.0.5`, comma separated for more than one.

To serve it under a path rather than a name, strip the path on the way through, or the app sees a
prefix it knows nothing about and answers 404:

```
example.com {
    handle_path /rmg/* {
        reverse_proxy 127.0.0.1:5000
    }
}
```

`handle_path` strips, `handle` does not. The page asks for `api/...` relative to wherever it was
served from, so it needs nothing else.

Use `--address any` to answer on every interface instead, which wants a firewall in front of it.

## Licence

[MIT](LICENSE). The licence covers the source code. Generated songs are yours to use however you like.
