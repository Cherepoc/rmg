# Roadmap

What is to be done, what listening is to settle, the ideas parked and the decisions that keep what is built so. What
is built is described where it is built, in the code, and its numbers are in the commits that made it; an entry here
is removed once it is built.

## Priorities

**Next** (P1): listening to what is deployed, 0.6 and on, and tuning by what it says (see *Listening*), and the
ratings by conventionality once enough are in, alongside, continuously and never as a gate; listening to the
presets as the genres' review retuned them, and Pop and Metal, among it (see *Genres*).

**Later** (P2): the ratings report (see *Listening data*) once the dashboard shows songs rated enough, and listening
to the presets and to what October 2026 gained (genres that sound like themselves, the pocket and the strum, builds and
drops, turnarounds and passing chords), alongside.

**When the need shows** (P3), each by what listening says: the items left in the sections below, each marked P3.

**Put off** until its uses ask for it: a tag (see *Form and sections*).

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
- **Borrowed and applied chords:** 7% to 12% of the chords but the cadence's leave the section's scale, two or three
  borrowed for every applied (`ChordScalesReportTest`), the chances a guess: does a melody holding the section's note
  over a borrowed chord clash, and does the next chord's shape spoil an applied dominant's resolution?
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
  as the form's are, should it matter. Long songs, ten minutes, are free forms seven times in ten, as no form reaches
  them: heard as rambling? Solos filling a long length rather than more parts, if so.
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
  section's energy over the song's in its range's place (`MelodyLayers.EnergyRegister`), the chorus 2.6 semitones over
  the verse, higher in 61% of the songs.
- **The band's devices, the rest** (P3, each an edit after assembly among the stages, as stop time and pushes are):
  stop time's hits added where a part rests on the downbeat, and a fill in a stop-time phrase's last bar; the drums'
  push, a crash and kick on the and of four; a backing figure that recurs through the song. Call and response between
  parts (postponed October 2026): an edit after the notes keeping the counter-melody to the melody's rests kept 12% to
  15% of its notes, the melody leaving no room; the counter-melody's rhythm placed against the melody's gaps when it is
  drawn, as the solo part's answers are (`SoloAnswers`), is the way, should listening ask.
- **Harmony's devices, the rest** (P3): a ii–V into a chord but the home, which a chord's own scale makes part of; the
  line cliché rising (the fifth, its sharp, the sixth) beside its falling one (`SongStages.InnerLines`), should
  listening ask.
- **The hook at the end** (P4, parked): a song opens on its chorus's first phrase now and then (`IntroKind.Hook`); an
  outro quoting it too, should endings sound unrelated to the song, though the last section is most often the chorus
  already and a fade replays it.
- **A section's parts chosen as its drum kit is** (`DrumKitGenerator.SelectKit`), leading and colouring roles, where
  now each part rests on its own (`Arrangement`).
