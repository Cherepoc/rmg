# Roadmap

What is to be done, what listening is to settle, the ideas parked and the decisions that keep what is built so. What
is built is described where it is built, in the code, and its numbers are in the commits that made it; an entry here
is removed once it is built.

## Priorities

**Next** (P1): listening to what is deployed, 0.6 and on, and tuning by what it says (see *Listening*), and the
ratings by conventionality once enough are in, alongside, continuously and never as a gate.

**Later** (P2): the ratings report (see *Listening data*) once the dashboard shows songs rated enough, and listening
to the presets and to what October 2026 gained (genres that sound like themselves, the pocket and the strum, builds and
drops, turnarounds and passing chords), alongside.

**When the need shows** (P3), each by what listening says: the items left in the sections below, each marked P3.

**Put off** until their uses ask for them: long cycles and a tag (see *Open work*, *Rhythm engine*).

**Parked** (P4): see *Parked*, kept for their reasons, not planned; and, parked where they stand: scales of other
sizes, a melody in block harmony, a hook at the end, the tempo leaning by the energy, a key change's common tone, the
fills' loudness as layers, and a head with solos (which the form's devices by genre with AABA likely make).

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
- **A melody written as a hook** (done): a section's melody on a hook, its repeated bars singing their letter's first
  bar's notes where they fit their chords, a landing as it lands (`LinePattern.Hook`), by its role (a chorus 60% at the
  plain end and the middle, 30% at the wild, `MelodyLayers.HookChanceOf`) and every section's by a genre's
  (`GenreChoices.Hook`, Hip-hop 80%, Electronic 70%). Of a hooked section's repeated bars' notes at their first bar's
  places, 50% sung as it (13% unhooked); whole bars alike 7% (0), a strong beat's note changing with its chord. Its
  phrase schemes were not the lack: 82 to 100% of the phrases repeat their bars already.
- **The band's devices** (P3, each an edit after assembly among the stages): stop-time breaks done (`SongStages.StopTime`,
  `GenreChoices.StopTime`), a section's appearances' first phrase, the band hitting the bar's first beat under the
  lines, a quiet section the likelier; sections in it Blues 35%, Funk 22%, Rock 19%, others 5%. Its hits are the
  band's own downbeats, none added where a part rests on one, and a phrase's last bar keeps no fill; both should
  listening ask. Pushes done (`SongStages.Pushes`, `GenreChoices.Push`): a pushing section's chord changes on the bar
  played an eighth early by the band's harmony, the drums keeping time and the lines going on, never in a solo, in
  stop time or at a part's entry; sections pushing Funk 39%, Latin 30%, Rock 12%, others 8-9%. The drums' push (a crash
  and kick on the and of four) should listening ask. Then a backing figure that recurs through the song. Call and
  response between parts postponed (October 2026): an edit after the notes that keeps the counter-melody to the
  melody's rests and held notes, note by note or bar by bar, kept 12% and 15% of its notes, the melody leaving no room;
  the counter-melody's rhythm placed against the melody's gaps when it is drawn, as the solo part's answers are
  (`SoloAnswers`), is the way, should listening ask.
- **Harmony's devices** (P3): a ii–V into a chord but the home, which a chord's own scale makes part of. A chromatic
  line inside a held chord done (`SongStages.InnerLines`), the line cliché: over a chord two bars long or longer, the
  chords' root voice falling a semitone a quarter (root, major seventh, seventh, sixth), a section's by its chords facet
  (0, 0.15, 0.3), never in a solo; 11% of the corpus's songs sound one (chords held two bars are a quarter of the
  sections). Rising (the fifth, its sharp, the sixth) should listening ask. A pedal under a section done (`SongStages.SectionPedalChance`, the key change's
  pedal generalized, `KeyChange.Pedal`): a section's appearances' first phrase over its bass holding its home or its
  dominant, by its progression facet (0, 0.06, 0.15); 4.8% of the corpus's sections, 18% of its songs.
- **The hook at the end** (P4, parked): a song opens on its chorus's first phrase now and then (`IntroKind.Hook`); an outro
  quoting it too, should endings sound unrelated to the song, though the last section is most often the chorus already
  and a fade replays it. The hook intro plays the parts its chorus plays, its rests and texture its
  appearance's; a chorus played as a solo is quoted by its tune.
