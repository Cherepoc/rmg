# Roadmap

What is to be done, what listening is to settle, the ideas parked and the decisions that keep what is built so. What
is built is described where it is built, in the code, and its numbers are in the commits that made it; an entry here
is removed once it is built.

## Priorities

**Next** (P1): listening to what is deployed, 0.6 and on, and tuning by what it says (see *Listening*), and the
ratings by conventionality once enough are in, alongside, continuously and never as a gate.

**Later** (P2), roughly in this order: the ratings report (see *Listening data*), once the dashboard shows songs rated
enough; listening to the genres' presets (see *Genres*), and to what the harmony, the melody and the form gained in
October 2026: chords off the scale, a phrase's loudness, the hook intro, the chorus's lift and the restated phrase.

**When the need shows** (P3), each by what listening says, roughly in this order: timing by role, then a genre's
pocket; the tempo leaning by the energy; scales of other sizes; legato slides; a motif developed further; the
page's wait for a remake; a key change's common tone; the fills' loudness and sounds by convention; modulation, when a
soundfont maps it, and a ping-pong echo (see *Open work*).

**Put off** until their uses ask for them: long cycles and a tag (see *Open work*, *Rhythm engine*).

**Parked** (P4): see *Parked*, kept for their reasons, not planned.

## Listening

What listening is to settle, each a question and what it decides. A report test picks the corpus seeds to hear for
each from the trace; the seeds named here are ones already found.

### The song and its sections

- **How plain by default:** drawn songs gather about a base of 0.25 (`Unconventionality.BaseCentre`), every value
  possible, above 0.75 in about one song in eighty (`BaseSpread`), the facets and sections drawn about it by the same
  law, and the major and minor scales and one scale through a song weigh more at the middle; 52% of drawn songs are in
  major or minor throughout, 61% straight, 70% in one key and 9% plain in every convention measured
  (`ConventionalityLevelsReportTest`). Still too adventurous? Then lower the centre, or the ease of the conventions'
  curves from 3 to 2, which makes a value below the middle plainer and one above it wilder, or retune the middles of the
  feel's tuplets and the key changes.

- **The forms:** do the songs of a form sound like verses and choruses, the chorus lifting? A song's sections take 80
  bars at the median: too short? Does an intro or an outro of its own material (about a third of middling songs and a
  seventh) sound like the same song? A last chorus played twice (17% of songs), lifted a key in a sixth of them: heard
  as a lift?
- **The sections' energy,** tuned by measurement only: do quieter sections sound as sections, and louder ones lift? A
  section's last appearance plays 0.10 of velocity louder than its first: heard, or too much with the lift's crescendo?
  The fills land into a quieter section 54% of the time: too often?
- **The rests and entries:** do breakdowns and sections without a melody sound like an arrangement or like parts gone
  missing; is a drumless section before a chorus a lift? Parts held back at the start (15 to 18% of songs) and a build
  bringing the drums in at a phrase: arrivals, or thin openings?
- **The lift:** is a crescendo of about 8 of 127 over the bar before a louder section heard as one? Do the drums' cues
  into a bigger section, the hi-hat opening and the snare on its head, about 2.6 a song, sound like a drummer's or
  predictable?
- **Key changes:** the fourth fifth's, about two a song: too many for a song only fairly wild? Prepared ones, half of
  them: does a minor section's chain, minor but at the cadence, lead; does the melody over a passing key turn well; are
  the pivots heard at all, the pedals as tension, the sequences as a climb?
- **Tempo:** a section's own tempo eased into half the time over the bar before, a metric one taken at once, a third of
  the wildest's appearances by a metric ratio from the one before: heard as a pulse taken over, or as a lurch? Does 6/8
  drag, its tempo counted in quarters, and is 13/16 too busy?
- **Half time and double time:** does double time, 22 drum notes a bar, sound like a lift or a scramble?
- **The fade-outs:** a ritardando into the fade, the drums fading first, or a tag after it, if asked for.
- **The settings panel:** does a song made again with changed settings, going on from the same share of its length,
  sound like the same song moved or a jump? Do the pad in 80% of songs, the counter-melody in 60% and the drums in 95%
  leave songs too thin?

### Harmony and lines

- **The section modes:** does the cadence into a change prepare it? The parallel major or minor and the harmonic minor
  sound clearly (corpus seeds 196, 10, 36 and 171), the one-note modes more faintly.
