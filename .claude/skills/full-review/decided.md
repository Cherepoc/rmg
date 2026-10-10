# Decided in earlier reviews

Findings raised before and decided against, or kept on purpose, with the reason. A reviewer does not raise one again
unless the code it names has changed since in a way that undoes the reason, and then says so. Added to as findings are
rejected while fixing: the place, what was decided, why.

## From the review of 9 October 2026

### Generation
- `SongFormGenerator`: a build's snare roll over a breakdown is kept; a build over a breakdown is idiomatic.
- `LateEntries`: a part entering at a last chorus's repeat is left as it is; marginal.
- `GenerateInt(min, max)` with `min == max`: kept, an empty range spends its draw so that the draws are as many; documented.
- `HatRolls`, `Ghosts`: reading the song's drum roles, not a section's redrawn ones, is kept; a note's state may not carry the section's role after realizing. Documented.
- `InstrumentsOverTime` ties: a part whose notes are alike (a chug) has no accents; taking a share by order switched a chugging riff's first notes (slow chugs in chords 87% to 68%). Rejected, documented.
- `RhythmicUnconventionality.GrooveHold` defaulting to none: kept, documented as none for a groove as the songs move it; the songs' own uses take no genre's hold.
- `SoloLines` pause weights, `SongLength` weights: weights scaled, not chances multiplied; kept.
- `DrumSolos`' many parameters and its tracks by number: left for when a stage takes the song's decisions whole (named streams, decided against in the roadmap).
- The octave bass and the alternating bass kept as two mechanisms: the two genre choices stay for presets and kept genres, so a merge saves a field and a stream, and a bass playing both (the fifth on strong beats, octaves off them) is a sound worth keeping.
- Kept apart as more code to merge than they save, or a risk to the fingerprint's floating order: the line devices as one table; the pitched parts' definitions; a section's trace helper; the parts' role labels; line notes' chords and the empty tracks over another's state; beats a second; streams in `SongTracks.Create`; a triad's quality (`Progressions` against `ChordNames`); `DrawRests` predicted twice; `TrackGroup`; the line notes' pitch writes (some snap to the scale, some keep the alteration); one-line twins in the lines and the server.
- `Generators.Int()`: kept, its 31-bit seeds feed three generators' draws, which a change would move.
- `DyadicTimelineDescriptor`'s period-and-phase constructor: kept as the tests' way of building a descriptor directly.
- `TimelineEdits`' context: left; constant states, no draws.
- `DrumSounds`' literals in `LiftCues` and `DrumDefinitions`: left as the drums' own note numbers.

### Core foundation and render
- `Midi.cs` writing a GS reset at tick 0: rejected; the page's synth would take the reset too, over the page's live instrument and mix settings.
- `TrackEventStateTimelineMap` keeping emptied tracks: rejected; callers index a resting part's track. Documented.

### Page and server
- The page reading a link's meter or mode past the options: left; the server refuses such settings with a 400 the page shows.
- Visually-hidden copies in the phone query: left; merging them moved the share button's label before the rules that place it (a phone layout test failed).
- The dialogs' shared styles and the editor's choice names written twice: left for a page refactor of their own.
- The phone's `.genre-row` margin: kept; the CSS merge broke a phone layout test.

### Tests
- `SoloTest`'s motif bound 0.42: kept; it separates no answers (0.38) from answers (0.47), and a wider bound would not tell the mechanism off from on.
- `FillGrooveTest`'s tuplet grid kept to halvings to an 8th, documented; telling straight from triplet needs the grid's tuplet class.
- `SongGeneratorSeedTest`'s random seed: kept; the overload's point is a random seed, and a crashing seed shows in the corpus tests.
- `TestCorpus` caching songs with values supplied, by seed and overrides: rejected; thousands of them at about 5 MB each would pass the 8 GB cap.
- `GenrePresetsTest.APreset_MakesSongs` left apart from the intro's pass: its own seeds and checks.
- The clash measure kept twice: `ChordScalesReportTest` on rendered offsets, `InnerLineReportTest` split by moved notes; different inputs and outputs.
- `TestCorpus.FillLines` and `DrumsFrom` kept as the tests' own statement of the rule rather than calling `FillGenerator`, which would make the fill tests agree with the code by construction.
- A tempo's expression in beats a minute left inline in tests; its few copies differ in where they read the tempo.
- Every song at the sound facet's wild end playing a strong effect is no rule: a strong effect no part may carry is not played; the share stays a calibration.