- **A section's parts chosen as its drum kit is** (`DrumKitGenerator.SelectKit`), leading and colouring roles, where
  now each part rests on its own (`Arrangement`).
- **The drums' changes by an appearance's energy** (their colour, a drum's stroke), which are the section's as it was
  made.
- **The tempo leaning by the energy** (P4, parked): a section's tempo nudged a percent or two
  by its energy through its tempo layer by `Tilt`, never in a plain song, which keeps to the click; no drift of its own.
- **A key change's common tone** (P4, parked): a line's note held over the change into the new key, which the lines' placement
  does not make; the pad perhaps carrying it.

### Fills and lines

- **Filling the melody's gaps before a chord change within a phrase,** should they sound empty now that the solo part
  answers.
- **The lift beyond the cues:** the chords' rhythm doubling, a part of the next section coming in early, should the
  crescendo and the drums' cues not lift.
- **The fills' loudness** (P4, parked): a run's swell, its accents and a landing's hit are constants, where the groove's
  loudness is layers; a fill's velocity layer would make them cumulative with the section's.
- **The fills' sounds by convention**: done (`DrumSound.Wildness`, `FillSounds`): runs and landings weigh a sound by
  its ends, the china and the splash never at the plain end and twice their weight at the wild; of the cymbal's hits
  0% china or splash in plain sections, 1.9% in middling ones, 4.3% in wild ones.

### Instruments and expression

- **The playing following the articulation** (P3, after genres; palm-muted chugs in *Genres*, step 2c): octaves popped in slap, short notes muted, without
  which an articulation is only another sound.
- **Plain doubling,** a part doubling another in unison, now that the parts play by their pocket (`SongStages.Timing`).
- **A motif developed further**: inverted done (`Soloist.MotifInversion`, by the melody facet (0, 0.3, 0.5)): a phrase
  answering the motif before mirrors its intervals about its first note now and then rather than moving it a step; a
  middling solo's phrases opening on the motif before, moved or mirrored, 63%. Stretched (its rhythm doubled) should
  listening ask.
- **Legato as an articulation**: done (`CompositionStateKinds.Legato`, the Articulation stage): a line's part (the
  melody, counter-melody, solo or riff) slurring its steps, by the sound facet (0, 0.4, 0.6), never the plainest songs:
  a note two semitones or less from the note before, within its phrase and a sixteenth or less after it, marked slurred,
  the note before held into it, General MIDI striking it 15% softer; 16% of a middling song's line notes. Left to check:
  whether a solo's runs group in fives and sevens, a fusion player's, which the feels give a whole part but perhaps not
  a run; if not, a case of segments in any pattern (see *Parked*), not a mechanism of its own.
- **Legato slides** (P3, a case of legato, now done): a slide strikes its note anew, where a player's note before goes on sounding into the next;
  holding the note before into the slide and leaving the next unstruck edits two notes at render. Built if the struck
  slide sounds like a stutter.
- **Long fall-offs** (P4): a fall of an octave into nothing at a note's end, a slide's kind of its own, if missed.

### Groove

- **A drum's pocket by its section's role** (P3, if a section's change of role is heard): the timing takes a drum's
  offset by its role in the song's groove (`SongStages.OffsetOf`), not a section's, which may move a ride to the time.
- **A piano's rolled chord**: done (`SongStages.RollChance`, the strum's seam): a part on a piano rolling its chords
  held a beat or longer upward over 60 ms, a part's trait by the sound facet (0, 0.3, 0.5); 11% of a corpus's held
  piano chords. A strum's thinner up stroke, its lowest strings left out, if the up strokes sound as full as the down.

### Scales and chords

