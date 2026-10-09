# RMG code review: cross-cutting reviewers

You review one concern across the whole codebase rather than one slice.

- Read `primer.md` and `decided.md` (same folder) and `/home/glyge/Projects/sw/rmg/CLAUDE.md` first. The checklist, the rules (read only, never build or run tests), the evidence standard and the severity scale there apply to you.
- Use Grep and Glob to find every place your concern touches, then read each place fully enough to judge it. Your job is completeness over the concern: list every occurrence you checked, not a sample.
- The output is the same JSON as the primer's, with these differences:
  - `"slice"` is your angle's id.
  - `"coverage"` lists the places you checked, each `{ "file", "lines": <line or 0>, "read": "full", "note": "<what you checked there>" }`.

## The angles

1. **streams.** Every random draw: `GenerateDouble`, `GenerateInt`, `TestProbability`, `Pick`, `Generators.*`, and anything drawing from an `IGenerationContext`.
   - Does each decision draw from a stream of its own (`SongStream`, `SectionStream`, `Seeds.Derive`)?
   - Does any draw happen conditionally on a stream another decision shares, so that turning a device off shifts later draws?
   - Are the `SongStream` and `SectionStream` enum values unique?
   - Is any stream reused for two decisions?
   - Are streams created per note in hot loops, at a cost?

2. **loudness.** Every arithmetic on `Velocity`: `RealizedNote.Velocity`, `StateKinds.Velocity` and the velocity layers.
   - The render maps a velocity sum through `Render.ToMidiVelocity` (a tanh curve), so multiplying a sum by a factor does not scale loudness and can even raise a negative sum. `Render.ScaleLoudness` scales a share on the heard scale. Find every place that multiplies a sum, or adds or subtracts expecting a loudness ratio, rather than using it.
   - In the render too: a velocity scaled after the curve (an ornament's, a grace stroke's, a second take's level) is on the heard scale already; check it is meant there.
   - Judge each: a correct offset on the sum scale, or a bug.

3. **stages.** `SongStages.All` and every stage function.
   - What each reads from `SongDecisions` and `SongDraft` and what it writes.
   - Does any stage read work a later stage makes?
   - Does a later stage silently undo or break an earlier one? For example, stop time removing a fill's notes, a push moving notes another stage placed, an expression stage reading state an earlier stage dropped.
   - Is any `CanSkip` stage read by a later stage (wrongly skippable)?
   - Is the order documented and right?
   - The sections each stage keeps out of: solos, drum solos, stop time, a polymeter's sections (`SongDraft.Polymeters`), figures, breakdowns, a short last bar. Stages that edit the same notes should keep out of the same things; one that does not breaks what another keeps (a push moving a polymeter's cycle was such a bug).
   - What each stage leaves a later one: an edit made before the realizer that the realizer can undo (a note struck again in a cleared span), or one made after it that misses what the realizer adds.

4. **duplicates.** Mechanisms implemented twice, or nearly twice:
   - several "parts by role" lookups, several "notes in a span" helpers, several bar-iteration loops, several ways of finding the bar before a line;
   - families `decided.md` keeps apart are not findings.
   - Name each family, its members (file and line) and a concrete merge or generalisation, with what it would save. Only real duplication: the same logic, not a similar shape.

5. **genre-data.** `Rmg.WebApi/Client/genres.json` against `GenreChoices`, `GenreChoiceDescriptions` and the editor (`Client/genre-editor.js`).
   - Every choice and option the presets name exists.
   - Every choice has a description and an editor tile or tab.
   - Values are in range: amounts 0–100, arrays of three, and the order the ends expect.
   - No preset names an option that has no effect.
   - Presets are coherent with their own description text: instruments, devices.
   - Chances choices named with `{}` that switch everything off, and whether that was intended.
   - Report each problem with the preset id and the JSON line.

6. **contract.** The page against the server.
   - Every `fetch` and request body in `Rmg.WebApi/Client/*.js`, against `Rmg.WebApi` endpoints and request models (`GenerateSongRequest`, analytics events, listening, admin, genres).
   - Field names, types, required against optional, error handling on both sides.
   - Events the client sends that the server rejects, and the other way round.
   - The listening mode's `listening.json` fields against its use.

7. **server.** `Rmg.WebApi` robustness and security:
   - input validation and limits on sizes and seeds;
   - rate limiting and the queue's back-pressure;
   - SQL built with interpolation;
   - the admin token's handling and comparison;
   - path traversal (sound fonts, static files);
   - exceptions leaking details;
   - disk growth of the analytics database;
   - concurrency in `SongQueue` and `EventStore`;
   - cancellation of abandoned songs.

8. **performance.** Generation and rendering speed and memory, across `Rmg.Core`.
   - Find the hot paths: per note, per bar, per stage.
   - Look for repeated O(n²) scans (`timeline.Where(...)` inside a loop over the same timeline), repeated `GetEffectiveStateMapAt`, repeated LINQ enumeration, allocation in tight loops, state maps rebuilt per note, regexes or reflection in loops.
   - Give each a cost estimate (how often it runs, the size of n) and a concrete fix.
   - A song takes about one second to make, so prioritise what would move that.

