# Roadmap

What is to be done, what listening is to settle, the ideas parked and the decisions that keep what is built so. What
is built is described where it is built, in the code, and its numbers are in the commits that made it; an entry here
is removed once it is built.

## Priorities

**Next** (P1): listening to what is deployed, 0.6 and on, and tuning by what it says (see *Listening*), and the
ratings by conventionality once enough are in, alongside, continuously and never as a gate.

**Later** (P2), roughly in this order: the ratings report (see *Listening data*), once the dashboard shows songs rated
enough; listening to the genres' presets (see *Genres*).

**When the need shows** (P3), each by what listening says, roughly in this order: timing by role; the tempo leaning by
the energy; a line scale by the chord and chord tones off the scale; legato slides; a motif developed further; the
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

- **A song's length, the rest:** a short length runs long, a quarter of the songs given a minute past 1:09 and a
  fifth of those given two past 2:18 (`SongLengthReportTest`), as an intro, an outro or a fade adds a section the
  length leans no draw of; their chances leaned by the length as the form's are, should it matter. Long songs, ten
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
- **The melody's register by the energy,** which a shift of the contour did not move, as the line follows its aim only
  weakly; and **the chords and the bass,** which follow the energy weakly, should choruses not sound fuller.
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
  doubling needs it.

### Scales and chords

- **A line scale by the chord** (P3): at the wild end, a line changing its scale from chord to chord, as a player does
  over changes, rather than one scale on every chord's root through a section.
- **Chord tones off the scale** (P3): an altered or split chord sounding as itself in any scale needs a chord's notes
  altered as the bass's approach notes are (`StateKinds.Alteration`), rather than snapped.

### Rhythm engine

- **Long cycles** (put off in October 2026): a bar pattern is one bar long, so a slower cycle, such as the kick's slowed
  to two bars, plays its first half and starts again at every bar line. Patterns as long as their cycle would let slow
  figures run whole, a crash every two bars, a kick figure answered in the second bar, a chord of two bars. Put off as
  an engine's change: the bar is the unit of the scheme's letters, the state's layers, the melody's question and
  answer, the drums' presence, the fills' lines and the feels. A crash every two bars, should it be missed, an edit
  after assembly as the lift's cues are.

### Architecture

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

- **Listening to the presets** (P1): each against its sentence (`GenrePresetsReportTest`). Reggae's one drop is a kick
  half a bar off its cycle, which the report cannot tell from beat 1, and Latin's tresillo is the time keeper's grouping
  alone; the rhythm values mean what they make together, and a preset's are tuned by where its notes fall.
- **A link replacing the settings kept** (a decision): opening a shared song makes its settings this browser's own,
  so that the next song goes on in them; keeping one's own and offering a way back to them is the other way, should
  listening to shared songs ask for it.
- **A range's axis by its scale** (P3): the window's chart of a range is linear in its numbers, so that a length's
  from half a minute to fifteen keeps a song's few minutes low on it; its scale's own spacing, as the amounts have it,
  should the editing ask for it.
- **One's own genres kept straight** (P1, small):
  1. **Renaming:** Save keeps the draft in place of the genre it was opened from, under its name as it now is; Save
     as alone adds one; a name another of one's own has asks before replacing it.
  2. **Delete asks**, as closing with changes does: nothing undoes it.
  3. **Names one whatever their case and spacing:** saving, finding one's own (isOwn) and the list compare them so,
     as Duplicate's numbering already does.
  4. **Keep a genre a link brought:** its button Keep, among one's own under its name, or its name and the first
     number free, then Edit; a song's genre played unsaved stays Edit.
  5. **Every genre of one's own as one file:** Import reads a genre or a list of them, each kept under its name or
     the first number free; Export all beside Export, the list as JSON, so that a browser's genres survive it.