- **A chord free of the section's scale** (P3, in steps): every chord's root is a step of the section's seven-note
  scale and its own scale the section's a semitone a step at most (`ChordScales`), so a chord cannot move to an
  unrelated key, its root cannot move by a semitone or a minor third, and it cannot take a scale of its own. A chord of
  its own kind, answering `Realizer.GetChord` as the others do, so that the lines and the chords read it unchanged;
  never a replacement for the scale-step chords, whose progressions, cadences, key changes and bass landings count
  roots in steps; the scale's test excepting it as it does a bass's approach. Steps:
  1. Done: a chord moved by semitones (`StateKinds.ChordShift`, applied where `Realizer.GetChord` builds it), its shape,
     its scale and every note over it moved with it; a key change's chain playing its chords unmoved. Its first use a
     chromatic passing chord, the next chord a semitone above (`PassingKind.Chromatic`), by the progression facet, a
     fifth of the passing chords in the middle, every one at the wild end but a third. Not done: a constant structure
     planed through keys.
  2. Done as a constant structure: a section planing (`Planing`, `GenreChoices.Planing`), every chord the home's shape
     moved by a semitone, a whole step, a minor third or a fourth, within a tritone of home, a wild song's (0, 0, 0.25),
     never the song's last section; 26% of a wild song's sections, 0.2% of the corpus's. Not done: choosing the next
     chord by the voice leader's least move among free shapes, should planing sound mechanical.
  3. Done: a chord's own scale drawn on its root (`ChordScaleKind.Own`, `ChordScales.Own`): a mode that keeps its triad
     (Lydian or Mixolydian over a major chord, Dorian, melodic minor or Phrygian over a minor one), the lines over it
     taking its colour, a wild song's (0, 0, 0.3) on a stream of its own; 11% of a wild song's chords. Not done: the
     diminished scale, of eight notes, which the seven-note scales cannot hold.
  Its uses past an approach: a progression that does not function, planing in pop and film scores, modern jazz. A
  fusion genre, a long-lined guitar player's, then has its harmony and its lines as a genre's data (free chords, the
  line scales of melodic minor, the octatonic and the whole-tone from each chord's root, leaping, stretched voicings,
  swells, odd meters); not its tone, which General MIDI has not, its long legato soloing, which the solo's gestures
  (licks, runs, held notes) are not built for, its drumming and its melodic bass, gaps of their own.
- **Stretched voicings**: done as a voicing among the others (`ChordVoicing`, "Spread"): the lowest note kept, the
  second lowest two octaves up and the rest one, by the chords facet (0, 0.05, 0.5); chords over an octave 7.4% of a
  middling song's (0.4%), over two octaves 9% at 0.75 of the chords facet (0). Not done: a section holding its spacing
  rather than the voice leader drawing it anew a chord, should a spread chord's neighbours sound close.