- **Harmonic rhythm,** set by measurement only: do sections of a chord every half bar sound busy, and of one every two
  bars static? Is a chord every half bar at 175 BPM, 0.7 s, hurried, or one every two bars at 90, 5.3 s, static? Then
  lean the span by its length in seconds.
- **The melody's question and answer, and the improvised appearances:** then a bar of a later letter within the
  phrase.
- **Varied repeats (A′):** a repeated bar is varied by a flat chance of 0.25 (`PhraseSchemes.VariedRepeatChance`).
  Should plain sections sound too varied or wild ones too literal, lean its chance alone, gently: a varied repeat is
  as conventional in songwriting as a literal loop is in electronic music, and the scheme already leans to more
  distinct bars in wild sections.
- **Where the melody leaps:** at a phrase's or a section's start, about a third of the time, mostly after a rest: a new
  phrase, or a break? Two rules to close a phrase onto its start were tried and dropped.
- **The sequences:** a figure repeated where the phrase rises rises with it: a tune going somewhere, or figures
  drifting? A melody's notes on the beat are the chord's 71% of the time over 64 songs.
- **The gaps before chord changes:** half the changes have no melody note in their last beat. Empty, now that the solo
  part answers in the melody's gaps?
- **The walks into a section:** the melody walks in at 15% of the changes, three notes most often, the counter-melody
  where the melody does not, the bass at half of the runs: too often?
- **The melody's double stops** on a chord instrument's downbeats in a quarter of its sections, and the riff's chords
  where a section starts: idiomatic, or muddy?
- **The bass's pickup** always falls on the beat: the 8th before the bar line, leaned by convention, if square.
- **The octaves and the twins:** a line in octaves in 10 to 20% of appearances, by the sound facet and the loudness:
  too often? Does a counter-melody an octave up cross the melody? The riff's twin in 15% of riff songs, its fourths and
  fifths at the wild end: too sour? Twin leads, the counter-melody in the melody's sound a third apart: clear, or one
  smeared voice?

### Solos

- **Runs and phrases:** do the runs sound like a player's or a scale exercise, a cell of five or seven a grouping or a
  stumble? Is the intensity's arc heard, its climax at 70%; the phrases as sentences, each landing, half of them
  opening on the last's motif a step away? A pause 17 to 20% of the segments: too many breaths? A middling solo 2.1
  notes a beat, two thirds of its runs at 16ths or faster: busy enough? Long 64th runs under 16th swing: rubato, or
  smeared?
- **Expression:** of a middling solo's notes of an 8th or longer 59% with a vibrato, 16% bent, 3% a wide vibrato: too
  few wide ones? 85% of guitar and synth lead solos bend: a straight band under a bending lead heard as meant?
- **Loudness:** a soloist plays 0.06 of velocity over the band's loudest part: heard over it, a flute too?
- **Chords in solos:** a chord soloist stacks 41% of its held notes, and planes 17% of the time at the middle and 59%
  at the wild end: block chords heard as a pianist's, the planing as meant?
- **The solo part's answers** in the melody's gaps, in 59% of songs: a guitarist filling between a singer's phrases,
  or clutter?
- **Drum solos:** a drummer's stickings round the kit, or a pitched run on drums; a crash with the kick held 23% of the
  segments, the kick under the accents half the time, a crash on the climax: too much?

### Drums and groove

- **The drums:** the percussion songs; the wild end, where the kick or the snare in a role not theirs may sound broken
  rather than bold; bound drums, too rare to hear at a doubling chance of 0.15?; the clap's ghost notes, soft hits
  around the backbeat, built only if missed.
- **The fills:** wild songs play about 1.8 times the tuplet notes of plain ones: too straight? Then read the tuplet the
  drums play most. Three runs in ten play quarters or slower: weak? Then aim a run's finest rank at the finest the
  tempo allows.
- **Repeated cycles** replay their beats' accents: mechanical? Then a small fresh loudness draw over the replayed
  accent.
- **Swing and panning,** set by measurement only: is a triplet's swing too heavy at 16ths, and do wild songs' tuplets,
  swung, sound played or broken? Are the chords too far out, or the melody too near the middle?
- **Drum pieces given:** both snares given double the backbeat, which the song never draws: heard as wrong? Then give
  the snare group one of its snares, as its rule draws. A piece put in plays by its roles alone: do a whistle or congas
  given to a kit song sound placed?
