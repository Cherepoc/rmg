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
   - What each stage keeps out of, by the kinds of `SongDraft.Holds` it names (the primer's *What the song holds*): does every stage that makes a stretch others should keep out of (a fill, a stop, a cut, a drum solo, a call, and any new one) write it to `Holds`, and does every stage that edits the same notes name the kinds it should? Stages that edit the same notes should keep out of the same things; one that does not breaks what another keeps (a push moving a polymeter's cycle was such a bug). What `Holds` does not hold yet (a polymeter's sections, `SongDraft.Polymeters`; figures; breakdowns; a calm or heavy section; a short last bar) is checked stage by stage, and a keep-out two stages write apart is a candidate for a new kind.
   - What each stage reads at a position of an intro or the ending: its source's decisions, by `SongMap.PlayAt`.
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

10. **musicality** is followed by genre, a reviewer per cluster of presets, and by device, a reviewer per family, below.

11. **bars.** Every place that turns a position into a bar, a beat, a group, a phrase or a pattern, or takes the bar before a line: `BarDuration`, `PhraseDuration`, `PatternDuration`, `PhraseBars`, `NearestGroupStart`, `BeatInBar`, `PatternBarAt`, `MeterAt`, `Math.Floor(position / ...)`.
    - Does it ask the map (`SongMap.PlaceOf`, a span's `Bars`, `Phrases`, `Patterns`, `Span.Within`), as the primer's *The map* says? One that walks or counts by hand where the map has the answer is a finding, its fix the map's call; the questions below are for what the map does not answer.
    - Is the position counted from its section's start (or its pattern's), not the song's, in the section's own meter? A section starts wherever the sections before it end, a whole number of its bars from nowhere in particular.
    - Does it hold for a phrase of three or of six bars, a pattern of several phrases, a meter of odd groups, and a short last bar (`SectionSpan.Cut`), whose bar before the next section is shorter than the section's bar?
    - Does a loop over a section's bars reach its last, partial one, and keep its edits inside it?

12. **architecture**, on a full review only, as described in *The architecture reviewer* below.

13. **foundations**, on a full review only, as described in *The foundations reviewer* below.

## The architecture reviewer

One reviewer, the id `architecture`, on a full review only, as a design changes more slowly than the deltas between
reviews. Every other reviewer asks whether the code does what its design says; this one asks whether a design the code
has chosen is the right one: a whole other way of doing a thing that would be better for its correctness, its speed or
its organisation, such as one way of counting a section's bars that every stage asks rather than arithmetic in each,
or another way of keeping the layered state that allocates less.

It works from evidence, not from reading code for what looks cleaner:

- **The history.** The records in `reviews/` (their themes, fix loops, fixes that made the next finding, findings found
  twice), `decided.md`, and `ROADMAP.md`'s *Decided, and kept*, *Decided in the review of …* and *Parked* sections. A
  class of finding that comes back review after review, or a fix that needed the same edit in many places, is what a
  design costs.
- **The measurements.** The allocation and profile reports (`AllocationReportTest`, the roadmap's performance entries,
  `decided.md`'s profile), and `run/` of this review. A claim about speed or memory quotes a measurement; where none
  measures the path, it says so and proposes the measurement, not the gain. A song takes about a tenth of a second, so
  speed ranks below correctness and organisation unless the numbers say otherwise.
- **The code** the design spans, read fully enough to describe it as it is and to plan the change.

It writes **at most three proposals**, the strongest first, each a finding of `category: "architecture"`, `severity`
by what the current design costs, `file` and `line` the design's centre, and these fields beside the usual ones:

- `"cost"`: what the current design has cost, by number: the findings and fixes it caused (the records' numbers and
  commits), the places that repeat it, the measured time or memory.
- `"alternative"`: the other design, concretely: its types and who calls them.
- `"deletes"` and `"adds"`: what the change would take out and put in, roughly by lines and files; a proposal that adds
  more than it deletes says why it is worth it.
- `"migration"`: the steps, each one that keeps the suite passing, and which of them move the fingerprint (CLAUDE.md:
  a refactor keeps it where the right code keeps it; a step that redraws songs is a deliberate one).
- `"decided"`: the roadmap's or `decided.md`'s entry on the same design, if any, and what has changed since that
  decision undoes its reason; a proposal that is a decided item with nothing changed is not made.

Against CLAUDE.md's principles: a simple, general mechanism over a realistic one, one mechanism over a case each. A
proposal makes the code simpler or more general, or measurably faster; one that makes it more layered for its own sake
is not made. Name overengineering and musicality in each: what the proposal risks of either. There is no quota: a
review whose designs are right proposes nothing, and says so in `slice_notes`.

Its proposals are not verified: each is critically reviewed by the main agent as the implementation loop reviews a
plan, and an accepted one goes to `ROADMAP.md` as a plan, a rejected one to `decided.md` with its reason.

## The foundations reviewer

One reviewer, the id `foundations`, on a full review only. The architecture reviewer asks whether the code's design is
right; this one asks whether a model the generator makes music with is: the rhythm (dyadic ranks and their thresholds,
and the figures that stand in for them), the melody (line profiles, contour, phrases), the harmony (levels, progressions,
chord scales), the form, the voicing (`Realizer`), the mood and the conventionality's leans, the layered state as a way
of deciding. Each asked whether it can express what the genres' players play, or keeps needing a way round.

It works from evidence, never from a model read as more elegant:

- **The ways round.** What stands in for a model where it cannot express an idiom: hand-written figures in place of
  generated rhythms (`Figures`, counted), stages of a single device, genre data patching around a lean, special cases a
  model's doc admits. A model that keeps needing them is the strongest signal, counted by the git history of each.
- **What listening said.** Verdicts and ratings (`ROADMAP.md`'s *Listening*, the dashboard's) traced to a model rather
  than to a device's amounts: a setting heard to do nothing (the mood leaning only a genre's own pool), a device heard
  wrong for a reason the model makes.
- **What the genres cannot play.** The roadmap's idioms the engine lacks, the genre research's reports (outside the
  repository, the review's folder naming where), the idiom gaps past reviews proposed.
- **What the songs measure.** The reports in `run/` (`genre-devices.txt`, `sounding.txt`, `every-preset.txt`) and the
  explicit report tests: syncopation, evenness, repetition, register, against what the style's records do.
- **What was decided.** `ROADMAP.md`'s *Parked* and *Decided, and kept*, and `decided.md`: an alternative parked or
  decided against (patterns by measured features, euclidean patterns, a library of idioms, drums generated together)
  is proposed again only with what has changed since, said.

It writes **at most two proposals**, the strongest first, each a finding of `category: "foundation"`, `severity` by
what the current model costs the music, `file` and `line` the model's centre, with these fields beside the usual ones:

- `"cost"`: what the model cannot express, by number: the ways round and their commits, the verdicts, the idioms.
- `"alternative"`: the other model, concretely, as rules a person could follow by hand: what it decides from what, its
  plain and wild ends, how a genre's data leans it. Algorithmic and explainable, as CLAUDE.md's principles want:
  **no machine learning**, no model trained on data, no corpus of other music.
- `"experiment"`: the smallest thing that would decide it by ear: the alternative built for one part in one genre as a
  genre option beside the current model, measured on the reports, and asked as a listening question against it. Not a
  migration: one follows, reviewed as a plan, only once listening prefers the alternative there.
- `"keeps"`: what the current model does well that the alternative must keep (its meters, its conventionality's ends,
  its streams), and the listening verdicts tuned on it that a change would put at risk.
- `"decided"`: the entry on the same model, if any, and what has changed since.

Name overengineering and musicality in each: a model more layered for its own sake, or one that sounds no better, is
not proposed. There is no quota: a review whose models serve the music proposes nothing, and says so in
`slice_notes`. Its proposals are not verified: each is reviewed by the main agent as the implementation loop reviews a
plan, an accepted one going to `ROADMAP.md` as its experiment, a rejected one to `decided.md` with its reason.

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
  entries, a section's calm or heavy form, call and response, held tones and let ring, the hook intro): does each keep out of the other where it should, through the kinds of `SongDraft.Holds` where the other is held there, and is what they make together intended? Most of the
  bugs a second corpus found were two devices meeting: a push moving a polymeter's cycle, a doubling silencing its
  part where a texture rested the doubled one, a drop landing on a section that builds.
- **As it sounds.** What the device does to the music, whether a player would do it so, and where its amounts would
  sound wrong; with the parts it sounds against, from `run/sounding.txt` and the primer's musicality lines: a device
  that copies or moves notes (doubling, imitation, quotes, polymeters) judged where its notes land, against the line
  still playing there. Where a rule the code or the music states settles it (notes against the chord, a device playing where
  its doc says it does not), it is a mistake. Otherwise it is `kind: "taste"` with a proposed listening question (see
  the primer's output): at most three for the family, ranked (`listen.rank`), the coverage check refusing more.
- **Worth keeping.** Whether the device earns its place, from the review folder's `ledger.md` (written by
  `scripts/ledger.py`): the presets that name it, how often the songs and each preset play it, the code that reads it,
  the findings past reviews raised there, and the listening questions asking it; and from what listening has decided
  of it before (`git log --grep 'Listening answered'`, `decided.md`, `ROADMAP.md`'s cuts). A preset that does not name
  a choice plays it at the songs' own amounts, so a device no preset names is not unused: its plays say so. A
  candidate to cut is one that plays rarely or nowhere its style wants it, that costs code and findings out of
  proportion, or that listening has found hard to hear; one the report does not measure is `unsure`, and the measure
  it needs is a `tests` finding on `GenreDevicesReportTest`. Simplicity counts for cutting: a device taken out is the
  cheapest simplification there is. A cut is never a reviewer's to decide: each candidate is a finding of
  `category: "cut"`, `kind: "taste"`, with a `listen` that plays the song with and without it (`without` the stage
  skipped or the choice at 0), at most three for the family, ranked (`listen.rank`), apart from its other taste
  findings' three. The ear decides.
- **Coverage.** List every device of the family in `coverage`, `file` the device's main file and `note` its name,
  what was checked and its verdict: `keep`, `cut candidate` or `unsure`. The coverage check fails a device not named
  there.

## The genre-fit reviewers

A reviewer per cluster of genre presets, the id `genre-<cluster>`, every preset of `Client/genres.json` in one cluster
(`genres/<cluster>.txt` in the review folder, written by `scripts/devices.py`): styles near enough that what sets each
apart from the others is part of what it must play. Read `run/every-chance.txt` (every chances option's three ends by
preset, `*` marking the songs' own amounts a preset inherits), `run/genre-devices.txt` (how often each preset plays
each device, measured), `run/every-preset.txt` (what each preset's songs draw at the plain end, the middle and the wild
end), `run/sounding.txt` (how each preset's parts sound together: a note held into its part's next, a semitone between
parts, its own notes' and a doubling's, a strong note off its chord or on a chord's missing third), and each preset in `Client/genres.json` with its description, every choice it names read against `GenreChoices`
and the code of the devices it switches on or leaves. Then, for each genre of the cluster:

- **Inherited where it does not belong.** A device the genre plays by the songs' own amounts (`*`) that its style would
  not play: a short bar in a genre built on square sections, a solo in a genre without soloists. The fix is the option
  named at 0, the choice's other options named at the songs' own amounts so that they keep playing.
- **Missing.** A device the engine has that the genre's players use, which the preset leaves at the songs' own amounts
  or at 0: ghost notes for a genre whose drummers play them, a riff intro for one that opens on its riff. Only devices
  the engine has; a device it does not have is an idiom gap for the roadmap, said as such.
- **Mis-set.** Amounts against the genre: a plain end playing what a plain song of the genre never does, a wild end
  losing the genre, a pool whose weights contradict the description, an instrument, a kit, a tempo, a meter or a form
  the style would not take.
- **Against the description.** What the preset's description says against what its choices do.
- **Its conventionality.** The plain end the genre's most typical song, the middle a good one of it, the wild end still
  the genre: a device that should be the genre's plainest habit left to its wild end, or a wild device leaking into
  its plain end.
- **Its song-wide settings at their ends.** The darkness, the intensity and the facets given at each end
  (`SongOverrides`), the songs compared on what the setting names, the same seeds at either end: a brighter song's major
  chords, a fiercer one's tempo and density. A setting whose ends a genre plays alike is a `mistake` with its numbers
  (Trap at darkness -1 and 1, 64% and 66% of its chords minor, every scale minor), its cause read where the setting
  leans (a pool naming no option at that end, a lean too small to hear), not a listening question first.
- **Gaps, on a full review only.** An idiom the cluster's players use that the engine has no device for: a bass
  figure, a comping pattern, a form, a drum habit. At most two for the cluster, each a finding of
  `category: "idiom-gap"`, ranked by how many presets would play it, in this cluster and any other (name them all): a
  device that serves several genres comes before one for a single genre, as CLAUDE.md wants one mechanism over a case
  each. Say which mechanism it would extend (another figure of `Figures`, another option of a choice) rather than a
  stage of its own where one would do, and what its plain and wild ends would be. Check `ROADMAP.md` first: an idiom
  it plans, parks or has let go of is not proposed again unless something has changed since, and then say what. A gap
  is not verified and goes to no package: it is reviewed as a plan, an accepted one going to the roadmap.

What is a mistake and what is taste:

- A **mistake** (`kind: "mistake"`) quotes a measured number from the four reports, with the report, the preset and
  the end it was read at, against what the preset's own description or the style's plain convention settles: the
  description names a room kit and the wild end plays the standard kit in 82% of songs. A mistake has no listening
  question; the verifiers check its number. A mistake without a number is not one.
- Everything else is **taste** (`kind: "taste"`): no verifier checks it, and listening has kept most of what reviewers
  heard in their heads. Propose **at most three** for the whole cluster, ranked (`listen.rank` 1 to 3), the one whose
  answer would change most songs first; the coverage check refuses more. A taste finding left out is not lost: say it
  in a line of `slice_notes`.
- Each finding but a gap is `category: "genre-fit"`, `file` `Rmg.WebApi/Client/genres.json` and `line` the preset's
  line (a gap's too), the preset's id at the start of its summary, with the change to the JSON as the suggestion. There is no quota: a cluster
  whose presets are right reports nothing. Name no artists or bands; describe the sound.
- In `coverage`, every preset of the cluster, each an entry `{ "file": "Rmg.WebApi/Client/genres.json", "preset":
  "<preset id>", "lines": <its line>, "read": "full", "note": "<the choices and inherited devices checked>" }`.