- **Approach chords** (P2 for its first steps, a plan in steps): a chord leading into the next one that the
  progression does not have, by its kind (the target's dominant, a passing diminished between roots a whole step
  apart, the target's shape a semitone above or below, the tritone substitute), its length (a beat, half a bar or a bar,
  taken from the chord before) and its beat: a weak beat about the middle, and the strong beats too and chained at the
  wild end. A plain song is never reharmonised, by any step, a turnaround and a reharmonised repeat among them: the
  songs' chance 0 at the plain end, and a genre's choice of them offering no plain end, its lean the middle's and the
  wild end's alone, data on the choice rather than a cap on its chance. Not reharmonising: the chords off the scale a
  genre plays at its plain end (`GenreChoices.ChordScales`), the blues' dominants being the plain blues. One chord in the bar state, a change of its own, which every part takes: the lines
  land on it, the bass approaches it, the chords voice it; the uses differ in where it goes and who plays it. A
  dominant or diminished approach is a root and a chord's own scale, as an applied dominant is; a chromatic one is not
  (the scale's root, third and fifth raised together run its steps onto each other, which `AlterScaleSteps` refuses),
  and wants a state of its own, the whole chord moved a semitone, the lines over it keeping the target's scale. Steps:
  1. Done: turnarounds (`Progressions.TurnAround`, `GenreChoices.Turnaround`): a section's pattern's last bar a V into
     its start, a ii–V where the bar has two chords, by the progression facet (0, 0.1, 0.1) and a genre's, no genre's
     at the plain end (`GenreOption.HasPlainEnd`). Sections ending on V: Jazz 74% (39%), Blues 69% (29%), songs 36%
     (32%); on a ii–V Jazz 47% (25%). The V's seventh raised as a cadence's is, so modal at the wild end.
  2. Done: passing chords (`PassingChords`, `GenreChoices.PassingChords`): a chord a bar long or longer passing into
     the next by the next's dominant, Mixolydian on its root where the scale lets it, over its second half, by the
     progression facet (0, 0.1, 0.1), the energy and a genre's, never at the plain end; a change of the harmonic rhythm
     (`HarmonicRhythm.Passing`) that every part takes. Sections with one: Jazz 29% (its chords mostly shorter than a
     bar), Blues 24%, Folk 20%, Rock 13%. Not done: the passing diminished, a chromatic root, with step 4.
  3. Done: a chord solo's approach (`Soloist.Approaches`, by the melody facet (0, 0.5, 0.5)): a stacked note off the
     beat leading into a chord change planed into it, its shape moved with its note against the band's chord, a
     constant structure's approach rather than step 2's chord; 3% of a middling chord solo's stacked notes, 7% of a
     wild one's. Not done: on the strong beats at the wild end, and the dominant as the approach.
  4. Chromatic approaches: done from above as a passing chord (see *A chord free of the section's scale*); from below,
     and a chord solo's (step 3), should listening ask.
  5. Done: a later appearance reharmonised (`Reharmonisation`, a stage before the lines): the song's last appearance
     of a section heard before, by the progression facet (0, 0.25, 0.25), half its chords a third below (vi for I, ii
     for IV) or above (iii for I), sharing two notes, in the section's own scale; a key change's chords, a moved chord and
     the chord it slides into kept. 20% of the songs, 9.5 chords each. Not done: the tritone substitute for V7.
  6. A melody in block harmony (P4, parked), the counter-melody or the pad voicing its long notes, approaches on its passing
     ones, as a horn section's soli.
  Today a chord solo stacks the band's chord or planes its shape, never another chord; the bass approaches any change.
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

- **Scales of other sizes** (P4, parked: the line scales give their sounds where they are heard): whole scales of more or fewer than seven notes, pentatonic,
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
  2. Done: the stages after assembly (`SongStages`: key changes, polymeter, fills, lift cues, drum solos, lines,
     doubling, line chords, solos, pedal, solo answers, notes, instruments, expression), reading what the song decided
     (`SongDecisions`) and handing on a draft (`SongDraft`), each recorded with its tracks' counts after it, one that
     only edits left out for a test; the fingerprint kept. The counts tell a stage that adds or takes away notes, not
     one that moves them, as the lines' restatement does; a stage's changes in place, should a report need them.
  3. Done: the song checked after every stage, its events within it and in order and its notes playable
     (`SongStagesTest`, the song observed as each stage ends). The rules a finished song keeps stay with their parts
     (every note in its chord's scale in `ScalesTest`, the bass's landings in `MelodyRepetitionTest`, a length given
     in `SongLengthTest`), one file of them moving tests for no gain.
  4. Done: tests as rules or calibrations, written into CLAUDE.md; the floors this round moved, the melody's moves
     and leaps and the solo's motifs, kept as wide bounds that print what they measure. The rest as they move.
  5. Found done: a hook intro copies the chorus's appearance, its rests and texture applied (`GeneratedSection.Appear`),
     so it plays what the chorus plays: of 821 parts in hook intros, 5 play where the chorus does not, 4 of them a
     chorus played as a solo, which the intro quotes the tune of.
  Parked: `SectionGenerator.Generate` split likewise, and a stage report of notes moved in place (see *Parked*).

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
- **Genres that sound like themselves** (P2, a plan in steps). A genre leans the surface (its instruments, scale,
  tempo, rhythm, shapes, effects, form); under it every song is a band playing verses and choruses, a melody that goes
  on, a chord a bar, a fill at every phrase, every part as loud. Hip-hop's preset plays as a lounge track: a beat is a
  loop of two to four bars on one or two chords, the drums leading, a short hook repeated with room for a voice, few
  fills, the drums a little late. Electronic's frame is the house and trance of the late nineties and two-thousands
  (four on the floor, 117 to 130, offbeat hats) and plays as eighties synth-pop: it lacks builds and drops over long
  sections, the pads and bass ducking under the kick, a figure looping for minutes, the drums and bass loudest. Steps:
  1. Done: an idiom report a genre (`GenreIdiomsReportTest`), its targets from its description. What it found, every
     preset alike: 4.3 to 5.2 chords in four bars (Hip-hop's and Ambient's as Jazz's), the drums 6 to 13 velocity steps
     under the melody, 0 to 4% of the sections sharing a progression, 1 to 5% of the melody's bars as the bar before,
     2 to 3.6 fills a minute; the snare on 2 and 4 in 37% of Rock's groove bars (but a phrase's last and where the drums
     rest), 62% in its plainest songs, the drums' own layers moving it in a middling one, and Electronic's kick on
     every beat in 52%.
  1b. Done: a genre's groove held (`GenreChoices.GrooveHold`, `RhythmicUnconventionality.GrooveHold`): every rhythm
     layer's chance of moving its speed and phase leaned the less, up to a sixteenth, a steady beat's. The layers that
     moved the backbeat in a middling song were the groove's moves, of every layer together (all of them off, 58% on 2
     and 4 against 35%; the drum group's alone 36%, the song's and the section's alone 47%), not a section's role drawn
     again nor the density. The snare on 2 and 4: Rock 66% (42 unheld), Hip-hop 66% (46), Funk 64% (47), Blues 58% (46),
     Electronic 62% (49), its kick on every beat 78% (65). Left: Hip-hop's kick on 1 42%, its preset weighting it to
     weaker beats; Reggae's one drop, the snare on 3, which its preset does not ask for.
  2. Done: a loop (`GenreChoices.Loop`, `SectionGenerator.Loop`): a section plays the first section's harmony (its
     scale, home, chords' pace, progression and chords' own scales) where its meter is the first's, its own drawn all
     the same so that its other draws stay; a genre's, the songs never. Hip-hop's sections sharing a progression 96%
     (4%), its chords in four bars 2.3; Electronic's 82%. The first section made first, its harmony the loop's. Not done:
     the figures shared (the bass's, the riff's), should the report or listening ask.
  3. Done: a genre's levels (`GenreChoices.Levels`), a part's level louder or quieter by up to 0.4 of the velocity's
     units (`VelocityLayers.GenreLevel`), the mix's volumes over them: the drums over the melody Hip-hop +14 velocity
     steps (-10), Electronic +7 (-13). A genre's arrangement after: done (`GenreChoices.Rests`).
  4. Done: the drums' fills by genre (`GenreChoices.Fills`), their spans leaning to longer ones or to none as a line's
     weight leans them, by up to six times the odds: fills a minute Hip-hop 1.4 (2.3), Electronic 1.9 (3.3), Ambient
     0.4, Rock 3.9 (3.3), Funk 3.3 (2.9).
  5. Done: a pump (`StrongEffect.Pump`), the pads, the chords, the bass and the rhythm part ducked 60% on every beat
     and swelling back over it as their expression, every part it suits rather than one; a genre's, the songs never;
     Electronic's songs 61%. On the beat, where a four on the floor's kick is (Electronic's in 78% of its bars), rather
     than on the kick itself, which the rendering of a part does not see: on the kick wherever it falls, should a
     pumping genre's kick leave the beat.
  6. Done: builds and drops (`GenreChoices.Drop`, `FormLayers.Drop`): a lift the drums announce, now and then, the
     band growing louder over the phrase before it and out for its last bar (`TimelineEdits.Silence`, the lines that
     walk into the section with it), the drums' fill alone in it and landing on the drop; a genre's, the songs never.
     Lifts dropped: Electronic 64%, Hip-hop 32%, Funk 22%. Not done: the build thickening, a snare roll doubling its
     rate and a riser sweeping up over the phrase (P3), should the drop sound unprepared.
  7. Done: Electronic split: its preset House (its key `electronic` kept, for the links that name it), Techno (a kick on
     every beat 82% of bars, 93% of sections on one progression, 2 chords in 4 bars, 133 bpm, little melody) and
     Synthwave (a backbeat, the kick on every beat 13%, a verse and a chorus, major or minor, 102 bpm, a lead's echo).
     Synthwave's snare on 2 and 4 58% (target 80%), as Rock's, the drums' own layers moving it.
  The melody as a looped hook: done, a melody written as a hook (see *Form and sections*), by a section's role and a
  genre's value.
- **What a genre cannot reach yet** (P2, a plan in steps): most of it a table the songs already draw by `ByConvention`,
  which a genre overrides as it does the scale, a choice, its lookup at the draw, the presets and a report each.
  1. Done: the lines' scales a genre's pool (`GenreChoices.LineScale`), the melody's and the riff's: pentatonic or
     blues 75% of Rock's melody's sections (38% before), Blues 84%, Funk 77%, Hip-hop 68%, Jazz the section's and melodic
     minor. Not done: a value of stepwise or leaping. Leaning the stepwiseness, at any odds, moved the mean move by a
     tenth of a semitone and the leaps not at all: a melody's leaps come of a phrase starting afresh in another register,
     its echoes and its landings, not of its steps. A melody's leaping by genre (P3), a lean of its register's freedom
     and its leap's size, should listening ask. Its range by genre done (`GenreChoices.MelodyRange`, the melody's line's
     range width times up to 1.5 either way): a section's melody spanning Hip-hop 7.4 semitones, Jazz 12.5, the rest 8.6
     to 10.6.
  2. Done: the bass's style, a genre's value of its leading into a chord, through the chance's odds
     (`GenreChoices.Bass`), and a pool of its landings (`GenreChoices.BassLanding`). A half step into a change: Jazz
     36% (30% unleaned), Blues 28% (24%), Reggae 12% (20%), Hip-hop 8% (11%); most of a bass's steps are its line's,
     so the lean moves a few points. Its landings on the root firmer for Electronic, Reggae and Hip-hop; a walking
     bass lands on the root, so Jazz keeps its own. Not done: a root and fifth figure within the bar, and Jazz's walking
     quarters, as its rhythm settings have them. The octave bass done since (`GenreChoices.OctaveBass`, the Articulation
     stage): a section's bass off the beat playing the note before an octave away, up or down as its range allows, but a
     note leading into a chord and a chugging section; songs (0, 0.05, 0.05) by the groove facet; bass notes an octave
     from the one before House 13%, Synthwave 11%, Funk 4% (its chugs muting instead), 1-3% elsewhere.
  2b. Done: a riff in power chords (`GenreChoices.Riff`, `CompositionStateKinds.PowerChord`): a section's riff playing
     every note with its perfect fifth over it whatever the scale and its octave where the range has it (`Realizer`),
     and no twin; a genre's, the songs never: Rock's riff sections 71%, Blues' 29%. 86% of such a riff's notes carry
     their fifth, a section's landing stacking the band's chord the rest.
  2c. Done: palm-muted chugs (`CompositionStateKinds.PalmMute`, the Articulation stage): in a section that chugs, a
     guitar's or a picked bass's note repeating the one before on a weak beat palm-muted, its state, its length kept,
     General MIDI playing it a third as long, a beat's eighth at least, and a tenth softer (`Render`); the riff's, the
     rhythm part's and the bass's, by their instruments' programs; a genre's, the songs never (`GenreChoices.Riff`):
     Rock's guitars' notes 21% muted, Funk's 15%. Ahead: a plugin's output playing it as a keyswitch or a controller, the
     one place in `Render` the seam; the instruments' variants over time articulations too, once an output differs.
  3. Done: the harmonic rhythm a genre's pool (`GenreChoices.HarmonicRhythm`), the energy leaning it still: chords in
     four bars Hip-hop 2.5, Ambient 2.6, Electronic 2.8, Funk 2.9, Folk and Reggae 3.4, Jazz 5.9 (4.3 to 5.2 every
     preset before). Not done: a chord a phrase, one chord over four bars, which the progressions' two chords a phrase
     at least (home and cadence) do not allow: a vamp of one chord is a progression of its own kind, should the loop's
     report ask for it.
  4. Done: the form's devices by genre: a later appearance's chance of a solo and its soloists (`GenreChoices.Solos`,
     `GenreChoices.Soloists`), and pools of the intros and the endings (`GenreChoices.Intro`, `GenreChoices.Ending`), the
     intros given their ends first, alike at every end so that the songs draw as they did. Solo appearances Jazz 64%,
     Blues 30%, Rock 18% (5% others); fades Electronic 72%, Hip-hop 69%, Ambient 59%; a button or a stop Funk 78%, Rock
     and Jazz 59%. Key changes by genre done since (`GenreChoices.KeyChanges`): a last section lifted, a section in a
     key of its own and any pattern's start, a genre's chances; songs changing key Jazz 44%, Rock 34% (28% unnamed), the
     blues, hip-hop, electronic, reggae and ambient none, however wild.
  5. Done as step 4 of genres that sound like themselves: the fills' amount.
  Done (P3): a genre's arrangement (`GenreChoices.Rests`), how often each part rests, leaning its chance by up to six
  times the odds (`Arrangement.GenreRestOdds`): the pad resting in Jazz's sections 94% (69% in Rock's), Ambient's 26%;
  the drums Reggae's 19%, Electronic's 16% (13%). Not done: a part resting by the section's role (a dub's breakdown
  section), with *A genre's sections differing*. Every step a tile in the editor and a page test that it shows; a genre kept before
  keeps its key, its new choice absent.