- **Every drum heard:** a drum no section's kit took joins one section (`DrumCoverage`), and a vibraslap or a tom the
  song never sounded lands once, with a crash where it can: placed, or tacked on? At the wildest groove a song of the kit
  and percussion plays percussion only in half its sections (`PercussionSections.WildShare`): the kit too seldom, or
  too often?
- **The polymeters:** the snare on every cell's start of a short cycle: too busy? The chords joining a cycle of short
  cells: mud? A cycle running on through a section or growing each time round: heard as a drift resolving?

### Sound

- **The velocity:** how even the bass and the chords play; the bar layers add 3 to 5 from bar to bar: jumps?
- **The effects:** the middle fifth plays 1.4 gentle kinds a song and a strong one (tremolo, echo, sweep) in 28% of
  songs, the plainest fifth in 16%, the wildest in 60%: too many or too few? Lean the song's weights.
- **The bends, vibratos and slides:** bends of one or two scale steps, two in a third of a middling song's; wide
  vibratos on guitars and synth leads; slides on 13% of a middling song's parts. Do the wide vibratos sound like a
  guitar's, does a part bending two steps everywhere tire, and are slides heard as slides? Slides are 44% of the
  corpus's pitch shapes: too many?
- **The instruments:** a switch keeping to the part's family, 58% of a middling song's: welcome, or does a song want
  more handovers? An articulation on the same rig, a driven guitar's harmonics, a clean one muted: heard as the same
  player?

## Listening data

A song's version (`VERSION`, `SongsVersion`) is stored with every analytics event about a song, with its settings
(`SongSettings`) and its rating; the dashboard shows the listening and the ratings by version and by fifths of how
plain or wild the songs are.

- **The ratings report** (P2, once the dashboard shows a few hundred songs rated in one version): an export of a
  version's ratings and time listened (seed, rating, seconds), behind the dashboard's token, and an explicit report
  test that reads it, keeps the seeds of the build's own version, generates them again with the trace and holds the
  ratings against its values (`StateTraceEntry.Value`): which endings, intros, drum setups, modes, tempos, energies or
  improvisations are liked or skipped, with their counts, so that a small sample shows itself. Measured by: ratings
  per version, enough to tell two versions apart.
- **Targets from outside** (P3): a few measures (the notes a bar by role, the intervals, the syncopation, the chord
  changes a bar) of a corpus of real MIDI songs to tune towards.

## Open work

Each item with its plan, and what it waits for.

### Conventionality

- **A song's length, the rest:** a short length runs long, 16% of the songs given a minute and 19% of those given two
  past 15% over (`SongLengthReportTest`), the intro's bars taken in (`SongFormGenerator.IntroDuration`) but an intro
  section, an outro or the ending's bars adding what the length leans no draw of; their chances leaned by the length
  as the form's are, should it matter. Long songs, ten
  minutes, are free forms seven times in ten, as no form reaches them: heard as rambling? Solos filling a long length
  rather than more parts, if so.
- **Listening to the drum solos' players:** a soloist is drawn evenly among the kit's drummer and the percussion
  drums of two sounds or more, so that in a song with percussion a percussion drum solos in 68% of the drum solos and
  without the kit in 64% (`DrumSoloTest.Report`); the kit weighed more, should listening miss the drummer. A player's
  turn traded away rests, its feet aside. A hi-hat's foot on the backbeats under the kit's solo only if listening asks
  for it, a pattern of its own.
- **More modes for the wild end** and **a song's own coherence,** how far its facets stray from its base, by the same
  rule (`ByConvention`), should the wild end sound samey or a song incoherent.
- **The plain end's rigidity,** to tune by listening: no change of feel, triplet fills included, every bar of a plain
  song at the same density.

### Form and sections

- **A tag** (put off): half a pattern before a section changes, which breaks the pattern's grid for the fills' lines and
  the phrases; with the meter as state, an engine's change for a rare idiom.
- **The energy's recurrence** counts a section's appearances, not its plays: should long sections sound too quiet.
- **A bar of two beats before a section,** which would make the bars of a section unlike each other; and swing by a
  section's own meter, which the song's meter decides now.