- **The drums' changes by an appearance's energy** (their colour, a drum's stroke), which are the section's as it was
  made.
- **The tempo leaning by the energy** (P4, parked): a section's tempo nudged a percent or two by its energy through its
  tempo layer by `Tilt`, never in a plain song, which keeps to the click; no drift of its own.
- **A key change's common tone** (P4, parked): a line's note held over the change into the new key, which the lines'
  placement does not make; the pad perhaps carrying it.

### Fills and lines

- **Filling the melody's gaps before a chord change within a phrase,** should they sound empty now that the solo part
  answers.
- **The lift beyond the cues:** the chords' rhythm doubling, a part of the next section coming in early, should the
  crescendo and the drums' cues not lift.
- **The fills' loudness** (P4, parked): a run's swell, its accents and a landing's hit are constants, where the groove's
  loudness is layers; a fill's velocity layer would make them cumulative with the section's.

### Instruments and expression

- **The playing following the articulation** (P3; palm-muted chugs done): octaves popped in slap, short notes muted,
  without which an articulation is only another sound.
- **Plain doubling,** a part doubling another in unison, now that the parts play by their pocket (`SongStages.Timing`).
- **A motif stretched,** its rhythm doubled, beside moved and inverted (`Soloist.MotifInversion`), should listening ask.
- **A solo's runs in fives and sevens,** a fusion player's grouping, which the feels give a whole part but perhaps not
  a run; if not, a case of segments in any pattern (see *Parked*), not a mechanism of its own.
- **Legato slides** (P3, a case of legato): a slide strikes its note anew, where a player's note before goes on
  sounding into the next; holding the note before into the slide and leaving the next unstruck edits two notes at
  render. Built if the struck slide sounds like a stutter.
- **Long fall-offs** (P4): a fall of an octave into nothing at a note's end, a slide's kind of its own, if missed.

### Groove

- **A drum's pocket by its section's role** (P3, if a section's change of role is heard): the timing takes a drum's
  offset by its role in the song's groove (`SongStages.OffsetOf`), not a section's, which may move a ride to the time.
- **A strum's thinner up stroke,** its lowest strings left out, if the up strokes sound as full as the down.

### Scales and chords

- **Free chords, the rest** (P3): a chord moved by semitones (`StateKinds.ChordShift`), planing (`Planing`) and a
  chord's own scale on its root (`ChordScaleKind.Own`) are built. Left: planing choosing the next chord by the voice
  leader's least move among free shapes, should it sound mechanical; the diminished scale, of eight notes, which the
  seven-note scales cannot hold. Its uses past these: a fusion genre, a long-lined guitar player's, its harmony and
  lines as a genre's data (free chords, melodic minor, octatonic and whole-tone line scales, leaping, stretched
  voicings, swells, odd meters); not its tone, which General MIDI has not, its long legato soloing, which the solo's
  gestures (licks, runs, held notes) are not built for, its drumming and its melodic bass, gaps of their own.
- **A section holding its spacing** rather than the voice leader drawing it anew a chord, should a spread chord's
  (`ChordVoicing`, "Spread") neighbours sound close.
- **Approach chords, the rest** (P3): turnarounds, passing chords, a chord solo's planed approaches, chromatic
  passing chords from above and a later appearance reharmonised are built. A plain song is never reharmonised, by any
  of them: the songs' chance 0 at the plain end, and a genre's offering no plain end (`GenreOption.HasPlainEnd`). Left:
  the passing diminished between roots a whole step apart, and chromatic approaches from below, a chromatic root
  wanting the whole chord moved a semitone with the lines over it keeping the target's scale; a chord solo's approaches
  on the strong beats at the wild end, and the dominant as its approach; the tritone substitute for V7 in a
  reharmonised appearance. Today a chord solo stacks the band's chord or planes its shape, never another chord.
- **A melody in block harmony** (P4, parked): the counter-melody or the pad voicing its long notes, approaches on its
  passing ones, as a horn section's soli.
- **Scales of other sizes** (P4, parked: the line scales give their sounds where they are heard): whole scales of more
  or fewer than seven notes, pentatonic, hexatonic (whole-tone, the blues' six) or octatonic, for a section's harmony as
  well as its lines, at the wild end of the scale facet alone. The progressions move by scale steps and their rules
  count a fifth as three steps, the chord shapes' heights sit between the qualities a seven-note scale gives, and the
  scales measure their brightness and their distance against others of as many notes (`Scales`): a fifth as the step
  nearest seven semitones, a chord's height snapped as now (`SnapChordToScale` takes any length), and a scale's
  distance only among its own size. Steps: the engine's assumptions of seven found by a trial forcing every wild
  section into a pentatonic, as the 12-bar's trial found the phrases'; then the scales drawn.

### Rhythm engine

- **Long cycles** (P3, the genres' review's 19; put off in October 2026 as an engine's change): a bar pattern is one bar long, so a slower cycle, such as the kick's slowed
  to two bars, plays its first half and starts again at every bar line. Patterns as long as their cycle would let slow
  figures run whole, a crash every two bars, a kick figure answered in the second bar, a chord of two bars, a clave's
  3-2. Put off as an engine's change: the bar is the unit of the scheme's letters, the state's layers, the melody's
  question and answer, the drums' presence, the fills' lines and the feels. A crash every two bars, should it be
  missed, an edit after assembly as the lift's cues are.
- **Phrases of other than four bars** (a 3-bar phrase, a 6-bar section), which the line's question and answer and the
  fills assume, once the sections of several phrases sound right.

### Architecture

- **The ratings in the history** (P4): the list of songs so far does not show which were liked or disliked.
- **A mix written by the page** (P4): a change of the mix asks the server for the file again only for the download;
  writing the mix into the file in the page would leave the server out of it, at the cost of MIDI writing in
  JavaScript.
- **The server's figures read in full** (P4): the dashboard reads every song made in its window, nothing at hundreds a
  day and slow at the 200,000 a day the table keeps at most; then the spreads would be worked out a day at a time.
- **The drum groups' pulses fading by their sounds** (P4): every group fades as fast as the parts' meters, a cymbal's
  wash as a click; a fade of each group's own would follow its sound, for little.
- **A flaky page test** (P3): `GenreEditorTest.ALength_IsShownAndGivenInMinutesAndSeconds_ATempoInBeatsAMinute`
  failed 3 of 11 full runs of the page tests in October 2026, never with detailed output nor alone, its message lost;
  a race in the tune's values shown, to find should it keep failing.
- **The page's tests of sound** (P4): the page tests drive asking for songs, not hearing them; playing, seeking, the
  MP3 export and the lock screen's controls are tried by hand, as are Safari and phones.
- **A report of thousands of songs** would make, measure and let go of its seeds past the shared corpus rather than
  cache them; a corpus song holds about 5 MB.

## Genres

A genre is the page's data (`Genre`, `GenreChoices`, `Client/genres.json`), every option three amounts, at the plain
end, the middle and the wild end; what each choice does is in the code and the numbers of each in its commit. What is
left:

- **The genres' review, the rest** (October 2026; its P1 and P2 items done, every device a genre's and every preset
  naming every device, the presets retuned, Pop and Metal added), each measured by the preset reports before and after
  (`GenrePresetsTest`, `GenreIdiomsReportTest`, `GenreDevicesReportTest`).

  **P3**, by what the measures and listening say:
  18. **Any genre** (a plan in steps): a song asked for with no genre drawing a preset, or the songs' own as one of
     its outcomes, evenly, so that the plain band stays among them; the seed alone making the song again, the CLI's
     and the API's too, and the ratings telling which genres are liked. Steps:
     1. The presets the server's data rather than the page's alone (`Client/genres.json`), served to the page, which the
        tests and the CLI read too (`--genre`).
     2. The server drawing one when asked for any, from a stream of its own, so that the rest of the song stays as the
        seed made it; the preset drawn in the response, the song's link and every analytics event about it, as its
        version and settings are.
     3. The page offering any genre beside the presets.
     4. The default (the songs' own, as now, or any genre) decided by the ratings by genre once a version has enough,
        and not before the retuned presets, Pop and Metal most, are heard.
     Not weighed by the presets nor leaned by conventionality: each preset spans the plainest songs to the wildest.
  19. **Two-bar patterns** (*Long cycles*, raised from put off by the idioms waiting on them: the clave, the montuno,
     a kick answered in its second bar), and snare ghost notes and drags (*What the genres cannot play yet*).
  20. **More genres,** should Pop and Metal sound right: Soul or Gospel, drum and bass, disco, trap (Hip-hop's glide
     and hat rolls).
  21. **The hook and the phrase scheme as one:** a hooked section a scheme sung literally, should a trial keep the
     report's repeats (50% of a hooked section's notes at their first bar's places); otherwise kept apart, with the
     reason.
  22. **Harmony for Jazz and Soul:** the tritone substitute, the passing diminished and a ii–V elsewhere (*Approach
     chords*, *Harmony's devices*); call and response (*The band's devices*); a part resting by its section's role.

  **P4:** the choices grouped into families (bass, harmony, form, drum devices), one choice of several chances rather
  than fifteen of one, which would read better in the editor and break every genre link kept; should the editor grow
  hard to use.
- **The arpeggiator by genre** (P2, data only; "chords parts arpeggiated" in `GenreDevicesReportTest`): eight presets
  play the songs' own pool, named for them (18-19% of their chord parts, and at the wild end every pattern as heavy as
  none, nine in ten broken). More: Pop 25-30% (up, up and down, Alberti), Rock 15-20% (up, up and down, a picked
  verse's, modest under a driven guitar), Hip-hop 10-15% (up, up and down); Blues its boogie, root and fifth 10-20% and
  no other; Metal rare at the middle and its sweep, up two octaves, at the wild end. Less: Jazz 3-5%, no Alberti (its
  comping block and shell chords), Funk and Reggae none (their chords the rhythm, stabbed). Every genre's wild end set
  as its own experiment rather than every pattern at 100.
- **Instruments and effects** (P2, a plan in steps, October 2026): 74 of the 128 programs play, the rest never; and no
  genre sets a part's reverb.
  1. A genre's reverb: a value a part (`space`, as `levels` are), over the role's send (`Expression.Reverb`) and its
     spread; Ambient wet, Funk, Hip-hop and Metal's chugs dry.
  2. An instrument's traits as data, one table (its family, sustaining or decaying, strumming, rolling, bending, its
     vibrato, the effects that suit it), read where program ranges decide them now (`Expression.Bends`, `Choruses`,
     `ChorusesClassically`, the strong effects, `VibratesWide`, the strum, the roll and the chugs in `SongStages`,
     `Soloist.BendSuit`); a refactor, the fingerprint kept. Before any instrument is added, which would otherwise take
     the traits of the numbers around it (a piccolo bending, synth brass swept).
  3. The unused melodic instruments in the roles' pools and the presets': honky-tonk piano (Blues, Folk), tango
     accordion (Latin), reed organ and dulcimer (Folk), synth brass (Synthwave, Pop, Funk), the orchestra hit as stabs
     (House, Hip-hop, Synthwave), pizzicato and tremolo strings as instruments (Pop, Ambient), steel drums (Latin,
     Reggae, Pop), music box and tubular bells (Ambient), synth voice and the calliope, chiff, voice and bass-and-lead
     leads (Synthwave, House, Techno), piccolo, recorder and whistle (Folk), English horn (Ambient); and the synth
     effects as pads (atmosphere, soundtrack, crystal, brightness, echoes; rain for Ambient; goblins and sci-fi at the
     wild end). Their loudness heard in both soundfonts served.
  4. The reverse cymbal as the riser: a note ending on a drop, and on a louder section's first beat as a lift cue, an
     edit after assembly on a free channel; the electronic genres' and Pop's most. (The drop's riser below.)
  5. The world instruments (koto, shamisen, shakuhachi, shanai, bagpipe) at the genres' wild ends alone, Folk's bagpipe
     too; a lead of their own would want a genre of its own.
  Not planned: the percussive family's woodblock, agogo, melodic tom and tinkle bell, the kit's own sounds again; the
  synth drum, whose 80s toms are fills on the drums' channel; the taiko, for a cinematic genre none is. The sound
  effects, and the sound-effects kit, see *Parked*.
- **What the genres cannot play yet** (P3, each by what listening to the presets says; `GenreDevicesReportTest`,
  `GenreIdiomsReportTest` the measures):
  - The loop's figures shared, the bass's and the riff's, beside its harmony (`GenreChoices.Loop`).
  - A drop's build sweeping up, a riser, beside its snare roll: the reverse cymbal (*Instruments and effects*, 4).
  - The pump on the kick wherever it falls, should a pumping genre's kick leave the beat; it ducks on the beat now.
  - The pinned drums (`GenreChoices.Pins`) in the bars a drop or an entry clears, and the wilder songs that leave a
    role free.
  - A snare's drag and its ghost notes, which want the backbeat's own figure, the pinned backbeat keeping its hits
    clean.
  - Latin's clave, of two bars (see *Long cycles*); the piano's montuno, a broken chord off the beat; a second time
    keeper playing straight under the tresillo.
  - A hook of one or two bars sung through a whole section, its phrase ends let go of; a hooked melody's bars repeat
    about half at most now, a phrase's last bar ending on its held note.
  - Metal's double kick, the kick in 16ths: the ground's rhythm settings at their most play 4.5 hits a bar, a value
    moving a setting by steps from where the role rests, and a genre giving the kick the time role leans the songs'
    own weight of it there (1) by its own, too little to take. A kick keeping time by a genre's role, or a value set
    outright (see *The rhythm's values against the part's own*).
  - A fill's drums leaning to the toms by genre, which the drum groups' state draws, not a genre's choice.
  - A melody's leaping by genre, a lean of its register's freedom and its leap's size: leaning its stepwiseness moved
    the mean move by a tenth of a semitone and the leaps not at all, a melody's leaps coming of a phrase starting
    afresh in another register, its echoes and its landings.
  - The bass's root and fifth figure within the bar, and Jazz's walking quarters, as its rhythm settings have them.
  - A vamp of one chord over four bars, a progression of its own kind, which the progressions' two chords a phrase
    (home and cadence) do not allow, should the loop's report ask for it.
  - A part resting by the section's role (a dub's breakdown), with *A genre's sections differing*.
  - A genre's lean of the phrases' place rules, should it press them, and the songs' own sections of eight bars now
    and then.
  - Articulations played by a plugin's output as a keyswitch or a controller, the one place in `Render` the seam, once
    an output differs from General MIDI.
  - Beyond General MIDI, parked: the gated reverb (one reverb send for the whole drum channel, no gate) and an acid
    line's accent (no 303).
- **A head with solos** (to review, and then go): a jazz tune's head, choruses of solos over its form, and the head
  again, which AABA (`SongForms.Forms`) and a genre's solos (`GenreChoices.Solos`) come near already; a form of its own
  only should listening ask.
- **A genre's sections differing** (P3, perhaps never): a value given a section role, such as a solo swung harder
  or a chorus louder, the genre's for the song otherwise; the generator asking the genre with the section's role,
  every choice's tile a role's override beside it. The dynamics' contrast and the phrases' AAB give most of it.
- **Listening to the presets** (P2): each against its sentence (`GenrePresetsReportTest`). Pop and Metal set no groove
  hold, their parts not pinned drifting as the songs' do (Pop's snare on 2 and 4 80%): Rock's (80) for them, should
  their beat sound loose. Jazz's ride, pinned, plays the
  songs' own time figure, 7% of its bars a tresillo's: a rhythm of its own for the time (a ride's spang-a-lang), should it
  not swing. Reggae's one drop is a kick
  half a bar off its cycle, which the report cannot tell from beat 1, and Latin's tresillo is the time keeper's grouping
  alone; the rhythm values mean what they make together, and a preset's are tuned by where its notes fall. Blues' twelve
  bars: V at bar 9 in 43% of its twelve-bar sections, home at bar 4 48%, IV at 5 63%, home at 8 61%, the middling and
  wild songs keeping the place rules less by the progression's strictness.
- **Listening to the genres' wild ends:** the presets' wild ends keep their idioms most and stray by what each names
  experimental (`EveryPreset_ChoicesByEnd`): the wildest Rock 34% in 4/4, its chords 7% quartal or tritone stacks; the
  wildest Jazz 56% swung, 50% sevenths, about a fifth So What, quartal, altered or eleventh. A home and a cadence lean
  the genre's shapes by their own over the songs', and where the songs never play a shape at an end the genre's weight
  alone (`ByConvention.Lean`), so that at the wild end a home is a genre's plain shape or one of its strangest, never a
  level-2 one (a ninth, a thirteenth): a wild song's homes plainer than its other chords, which the experimental
  shapes' amounts may be raised against, should listening ask.
- **How the wild end swings:** a swing is as likely in the wildest songs as in a middling one, and the principle has
  the wild end choose how it is played instead, such as a heavier swing or one past the triplet's; the amount is drawn
  alike at every conventionality now (`Groove.Generate`).
- **The rhythm's values against the part's own:** a value moves a setting by steps from where the part rests, so that
  one part's "faster" is another's "as fast as can be"; a value set outright, where it lands, would read the same for
  every part, should listening ask for it.
- **A link replacing the settings kept** (a decision): opening a shared song makes its settings this browser's own,
  so that the next song goes on in them; keeping one's own and offering a way back to them is the other way, should
  listening to shared songs ask for it.
- **Instruments outside a role's pool,** with their leading drawn from their family.

## Parked

Ideas with a reason, not planned: none has shown a need. Taken up only when listening or another item asks for one.

- **Sound effects** (P4): General MIDI's 120-127 and the drums' sound-effects kit sound unlike in each soundfont and at
  each pitch, and read as a mistake rather than as meant; a gunshot, a telephone or a helicopter never, applause a
  gimmick, breath and fret noise realism. One experiment, should listening ask: a bed of the sea, birds or rain held
  under an Ambient intro or outro, a part of one held note and no harmony.
- **Ping-pong echo** (P4): an echo alternating sides, from the part's pan to its mirror, where a part's echo now
  repeats in place.
- **Modulation** (blocked until a soundfont maps it): CC1, which none of the soundfonts offered maps to tone. The rule to
  keep: modulation and a vibrato never at once, modulation and bends together.
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
- **An applied dominant keeps the shape drawn for it,** which its Mixolydian makes a major triad, a dominant seventh
  or a suspended dominant; forcing a seventh would override the shape's draw for the stranger shapes alone.
- **Groove hold stays beside pins** (measured October 2026): a pin decides a part's figure, groove hold leans every
  part steadier, which the principles ask for over pinning more. With it taken out of every preset the pinned measures
  moved 2 points or less, but the unpinned backbeat fell: House's snare on 2 and 4 0.67 -> 0.45, Techno's 0.61 -> 0.47,
  Funk's 0.77 -> 0.68, Blues' 0.71 -> 0.63, where a snare pinned 70% of the time is held by it the rest.
- **Seeds are not stable across versions;** the version labels what was heard (see *Listening data*).