- **A head with solos** (to review after the form's devices by genre, which with AABA may make it, and then go): a jazz tune's head, choruses of solos over its form, and the head again, which AABA
  (SongForms.Forms) and the solos' sections (Solos) come near already; a form of its own only should listening ask.
- **A genre's levels** (done, step 3 of genres that sound like themselves; its arrangement after, P3): how loud each part plays against the others, values a part each, applied where the mix
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

- **What the genres cannot play yet** (P2, a review of October 2026; `GenreDevicesReportTest` the measure): every
  device is now a genre's choice (`GenreChoices.HarmonyDevices`, `GenreChoices.Articulation` the last), and the beat
  genres name theirs off. What no setting reaches:
  1. Done: a drum pinned to its figure (`GenreChoices.Pins`, `DrumRoles.Pins`): a genre's chance a song pins a role,
     its drums playing their song's rhythm settings in every bar, every layer's moves let go of. Kick on every beat
     House 77% -> 85%, Techno 83% -> 90%; Hip-hop's kick on 1 42% -> 91%, its snare on 2 and 4 66% -> 86%; Synthwave's
     snare 59% -> 89%, Rock's 65% -> 77%. What is left: the bars a drop or an entry clears, and the wilder songs that
     leave a role free.
  2. Long phrases: done in part (`GenreChoices.SectionLength`, `GenreChoices.Texture`): a genre's pool of section
     lengths, thirty-two bars among them, which the songs never play, and of textures, a section building part by part;
     House's and Techno's sections 16 bars or more 93%, 32 bars 28%, over 8-bar patterns, 43% of their appearances
     building or thinning (9% of the songs'), and a drop's build rolling its snare over the two bars before its
     silence, 8ths then 16ths, louder as it goes (every one of 28 builds). Left: a riser, a noise or a pad sweeping up.
  3. Done: an arpeggiator, the chords' and the rhythm part's broken chords (`Arpeggios`) a genre's pool
     (`GenreChoices.Arpeggio`): Synthwave's chord parts arpeggiated 52% (16% the songs'), Ambient's 34%, House's 22%.
  4. Done: a gliding bass (`GenreChoices.Glide`, `Expression.GlideChance`), a genre's chance its bass slides into
     half its changes of pitch from the note before, across a gap of a beat and into a 16th: the bass's changes slid
     into Hip-hop 13%, Techno 9% (the songs' 0). Not done: an acid line's accent, which General MIDI has no 303 for.
  5. Done in part: hi-hat rolls (`SongStages.HatRolls`, `GenreChoices.HatRolls`), a bar's last beat or its half on the
     drums keeping time rolled in 32nds or 16th triplets, louder as it goes, a genre's: 204 rolls in 16 Hip-hop songs,
     none in the songs. Not done: a snare's drag and its ghost notes, which want the backbeat's own figure, the pinned
     backbeat keeping its hits clean.
  6. The one drop (the kick and snare on 3 alone, beat 1 empty), Reggae's, and its skank on the offbeats only.
  7. A clave the band keeps to, Latin's: the parts' rhythms aligned to the 3-2 or 2-3, and the piano's montuno.
  8. A rap's place: a hook sung and the verses left to a voice, the melody resting or a riff carrying them.
  9. A melody written as a hook in every section, not only its repeated bars (House's and Hip-hop's melody bars
     repeated 13% and 18%, a target of 50%).
  10. A chord stab: a chord struck short on the offbeat, House's piano and Techno's dub chord, with an echo.
  11. Gated reverb on the snare and a big tom fill, Synthwave's, which General MIDI cannot sound but a velocity and
     length could suggest.

## Parked

Ideas with a reason, not planned: none has shown a need. Taken up only when listening or another item asks for one.

- **Ping-pong echo** (P4): an echo alternating sides, from the part's pan to its mirror, where a part's echo now
  repeats in place.
- **Modulation** (blocked until a soundfont maps it): CC1, which none of the soundfonts offered maps to tone. The rule to
  keep: modulation and a vibrato never at once, modulation and bends together.
- **`SectionGenerator.Generate` split** into stages as the song's are: its draws are apart by their streams already,
  and no bug has come of its order.
- **A stage report of notes moved in place:** the stages' counts tell notes added and taken away; a diff of every
  timeline after every stage, should a report need what a stage moves.
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