- **The chords and the bass,** which follow the energy weakly, should choruses not sound fuller; the melody sings a
  section's energy over the song's in its range's place (`MelodyLayers.EnergyRegister`, `CompositionStateKinds.LineLift`),
  the chorus 2.6 semitones over the verse, higher in 61% of the songs; a bigger lift leaps into the section more.
- **The hook at the end** (P3): a song opens on its chorus's first phrase now and then (`IntroKind.Hook`); an outro
  quoting it too, should endings sound unrelated to the song, though the last section is most often the chorus already
  and a fade replays it. The hook intro plays the whole band, a chorus's rests and its solo being the song's later
  edits by section; should a full band before a sparse verse jar, the intro could take the first chorus's rests.
- **A section's parts chosen as its drum kit is** (`DrumKitGenerator.SelectKit`), leading and colouring roles, where
  now each part rests on its own (`Arrangement`).
- **The drums' changes by an appearance's energy** (their colour, a drum's stroke), which are the section's as it was
  made.
- **The tempo leaning by the energy** (P3, should the songs sound metronomic): a section's tempo nudged a percent or two
  by its energy through its tempo layer by `Tilt`, never in a plain song, which keeps to the click; no drift of its own.
- **A key change's common tone** (P3): a line's note held over the change into the new key, which the lines' placement
  does not make; the pad perhaps carrying it.

### Fills and lines

- **Filling the melody's gaps before a chord change within a phrase,** should they sound empty now that the solo part
  answers.
- **The lift beyond the cues:** the chords' rhythm doubling, a part of the next section coming in early, should the
  crescendo and the drums' cues not lift.
- **The fills' loudness** (P3): a run's swell, its accents and a landing's hit are constants, where the groove's
  loudness is layers; a fill's velocity layer would make them cumulative with the section's.
- **The fills' sounds by convention** (P3): runs and landings weigh a drum's sounds the same in a plain section as in a
  wild one; the weights could lean so that plain sections crash and wild ones reach for the china and the splash.

### Instruments and expression

- **The playing following the articulation** (P3, after genres): octaves popped in slap, short notes muted, without
  which an articulation is only another sound.
- **A solo's loudness by its phrase** (P3): the melody's, counter-melody's and riff's lines lean louder at a phrase's
  highest note and softer at its lowest (`LinePattern.PhraseArc`), a phrase running to its held note; a solo's lines
  are placed apart (`SoloLines`) and play without it, which they could take by the same rule should they sound flat.
- **Plain doubling,** a part doubling another in unison, which waits for timing by role.
- **A motif developed further** (P3), inverted or stretched, should the solos' sequences sound mechanical.
- **Modulation** (P3, when a soundfont maps it): CC1, which none of the soundfonts offered maps to tone. The rule to
  keep: modulation and a vibrato never at once, modulation and bends together.
- **Legato slides** (P3): a slide strikes its note anew, where a player's note before goes on sounding into the next;
  holding the note before into the slide and leaving the next unstruck edits two notes at render. Built if the struck
  slide sounds like a stutter.
- **Ping-pong echo** (P3): an echo alternating sides, from the part's pan to its mirror, where a part's echo now
  repeats in place.
- **Long fall-offs** (P4): a fall of an octave into nothing at a note's end, a slide's kind of its own, if missed.

### Groove

- **Timing by role** (P3): the backbeat a little late, the hi-hat on top, should swing alone sound stiff; plain
  doubling needs it. Then **a genre's pocket**: the offsets a genre's values, a laid-back backbeat or a pushed bass.
  Offsets a role each only, never a random jitter, which would be playing a player rather than a rule; never in the
  plainest songs, which keep to the click.

### Scales and chords

- **A chord's own scale** (P2, a plan in steps). Every chord's notes are snapped to the section's scale
  (`Realizer.SnapChordToScale`), so a blues cannot play I7, IV7 and V7 (IV is IVmaj7 in major, and even Mixolydian gives
  only I7), a minor key has no major V but at its cadence, and there are no secondary dominants (V/V, V/ii) and no
  borrowed ♭VII, ♭VI or iv in major: the commonest chromatic sounds of every genre but the plainest. The rule: a chord
  sounds in a scale, the section's own, or the section's with some steps a semitone up or down. Its alterations come
  from one of two places: a mode on the home, borrowed (Aeolian in a major song gives ♭VI, ♭VII and iv of the roots it
  already draws), or a mode on the chord's own root, applied (Mixolydian on a root a fifth above the next makes it that
  chord's dominant; on every chord, the blues). Every note over the chord takes it, the chord's, the bass's, the
  melody's, the counter-melody's and the solo's, as `Realizer.GetChord` gives them the cadence's raised seventh now
  (`StateKinds.ScaleAlterations`), which is the first case of it: harmonic minor borrowed at the cadence. It takes in
  a line scale by the chord, a line's passing notes taking its chord's scale, and chord tones off the scale. Steps:
  1. Done: a chord's alterations (`ChordScales`, `StateKinds.ScaleAlterations`), applied and borrowed
     chords drawn by the progression facet, never at the plain end and as often at the wild end as the middle; 7% to 12%
     of the chords but the cadence altered, two or three borrowed for every applied (`ChordScalesReportTest`); the
     melody's notes on a beat a semitone from the chords 7.5% before, 7.2% after. A key change's chain and pivot chords
     play the section's own scale in their keys, which the cadence's raise used to leave under them. The chances are a
     guess to tune by listening, the borrowed mode mostly Mixolydian (♭VII) in a middling major song and the minor's in
     a plain one. A chord leaves the scale only where one of its own notes moves, root, third, fifth or seventh, a
     quarter of the borrowed chords having moved only the notes between them: 4% to 7% of the chords borrowed by the
     progression facet's fifths, 3% to 5% applied.
  2. Done: a genre's (`GenreChoices.ChordScales`): applied and borrowed chords' chances, and every chord but the home
     its own dominant, which the songs never play; Blues 67% of its chords dominants (the rest its homes), Jazz 17%
     applied and 5% borrowed, Rock 11% borrowed (`EveryPreset_ChordScales`).
  Decided: an applied dominant keeps the shape drawn for it, which its Mixolydian makes a major triad, a dominant
  seventh or a suspended dominant; forcing a seventh would override the shape's draw for the stranger shapes alone.
  Musicality to listen for: a melody holding the section's note over a borrowed chord (the step's measure of clashes
  stands for it), and an applied dominant whose resolution the next chord's shape spoils.