9. **tests.** The test suites as a whole (`Rmg.Tests`, `Rmg.Tests.Page`):
   - tests asserting a description or trace text rather than typed values;
   - calibrations held as tight rules;
   - tests that can pass vacuously (no count or `IsGreaterThan` guard on a loop's matches);
   - duplicated tests;
   - the slowest patterns (corpora of 64 or more songs generated in a rule test, `Report`s not marked `[Explicit]`);
   - flaky page patterns such as fixed delays and races;
   - important behaviours of `Rmg.Core` with no rule test at all, especially the stages and devices added since the last review (`git log` since its date);
   - tests that hold only for the corpus's own songs: a part, a device, an intro or a section kind assumed present, a seed's song named for what it happens to do (`RMG_CORPUS_OFFSET` moves the corpus);
   - calibrations over fewer than 64 songs, or comparing one genre's songs with another's rather than the same songs with a mechanism on and off.

10. **musicality** is followed by genre, a reviewer per preset, and by device, a reviewer per family, below.

11. **bars.** Every place that turns a position into a bar, a beat, a group, a phrase or a pattern, or takes the bar before a line: `BarDuration`, `PhraseDuration`, `PatternDuration`, `PhraseBars`, `NearestGroupStart`, `BeatInBar`, `PatternBarAt`, `MeterAt`, `Math.Floor(position / ...)`.
    - Is the position counted from its section's start (or its pattern's), not the song's, in the section's own meter? A section starts wherever the sections before it end, a whole number of its bars from nowhere in particular.
    - Does it hold for a phrase of three or of six bars, a pattern of several phrases, a meter of odd groups, and a short last bar (`SectionSpan.Cut`), whose bar before the next section is shorter than the section's bar?
    - Does a loop over a section's bars reach its last, partial one, and keep its edits inside it?

## The device families

A reviewer per family of musical devices, from `devices/<family>.txt` (written by `scripts/devices.py`): every genre
choice and every stage of the family. The id is `device-<family>`. For every device of the list:

- **End to end.** Its decision (where it is drawn, from which stream, leaned by which facet and genre choice), the stage
  or generator code that plays it, what the render does with it, its genre data (the presets that name it, the songs'
  own amounts), its tests (a rule that it plays where it should and never at the plain end where it must not, a report
  of how often it plays), and its roadmap and listening entry. A device whose decision, stage or tests are missing, or
  that plays where its own doc says it does not, is a finding.
- **Against the others.** The family's devices in pairs, and each against the devices of other families that edit the
  same notes or the same bars (stop time, drops, solos, textures, polymeters, figures, short bars, doubling, late
  entries): does each keep out of the other where it should, and is what they make together intended? Most of the
  bugs a second corpus found were two devices meeting: a push moving a polymeter's cycle, a doubling silencing its
  part where a texture rested the doubled one, a drop landing on a section that builds.
- **As it sounds.** What the device does to the music, whether a player would do it so, and where its amounts would
  sound wrong. A finding of taste is `kind: "taste"` with a proposed listening question (see the primer's output).
- **Coverage.** List every device of the family in `coverage`, `file` the device's main file and `note` its name and
  what was checked; the coverage check fails a device not named there.

## The genre-fit reviewers

A reviewer per genre preset, the id `genre-<preset id>`, every preset of `Client/genres.json` (listed in `genres.txt`,
written by `scripts/devices.py`). Read `run/every-chance.txt` (every chances option's three ends by preset, `*` marking
the songs' own amounts a preset inherits), `run/genre-devices.txt` (how often each preset plays each device, measured),
`run/every-preset.txt` (what each preset's songs draw at the plain end, the middle and the wild end), and the preset in
`Client/genres.json` with its description, every choice it names read against `GenreChoices` and the code of the
devices it switches on or leaves. Read the presets nearest it in style too, for contrast: what sets this genre apart is
part of what it must play. Then, for the genre:

- **Inherited where it does not belong.** A device the genre plays by the songs' own amounts (`*`) that its style would
  not play: a short bar in a genre built on square sections, a solo in a genre without soloists. The fix is the option
  named at 0, the choice's other options named at the songs' own amounts so that they keep playing.
- **Missing.** A device the engine has that the genre's players use, which the preset leaves at the songs' own amounts
  or at 0: ghost notes for a genre whose drummers play them, a riff intro for one that opens on its riff. Only devices
  the engine has; a device it does not have is an idiom gap for the roadmap, said as such.
- **Mis-set.** Amounts against the genre: a plain end playing what a plain song of the genre never does, a wild end
  losing the genre, an amount the measured share shows far from the style, a pool whose weights contradict the
  description, an instrument, a kit, a tempo, a meter or a form the style would not take.
- **Against the description.** What the preset's description says against what its choices do.
- **Its conventionality.** The plain end the genre's most typical song, the middle a good one of it, the wild end still
  the genre: a device that should be the genre's plainest habit left to its wild end, or a wild device leaking into
  its plain end.
- Each finding is `category: "genre-fit"`, `file` `Rmg.WebApi/Client/genres.json` and `line` the preset's line, with the
  change to the JSON as the suggestion. A change of amounts is a matter of taste unless the description or the style's
  plain convention settles it: `kind: "taste"` with a listening question, or `"mistake"`.
- The strongest eight findings at most, so that the list stays one a person acts on. Name no artists or bands;
  describe the sound.
- In `coverage`, every choice the preset names and every device the inherited table shows it playing, each with what
  was checked.
