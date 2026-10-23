# Decided in earlier reviews

Findings raised before and decided against, or kept on purpose, with the reason. A reviewer does not raise one again
unless the code it names has changed since in a way that undoes the reason, and then says so. Added to as findings are
rejected while fixing: the place, what was decided, why.

## Standing

- No machine learning: no model trained on data and no corpus of other music. The generator is rules a person can
  follow, leaned by data; a proposal of another model proposes rules (16 October 2026, the owner).

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
- Per-section chances that stop short of 1 at the wild end (a short bar, a metric modulation, a section's tempo, pushes): a share of a song's sections, which its many sections make near-certain in a wild song but not everywhere; not a song-level chance that must reach 1 (decided 11 October 2026).
- A half count-in, an ending's ritardando and a tempo ramp drawn alike at every end: a song's style, neither plainer nor wilder, as the parts a song has are (decided 11 October 2026).
- A chord soloist's approach asserted off the beat: not testable from the notes, as a stacked note's line may be another soloist's (decided 11 October 2026).

## From the review of 11 October 2026

- The tension (`tension`, a line's strong beat held on a tension) kept to Djent (and Metal's and Nu metal's counter-melody as named): its question heard 3 better, 4 worse and 5 not told apart of 12 (8 October); Black metal's taken out. Not a cut candidate again unless Djent's is heard worse.
- The polymeter's frame (`polymeterFrame`), the melody's inversion (`inversion`) and the pinch's squeal (`pinchSound`) cut on listening: not told apart, heard worse in 5 of 6, and General MIDI's harmonics preferred. Not proposed again.
- A part's tone closes 16 steps at its deepest (`Expression.ToneReach`), short of the filter's reach of 24, as the brightness's floor at 40 is where instruments fall nearly silent: not raised to 24, though listening on 8 October asked for more, which would leave a deep tone no room for a filter; whether the tone earns its place is a listening question (#28).
- 12/8's backbeat is the meter's half-cycle-late cycle (`Meter.Backbeats`), dotted quarters 2, 3 and 4; the heavy figure's added backbeat follows it, as the snare's own backbeat does, until the cycle is measured (roadmap).

## From the review of 16 October 2026

- A part's rhythm leaned by a leader's onsets (foundations proposal #99): rejected for now. It would order the pitched
  parts' generation within a section for a lean that a figure followed at a loose strictness gives, once figures are
  chances (the roadmap's experiment); taken up again only if that experiment is built and an interlocking part is still
  out of reach, with a measure of onsets struck together.
- A genre's fineness moves a part coarser alone (`GenreRhythm.Of`, documented); a value above the middle is not
  refused, the presets' 100s taken out (#61, #339).
- The heavy presets' half and double time as their records tuned them (Heavy metal, Thrash, Symphonic, Black metal):
  the kick's steady figure plays through them; only the descriptions set right (#367, #369, #398).
- Walks of stretches that are no map unit kept by hand: a polymeter's frames (whole sections or several patterns),
  stop time's phrases, the fill lines of `FillGenerator.GetSectionLines` (fill sections, which have no spans); and the
  map-less place of `TimelineEdits` and `Song`, which the fill tests' songs without a map need (#48, #106, #164,
  #170, #171).
- Tests counting a section's bars, phrases or patterns from its start in its own meter, which the map answers alike,
  left as they are where they hold for a short last bar too (#221, #244, #247, #264, #273, #278, #288, #303).
- A drummer's stickings (`sticking`, #341) kept on listening: the eighth round (19 October 2026) heard the songs with
  the genre's stickings preferred 5 to 1 over the songs' own strokes. Not a cut candidate again.
- The shared solo played together (`soloShare.together`, #136) kept beside the twin solo (`solos.twins`): the drawn
  songs' twin chance is 0, so the share is their only way to play a solo together; the twin no longer sets a solo of
  several soloists together over the share, joining a soloist alone (19 October 2026). Not a cut candidate again.
- The inner line over a vamp's marks (#73 of 16 October, #57 of 22 October) left as it is: measured, a held chord run
  across a vamp's marks of the same root plays the line no more often (Dark jazz 8% to 5% of its sections), as the line
  keeps to a held triad or power chord and the presets that name it play sevenths there; the line itself was kept on
  listening (18 October 2026). Not raised again.