- **Scales of other sizes** (P3, taken from *Parked*): whole scales of more or fewer than seven notes, pentatonic,
  hexatonic (whole-tone, the blues' six) or octatonic, for a section's harmony as well as its lines, at the wild end
  of the scale facet alone. The progressions move by scale steps and their rules count a fifth as three steps, the
  chord shapes' heights sit between the qualities a seven-note scale gives, and the scales measure their brightness
  and their distance against others of as many notes (`Scales`): a fifth as the step nearest seven semitones, a
  chord's height snapped as now (`SnapChordToScale` takes any length), and a scale's distance only among its own
  size. Steps: the engine's assumptions of seven found by a trial forcing every wild section into a pentatonic, as the
  12-bar's trial found the phrases'; then the scales drawn. The line scales give pentatonic, whole-tone, octatonic and
  blues lines already; this is their harmony, a narrow sound, so it waits on listening to the wild end.

### Rhythm engine

- **Long cycles** (put off in October 2026): a bar pattern is one bar long, so a slower cycle, such as the kick's slowed
  to two bars, plays its first half and starts again at every bar line. Patterns as long as their cycle would let slow
  figures run whole, a crash every two bars, a kick figure answered in the second bar, a chord of two bars. Put off as
  an engine's change: the bar is the unit of the scheme's letters, the state's layers, the melody's question and
  answer, the drums' presence, the fills' lines and the feels. A crash every two bars, should it be missed, an edit
  after assembly as the lift's cues are.

### Architecture

- **The song's edits as named stages, and what they share** (P2, a plan in steps). The song after assembly is a
  sequence of edits whose order is their correctness and is said nowhere (`SongGenerator.GenerateSong`, 464 lines, 69
  streams): October 2026's bugs were all an edit not knowing another's (a key change keeping the old chord's scale,
  the restatement overriding the bass's landings, a length not taking in the intro). Steps:
  1. Done: a chord's scale one kind its lowest layer decides (`StateKinds.ScaleAlterations`, `StepAlterations`), the
     section's chords setting it and a key change's chain and pivot chords setting the key's own deeper, which wins;
     the fingerprint kept.
  2. Named stages: what the song decides before assembly (its plan, structure, meters, hook, energies, parts,
     facets, genre, streams) one record it reads, and the song after it a draft of its timeline and what stages hand
     on (the fill runs, the melody's walks, the key changes, the drum solos), each field with who sets and reads it;
     a fixed list of about fifteen stages, each traced by name with how many notes it changed; one turned off for a
     test where it only edits the timeline. No order to configure, no graph of dependencies: the order is the music's.
     A refactor: the fingerprint kept; a report of what each stage changes.
  3. The rules a song keeps in one suite run on finished songs (every note in its chord's scale, landings on the
     root, a length within bounds, nothing in a part's rest), structural checks between stages (positions within the
     song, notes in order), and the stage report to tell which stage moved what.
  4. Tests as rules or calibrations: a rule asserts what must hold; a calibration keeps a wide bound against the
     catastrophic and its target in an explicit report, written into CLAUDE.md; the floors this round moved converted
     first, the rest as they move.
  5. A hook intro takes the rests of the chorus it quotes, rather than the intro made an appearance of its own,
     which every reader of the map's sections would have to learn.
  Deferred: `SectionGenerator.Generate` (188 lines) split likewise, its draws already apart by their streams, should
  the song's stages show a gain there.

- **The page's wait for a remake** (P3): a song is asked for again 400 ms after the settings last changed; a remake let
  go of frees its core on the server, so the wait could be 200 ms. To shorten if the wait is noticed.
- **The sections on the seek bar** (P3): the report gives where each section starts, in seconds, with its role and any
  solo, and the song's length; the page marks the starts on the seek bar, named on hover, and shows the length before
  Play, where it now shows 0:00. Put off from the UI round of October 2026.
- **The ratings in the history** (P4): the list of songs so far does not show which were liked or disliked.
- **A mix written by the page** (P4): a change of the mix asks the server for the file again only for the download;
  writing the mix into the file in the page would leave the server out of it, at the cost of MIDI writing in
  JavaScript.
- **The server's figures read in full** (P4): the dashboard reads every song made in its window, nothing at hundreds a
  day and slow at the 200,000 a day the table keeps at most; then the spreads would be worked out a day at a time.
- **The drum groups' pulses fading by their sounds** (P4): every group fades as fast as the parts' meters, a cymbal's
  wash as a click; a fade of each group's own would follow its sound, for little.
- **The page's tests of sound** (P4): the page tests drive asking for songs, not hearing them; playing, seeking, the
  MP3 export and the lock screen's controls are tried by hand, as are Safari and phones.
- **A report of thousands of songs** would make, measure and let go of its seeds past the shared corpus rather than
  cache them; a corpus song holds about 5 MB.

## Genres

Built: a genre is the page's data, pools of the options of the choices the songs draw, chances and rhythm values
(`Genre`, `GenreChoices`, `GenreRhythm`), every option three amounts in whole percents, its plainest songs', its
middling ones' and its wildest ones' (`ByConvention.Weigh`), the songs' own served as the choices' (`GenreOption.Own`);
ten presets (`Client/genres.json`), their idioms at every end and their experimental directions at the wild end alone;
a picker and an editor on the page, every choice a graph of what the songs draw of it and its override
(`/api/genres/previews`), changed in a window over it, its options picked and removed and their amounts dragged
(`genre-tune.js`); a genre kept on the server by an 11-character key of its canonical form (`GenreStore`), which
a link carries. What is left:

- **Sections of several phrases: the 12-bar blues and its kin** (P2, a plan in steps). A section's pattern is a
  phrase of four bars now (Progressions.BarCount, Meter.PatternBarCount), its harmony a four-bar cycle. The rule: a
  section's progression is P phrases of four bars, P drawn, one as now, two (an 8-bar), three (the 12-bar), four (a
  16-bar); each phrase starts from a degree by its place and ends by its place, the first from home, a middle one
  leaning to the subdominant, the last to the dominant, every phrase but the last ending home or on a half close, as the genre leans it, and
  the last on the cadence, so that the blues' I–I–I–I / IV–IV–I–I / V–IV–I–V comes of the place rules and its root
  motions, and a pop 8-bar's half close of the same rules. Phrases stay four bars, so a line's question and answer,
  a fill and a solo's turn keep their place; only the harmony and the section's length grow. Steps:
  1. Done: a phrase (Meter.PhraseBarCount) and a section's pattern (Meter.Phrases of them, one everywhere) told apart.
     A line's question and its answer are two plays of the pattern (LinePattern), which step 4 takes up.
  2. Done: a progression over P phrases (Progressions.Generate, its place rules data); the phrases' ends home about 60%
     of the time, the chord before an ending free, which the genre's lean of an ending may press, should listening ask.
  Tried with every section three phrases: every song made, nothing thrown, the bar states and the progression twelve
  bars long; but 27 tests fail, among them the measured ones a longer song changes and, what is wrong, the pitched
  tracks' chord shapes apart past the first phrase ("PitchedTracks_ShareTheChordShapeAtTheSameTime", at beat 72) and the
  melody 1.8 notes a bar where 3 at least are (its question and answer each a pattern, LinePattern, its notes a
  pattern's first phrase): a track's bars are made a phrase at a time. So before P is drawn:
  2b. Done: a track's bars across every phrase; P a genre's (GenreChoices.Phrases), a section's plays drawn in
     phrases; the tests of a pattern's bars by the section's own. With every song's sections twelve bars only what
     fewer, longer patterns change fails: a length given less near, no sequence or late drums (each wants a second
     pattern), the answer measured nowhere (it waits on 2c), fewer phrase lines.
  2c. Done: a pattern's last phrase answers (LinePattern.Answers). Measured in twelve bars: the second phrase keeps 98%
     of the first's rhythm, the third 86%; but the second keeps 36% of its contour (the step to the next note), the
     notes drawn afresh over each phrase's chords. Done since: a middle phrase sings the first's notes where they fit
     its chords, a chord's note on a strong beat and its scale's on another, its own where not (`LinePattern.Restate`),
     every line alike but where a note lands on its chord, as the bass on the IV's root, which keeps its landing
     (63% of the bass's changes on the root in a middle phrase, as elsewhere): 60% of the second phrase's notes
     the first's (19% before), its contour 52% (36%).
  6. Done: the presets' patterns (Blues twelve bars 81% of its sections, Jazz and Folk eight, Rock four and eight),
     reported by EveryPreset_Phrases. Left to listening: Blues' V at bar 9 43% of its twelve-bar sections and home at
     bar 4 48% (IV at 5 63%, home at 8 61%), the middling and wild songs keeping the place rules less, by the
     progression's strictness; a genre's lean of the place rules, should it press them, and whether the songs' own
     should play eight bars now and then.
  Later: phrases of other than four bars (a 3-bar phrase, a 6-bar section), which the line's question and answer
  and the fills assume, once P phrases sound right.
- **A head with solos** (P3): a jazz tune's head, choruses of solos over its form, and the head again, which AABA
  (SongForms.Forms) and the solos' sections (Solos) come near already; a form of its own only should listening ask.
- **A genre's levels** (P3): how loud each part plays against the others, values a part each, applied where the mix
  gives none, a mechanism of its own that a mix given may fight; the pan is a genre's (GenreChoices.Pan), the levels
  only should listening ask for them.
- **A genre's sections differing** (P3, perhaps never): a value given a section role, such as a solo swung harder
  or a chorus louder, the genre's for the song otherwise; the generator asking the genre with the section's role,
  every choice's tile a role's override beside it.
  The dynamics' contrast and the phrases' AAB give most of it; what is left, a solo swung harder, weighed against a
  role's override on every tile once 3 to 8 are heard.
- **Listening to the presets** (after effects, the phrases, the forms and the mix, which change them): each against its sentence (`GenrePresetsReportTest`). Reggae's one drop is a kick
  half a bar off its cycle, which the report cannot tell from beat 1, and Latin's tresillo is the time keeper's grouping
  alone; the rhythm values mean what they make together, and a preset's are tuned by where its notes fall.
- **Listening to the genres' wild ends:** the presets' wild ends keep their idioms most and stray by what each names
  experimental (`EveryPreset_ChoicesByEnd`): the wildest Rock 34% in 4/4, its chords 7% quartal or tritone stacks; the
  wildest Jazz 56% swung, 50% sevenths, about a fifth So What, quartal, altered or eleventh; Electronic 11% quartal
  triads or clusters. A home and a cadence lean the genre's shapes by their own over the songs', and where the songs
  never play a shape at an end the genre's weight alone (`ByConvention.Lean`), so that at the wild end a home is a
  genre's plain shape or one of its strangest, never a level-2 one (a ninth, a thirteenth): a wild song's homes
  plainer than its other chords, which the experimental shapes' amounts may be raised against, should listening ask.
- **How the wild end swings:** a swing is as likely in the wildest songs as in a middling one, and the principle has
  the wild end choose how it is played instead, such as a heavier swing or one past the triplet's; the amount is drawn
  alike at every conventionality now (`Groove.Generate`).
- **The rhythm's values against the part's own:** a value moves a setting by steps from where the part rests, so that
  one part's "faster" is another's "as fast as can be"; a value set outright, where it lands, would read the same for
  every part, should listening ask for it.
- **A link replacing the settings kept** (a decision): opening a shared song makes its settings this browser's own,
  so that the next song goes on in them; keeping one's own and offering a way back to them is the other way, should
  listening to shared songs ask for it.
- **Later:** the playing following the articulation (see *Open work*), and a genre's own after it; instruments outside a
  role's pool, with their leading drawn from their family; phrases of other than four bars, once the phrases sound
  right.

## Parked

Ideas with a reason, not planned: none has shown a need. Taken up only when listening or another item asks for one.

- **Note keys** are hashed seeds where a plain key of the bar pattern, the cycle and the place would do: readable, but
  nothing needs them read.
- **The drummer as one player:** the fills' `Drummer` and the drum solo's `Soloist` one record, a refactor with no
  sound of its own, until the two drift apart.
- **Segments in any pattern:** bars that mix feels, such as three straight beats and a quintuplet beat; a fill's speed
  change, a rank more for one half of its span, would be a case of it.
- **The drummer as state:** the favourite walk as a pool like the chord pool's, and busyness as a fullness and a span
  length that the energy adds to.
- **A drum's sounds together:** a drum is one track, whose note plays one sound, so a run never plays two toms
  together; notes of several sounds on one track would allow it.
- **The vibraslap's pickup,** the last beat before a landing.
- **Patterns by measured features** (syncopation, evenness) as a family, should a target prove out of reach of the
  dyadic engine; euclidean patterns that fit no cycle; a library of idioms such as clave and bossa, which genres may
  take up; and drums generated together, the snare avoiding the kick and the hi-hat filling the gaps.
- **An endless song:** sections generated afresh as it plays, with no ending. The sections generated per appearance
  are a step towards it; the form's plan, its intro and ending, and the song's single pass are not. The time is no
  obstacle: an hour's song asked of the generator is made in one to two seconds, but plays 33 to 45 minutes at 120
  bpm, its form's parts running out (`SongLength`'s free form of up to 32), and the page's length stops at fifteen
  minutes (`LengthScale`); an hour and more is the endless song's, not the length's.
- **Memory:** every `RealizedNote` keeps the state it was decided from, about 1.7 MB a song: nothing to do.

## Decided, and kept

- **The tables** that are how often musicians do a thing (the fills' spans and treatments, the landings, the intros
  and endings, the progressions' homes and root motions, the cadences, the scales, the bass's approaches and arrivals,
  the drums' weights, the phrase ends) are convention the draws are pulled towards, and stay tables.
- **The drums' articulation offset stays a list:** every layer's fraction of a drum's sounds is rounded to whole
  sounds and added, so that a layer moves every note under it alike, which an additive number rounded once would not.
- **The scale and a chord's shape are single values that are lists,** set by one layer each (`SingleValuedListsTest`);
  a kind of their own would add a mechanism for a mistake not made, and forbid a section's scale overriding a song's.
- **The chords and the bass are not placed at generation** as the melody is: they already repeat their pitch classes
  where a section plays again, and `Realizer` only chooses their register.
- **The kit's leads among themselves,** the snare and the hi-hat, keep their own feels over the drums' shared layers;
  only the drums that do not lead follow a lead's (`FeelLeads`).
- **A drum keeping time plays grouped cycles (a tresillo) unleaned,** as the other drums do: its convention is a
  genre's to say (see *Genres*).
- **A polymeter's cells are not accented by themselves:** a cell's first note is the motif's, on the frame's downbeat,
  and carries its accent already (0.52 of velocity against the rest's 0.15).
- **The state is realized per map:** of the songs' own time a quarter is realizing notes, each map's state timeline
  built once (`StateTimelineMap`), which is how the state is designed rather than a hot spot to fix.
- **Seeds are not stable across versions;** the version labels what was heard (see *Listening data*).