- **A range's chart as one band** (P2): its ends and its lean on one chart, the band between the from and the to along
  the songs, a line where its draws centre (the lean's median, (1/2)^(e^-lean) across it) and the band shaded by
  how likely each height is, a vertical gradient a slice, a few dozen slices; handles on its edges for the ends and on
  the line for the lean, the lean's own chart gone; the tile drawing the same, its fill a gradient.
- **A genre's mix** (P2): how far each part sits from the middle, a lean of its role's spread (Panning.GetSpread), and
  how loud each plays against the others, values a part each, applied where the mix gives none, so that a given pan
  or volume stays as given; reggae's bass up, a jazz trio's piano and bass apart, ambient's pads wide.
- **A genre's song forms** (P2): the forms songs are written in (SongForms.Forms), verse-chorus, with a pre-chorus,
  with a bridge, a pool a genre weighs, and a form of its own as a chance, so that folk keeps verses and choruses and
  jazz plays AABA once it is among them; AABA and a head with solos, the forms to add.
- **Sections of several phrases: the 12-bar blues and its kin** (P2, a plan in steps). A section's pattern is a
  phrase of four bars now (Progressions.BarCount, Meter.PatternBarCount), its harmony a four-bar cycle. The rule: a
  section's progression is P phrases of four bars, P drawn, one as now, two (an 8-bar), three (the 12-bar), four (a
  16-bar); each phrase starts from a degree by its place and ends by its place, the first from home, a middle one
  leaning to the subdominant, the last to the dominant, every phrase but the last ending home or on a half close and
  the last on the cadence, so that the blues' I–I–I–I / IV–IV–I–I / V–IV–I–V comes of the place rules and its root
  motions, and a pop 8-bar's half close of the same rules. Phrases stay four bars, so a line's question and answer,
  a fill and a solo's turn keep their place; only the harmony and the section's length grow. Steps:
  1. **Phrase and pattern told apart** (a refactor, the fingerprint kept): Progressions.BarCount becomes the phrase's
     four bars, and a section's pattern P of them, its meter's PatternDuration P phrases, P one everywhere; each of the
     uses (SectionGenerator, BarStateGenerator, PatternGenerator, LinePattern, MelodyLayers, KeyChange, Solos,
     FillGenerator, SongMap.PatternBarAt, Polymeter, FormLayers, SongFormGenerator, SongLength) read as phrase or
     pattern by what it means.
  2. **A progression over P phrases** (Progressions.Generate), the place rules above as weights on the roots a
     phrase starts and ends on, leaning as the cadence's do, by the progression facet's strictness; P one drawing
     exactly as now, so that the fingerprint holds.
  3. **P a genre's** (a pool, "Phrases a progression": 4, 8, 12, 16 bars), drawn a section from a stream of its own;
     the songs' own one alone, so that a song in no genre is as it was; a section's plays counted in phrases, so that
     a 12-bar plays once or twice where a 4-bar plays four times, and its length as near (SectionLength, SongLength).
  4. **The melody across phrases:** the first phrase states, a middle one restates it, the last answers (AAB), the
     blues' lyric form, as a line's answer bars are now (MelodyLayers.AnswerBars) but by the phrase's place.
  5. **Fills and solos by the pattern:** a fill's largest at the pattern's end, its smaller at a phrase's; a solo's
     chorus a pattern (Solos), so that a blues solo takes 12-bar choruses.
  6. **Presets and listening:** Blues three phrases most, Jazz two (an 8-bar A), Folk two, Rock one or two; a report
     of the roots at each phrase's start and end by preset (the blues' IV at bar 5 and V at bar 9 most of the time)
     and of sections' lengths; then whether the songs' own should play two phrases now and then.
  Later: phrases of other than four bars (a 3-bar phrase, a 6-bar section), which the line's question and answer
  and the fills assume, once P phrases sound right.
- **A genre's effects** (P2): the chances of a vibrato, bends, a chorus, a swell, a tremolo, an echo and a sweep
  (Expression), a song's drawn by its sound facet, a genre's to lean, as the swing's chances are.
- **A genre's dynamics** (P3): how loud and how busy its songs and the contrast between their sections, values leaning
  the energies drawn (SectionEnergy), so that ambient stays level and rock's choruses lift.
- **A genre's sections differing** (P3, after the rest): a value given a section role, such as a solo swung harder
  or a chorus louder, the genre's for the song otherwise; the generator asking the genre with the section's role,
  every choice's tile a role's override beside it.
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
- **Later:** the playing following the articulation (see *Open work*), and a genre's own after it; instruments outside a
  role's pool, with their leading drawn from their family; ratings by genre on the dashboard, which the events carry (a
  preset's id or a genre's key, with the version).

## Parked

Ideas with a reason, not planned: none has shown a need. Taken up only when listening or another item asks for one.

- **Scales of other sizes:** whole scales of more or fewer than seven notes, where the progressions' rules would need
  their steps as fractions of the octave. The line scales give the melodies and riffs pentatonic, whole-tone,
  octatonic, chromatic and blues notes already, and harmony in such scales is a narrow sound for the largest change the
  engine could take.
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