### Put off to the roadmap, not findings
- The stages' reads and writes declared by each stage; tests that check less than their names (the louder and quieter forms per appearance, a breakdown's chugs, a solo's approach off the beat, the passing kinds' shapes, the melody's joins at every pattern, the busyness reports' facets, the MIDI file's program changes, the stages' hand-offs).

## From the session of 10 October 2026

- `Polymeter`: a copied note takes its new place's chord shape from any pitched part's note in that chord, every part sharing it; a chord's pool index is gone by that stage.
- `SongMap.MeterAt` gives a short last bar its own meter, while `PatternBarAt` and `BeatInBar` count by the section's: deliberate, so that the bar before a section is the bar there is.
- `Solos.WithSoloist` is not gated by a genre's `solos.sections`: gating it would scale one chance by another by hand, or cut the songs' own soloist solos fivefold; a preset that keeps solos rare sets `parts.solo` low instead.
- Jazz's plainest song passes no chord (`PassingChordTest.ThePlainestSongs_PassNoChord`); its turnaround at the plain end is the preset's.
- A pedal wins over a doubling: a bass note a pedal holds is the pedal's, no longer the doubled line's.
- A hook intro quotes its chorus's notes only where the chorus plays in the intro's key and tempo; elsewhere the lines' quote alone.
- Black metal inheriting the songs' ornaments: measured nearly inaudible under its distorted band (0.02 heard difference over 24 songs), so neither a question nor a change.
- The fingerprint's determinism checked only within one process (SongsVersionTest): a second process would need a CLI run of the whole corpus, and a per-process drift would show at once as a version raised on every deploy.
- Performance findings of single paths (Realizer's scans, Solos realizing every track, Chugs, LinePattern's chord lookups, StateMap.With, TimelineEdits, ExpressionRender, Polymeter's ShapeAt, Line's scale notes, EventTimeline's resort, StateMap.Aggregate, StateTimelineMap.Create, Unroll): a profile of 384 songs (2026-10-10) shows none above about 2% of generation, the time spread over allocation and GC; a buffer for StateMap.FromOrderedStates, the largest allocator, measured no gain (20.4 s against 20.0 s). Server GC in the CLI took 20 s to 12 s. Reopen with a profile showing a path above 5%.
- Tests making the same overridden songs again (SoloTest, ExpressionTest, FiguresTest, GenreMelodyRangeTest, StrumTest's GetSwing, ListeningQuestionsTest.Write measuring 128 seeds): left, as a cache of traced songs would press the 8 GB cap and the suite's 3m40s is not spent in any one class.
- The page's faders writing the settings on every input event (app.js): left to the page packages, 24.
- genres.db not pruned (GenreStore): a genre is kept for the links that name it, which have no end; its growth is bounded by the 10,000 new genres a day, a few MB a day at the most, honest use far below.
- An imported genre's options, amounts and name are not checked on the page (genre-editor): the server reads every request's genre and its refusal reaches the page; a second validator in the page would only drift from it.
- The audio worklet (spessasynth_processor) and the MP3 worker's encoder load from the CDN with no integrity hash: addModule and a worker's static import take none; the import map's three modules carry theirs. Self-hosting them is the remedy if the CDN is ever in doubt.
- Listening mode's A and B alternate by the song's index (listening.js): the listener is the operator, alternating balances which is heard first, and a drawn order would cost every listening test a way to learn it; kept.
- DyadicRankTimeline.Generate kept though only tests call it: it is how a dozen tests read a rhythm as a timeline.
- The thinning texture (Texture, TextureKind.Thin) may drop the melody among the parts, its order drawn: kept on listening (Synthwave IrBYcTV7F1B's second verse ending on four bars without it), a band's instrumental breath.
- Medieval's raised cadence (cadenceRaise at the songs' own) kept on listening, 5 of 6: the leading tone raised at a cadence is medieval practice (musica ficta, the double leading-tone cadence); only its full major triad on the fifth is later.
- Funk's key changes and pushes, Nu metal's half time, Techno's half and double time and Djent's polymeter kept on listening (10–11 October 2026).
