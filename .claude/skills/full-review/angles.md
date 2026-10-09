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

10. **musicality.** The generator as a musician would hear it, genre by genre.
    - Read `genres.json`'s presets with their descriptions and the devices they switch on.
    - Read the devices' code enough to know what they do.
    - Report idiom mistakes (a device a genre would never use, or plays wrongly) and idiom gaps (what a player of the genre would always do and the generator never does). Cover every preset briefly and the defining ones in depth: rock, metal, jazz, funk, electronic and techno, trance, hip hop, reggae, latin and bossa, blues, ambient, orchestral, black metal, djent, IDM, drum and bass.
    - Each finding is `musicality`, with a concrete suggestion. `file` and `line` point at the preset or the code.
    - You may not run anything. If a measurement would settle a question, say what report and what number would settle it.

11. **bars.** Every place that turns a position into a bar, a beat, a group, a phrase or a pattern, or takes the bar before a line: `BarDuration`, `PhraseDuration`, `PatternDuration`, `PhraseBars`, `NearestGroupStart`, `BeatInBar`, `PatternBarAt`, `MeterAt`, `Math.Floor(position / ...)`.
    - Is the position counted from its section's start (or its pattern's), not the song's, in the section's own meter? A section starts wherever the sections before it end, a whole number of its bars from nowhere in particular.
    - Does it hold for a phrase of three or of six bars, a pattern of several phrases, a meter of odd groups, and a short last bar (`SectionSpan.Cut`), whose bar before the next section is shorter than the section's bar?
    - Does a loop over a section's bars reach its last, partial one, and keep its edits inside it?
