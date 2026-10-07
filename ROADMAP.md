# Roadmap

What is to be done, what listening is to settle, the ideas parked and the decisions that keep what is built so. What
is built is described where it is built, in the code, and its numbers are in the commits that made it; an entry here
is removed once it is built.

## Priorities

**In order** (P1, the plan of 9 October 2026, each step through the implementation loop):

1. Done: the echo as the synthesizer's delay rather than notes; heard on the test instance (`?listen=echo`).
2. Done: the strong effects stacked, the first as before, the others beside it by their shares, the volume multiplied.
3. Done: the gate holding whole chords only, an arpeggio gated as it plays, Trance's arpeggios in 16ths.
4. Done: the part alone a genre's.
5. Done: metal's arpeggios, a pedal point, a part's colour, a clean arpeggio staying clean.
6. Done: Djent's synth over the guitars, a line's strong beats on a tension, a genre's by part.
7. Done: the brightness measured in both soundfonts, the filter's floor raised to 40.
8. Done, with 9: one brightness a part, its tone and a filter's closing added, never below the filter's floor.
9. Done: a part's darker tone, a gentle effect, darker in the quiet sections; a swell brightening left (P3).
10. Done: a tape's wow, the parts drifting together, some detuned, IDM's and a lo-fi genre's.
11. Done, with 12: the whole mix filtered, a song opening up, a section dipping before the next, a song closing.
12. Done with 11.
13. Done: Trance's rolling bass, its build rolling to 32nds, its ♭VI, ♭III and ♭VII, its mix filtered.
14. Done: Djent's mix dipping before its heavy sections, its riff stuttering under the gate.
15. Waiting on listening: the rest of rock's and metal's, long cycles, more figures, rolls in 32nds, a riff's drone.

Done in the plan of 8 October 2026: the genres sorted by name; rock and metal measured and their presets tuned; a
section left to drums that only mark the form; the listening tool, *Listening* cut to its questions; a progression's
degrees leaned by a genre; the render reading the song's map, stop time decided once; the song's sound decided before
its notes; figures as a genre's data, the kick with the riff, the double kick, the gallop and the clave.

**Next** (P1, alongside): listening to what is deployed and tuning by what it says (see *Listening*, the page's
listening mode for the questions it asks), done for a version when every question is answered or let go of; and the
ratings by conventionality once enough are in, continuously and never as a gate.

**Later** (P2): the ratings report (see *Listening data*) once the dashboard shows songs rated enough.

**When the need shows** (P3), each by what listening says: the items left in the sections below, each marked P3, any
genre first (see *Genres*).

**Put off** until its uses ask for it: a tag (see *Form and sections*).

**Parked** (P4): see *Parked*, kept for their reasons, not planned; and, parked where they stand: scales of other
sizes, a melody in block harmony, a hook at the end, the tempo leaning by the energy, a key change's common tone, the
fills' loudness as layers, and a head with solos (which the form's devices by genre with AABA likely make).

## Listening

What listening is to settle: each a question, its measure, what it decides, and where the page's listening mode asks
it, `?listen=` its id on the test instance (`ListeningQuestionsTest` writes them, the dashboard tables the verdicts).
Done for a version when every question is answered or let go of.

### The song and its sections

- **How plain by default:** drawn songs centred at 0.5 (`Unconventionality.BaseCentre`), 5% plain in every convention
  (`ConventionalityLevelsReportTest`): too adventurous for songs of no genre? Decides a centre of 0.375, or not.
- **The forms:** verses and choruses heard as such, the chorus lifting; sections of 80 bars at the median; an intro or
  outro of its own material (a third and a seventh of middling songs) the same song; a last chorus twice (17%). Decides
  the forms' weights and the last repeat's chance.
- **The sections' energy:** quieter sections heard as sections, louder ones lifting; a last appearance 0.10 louder
  than the first; fills into a quieter section 54% of the time. Decides the energy's contrast and the fills' landings.
- **Rests, entries and textures:** breakdowns and melody-less sections as arrangement or parts gone missing; parts held
  back at the start (15-18%); one part alone or parts dropping out, leaned to the quieter sections (`TextureTest`).
  Decides the textures' weights and their lean.
- **The lift** (`?listen=lift-cues`): a crescendo of 8 of 127 into a louder section, and the drums' cues, 2.6 a song.
  Decides whether the cues stay, and how often.
- **Key changes:** two a song in the fourth fifth; prepared ones, pivots, pedals, sequences heard. Decides their
  chances and devices.
- **Tempo:** a section's own tempo eased in or taken at once; 6/8 counted in quarters; 13/16 too busy? Decides the
  easing and the odd meters' weights.
- **Half and double time:** double time's 22 drum notes a bar a lift or a scramble? Decides its weight.
- **The fade-outs:** a ritardando into the fade, the drums fading first, a tag after it? Decides whether to build one.
- **The settings panel:** a song made again with changed settings sounding moved rather than jumped; the pad in 80% of
  songs, the counter-melody in 60%, the drums in 95%. Decides the parts' chances.

### Harmony and lines

- **The section modes:** a mode change prepared by its cadence; the parallel and harmonic minor heard (seeds 196, 10,
  36, 171). Decides the modes' weights.
- **Harmonic rhythm:** a chord every half bar busy at 175 bpm, one every two bars static at 90? Decides leaning the
  span by its length in seconds.
- **Phrases:** question and answer, varied repeats (A′ at 0.25, `PhraseSchemes.VariedRepeatChance`), the melody's
  leaps at a phrase's start (a third), the sequences rising with the phrase. Decides their chances.
- **The gaps before chord changes:** half the changes with no melody in their last beat, the solo part answering now
  (`?listen=solo-answers`). Decides filling them.
- **The walks into a section:** the melody at 15% of changes, the bass at half of the runs. Decides their chances.
- **Double stops and the riff's chords:** the melody's on a chord instrument's downbeats in a quarter of sections.
  Decides their chance.
- **Borrowed and applied chords:** 7-12% of chords leaving the scale (`ChordScalesReportTest`); a held melody note
  clashing, an applied dominant's resolution spoiled? Decides their chances.
- **The bass's pickup** always on the beat: square? Decides an 8th before the bar line, leaned by convention.
- **Octaves and twins** (`?listen=doubling`): a line in octaves in 10-20% of appearances, the riff's twin in 15% of riff
  songs, twin leads a third apart. Decides their chances and intervals.

### Solos

- **Runs and phrases:** a player's or a scale exercise; cells of five or seven; the intensity's arc; pauses in 17-20%
  of segments; 2.1 notes a beat at the middle. Decides the runs' cells and the solo's density.
- **Expression:** 59% of a middling solo's longer notes with a vibrato, 16% bent, 3% wide; 85% of guitar and synth
  solos bending. Decides the soloists' leans.
- **Loudness:** a soloist 0.06 of velocity over the band's loudest part, a flute too? Decides the margin.
- **Chords in solos:** 41% of a chord soloist's held notes stacked, planing 17% at the middle and 59% wild. Decides
  their rates.
- **Drum solos:** stickings round the kit; a crash with the kick in 23% of segments. Decides the drum solo's gestures.

### Drums and groove

- **The drums:** the percussion songs; the wild end's kick or snare in a role not theirs; bound drums at 0.15.
  Decides the roles' wild weights and the binding chance.
- **The fills:** wild songs' 1.8 times the tuplets; three runs in ten at quarters or slower. Decides aiming a run's
  finest rank at the tempo's finest.
- **Repeated cycles** replaying their accents: mechanical? Decides a fresh loudness draw over them.
- **Swing and panning:** a triplet's swing at 16ths; wild tuplets swung; the chords' pan. Decides the swing's amounts
  and the pans.
- **Drum pieces given:** both snares doubling the backbeat; a piece put in by its roles alone. Decides giving the
  snare group one snare.
- **Every drum heard** (`DrumCoverage`): a drum landing once, placed or tacked on? Decides its landing.
- **The drums' marks** (`?listen=drum-marks`): a strike every three bars and a swell into a section. Decides the marks'
  rate and the swell's length.
- **The ornaments** (`?listen=ornaments`): one in eight to ten long notes, 2.5% of Folk's and Medieval's lines; a trill
  up to 1.5 s. Decides their rate and the trill's length.
- **The polymeters:** the snare on every cell's start; the chords joining a cycle. Decides who joins a cycle.
- **The pocket** (`?listen=pocket`): the backbeat a little late, the bass a touch ahead, the lead laid back. Decides
  the offsets.
- **The figures** (`?listen=kick-lock`, `double-kick`, `gallop`, `clave`): Metal's sections 27% with the riff, 14% a
  double kick, 15% galloping, Nu metal's 42% with the riff (`RockAndMetalReportTest`), Latin's claves on the clave.
  Decides their weights and leans.
- **Pushes and stop time** (`?listen=pushes`, `?listen=stop-time`): the band's changes an eighth early; the band
  hitting once a bar. Decides their chances by genre.

### Sound

- **The velocity:** the bass's and the chords' evenness; bar layers adding 3 to 5. Decides the layers' spread.
- **The echo as the synthesizer's delay** (`?listen=echo`): the Roland GS delay's tone in the player, its feedback
  (100, about half), its darkness (2 of 7), the send (100), and a dub snare's quiet strokes echoing with its backbeat,
  the delay's send being a drum's, not a stroke's. Decides those settings, or the echo's notes back.
- **Metal's arpeggios** (`?listen=metal-arpeggio`, `?listen=djent-arpeggio`, `MetalArpeggiosReportTest`): a third
  of Metal's chord parts' sections broken, the pedal point the commonest, 1.7 notes a beat, its colours dissonant 17%;
  Djent's 79%. Ringing over the riff or muddying it; the rhythm part resting less where the heavy guitars rest, should
  the arpeggio be lost under them. Decides the colours' and the patterns' weights.
- **The strong effects stacked** (`ExpressionTest.ReportStacked`): Trance 56% of its songs with two or more, a pump
  and a gate or an opening; the drawn songs 5%. A wall of effects, or a produced track? Decides the shares beside the
  first, and a genre's weights.
- **The effects:** 1.9 gentle kinds a song at the middle, a strong one in 25% (`ExpressionTest.Report`). Decides the
  song's weights.
- **Bends, vibratos and slides:** slides 44% of the pitch shapes. Decides their rates.
- **The instruments added in October 2026** (about 4% of parts): any too loud or strange in either soundfont? Decides
  their weights and traits.
- **The riser into a drop:** a soundfont's sample shorter or longer than the bar. Decides starting it later.
- **The trance gate** (`?listen=gate`): a held chord chopped in 16ths, Trance 17% of its songs (`GateTest.Report`);
  a pad's slow attack, the chop clicking? Decides its weight and the instruments it chops.
- **The filter:** wobble, squelch and opening, Techno 11%, Trance 28%, Dnb 26% (`FilterTest.Report`); a General MIDI
  synth bass wobbling thin? Decides its depth and resonance.
- **Double-tracking:** Metal 2.8 parts a song at the sides (`DoubleTrackingTest.Report`): a wall, or mud? Decides the
  delay and which parts stay single.
- **The dub echo, the insertion effect and the rooms** (`DubEchoTest`, `InsertionTest.Report`): echoes as notes thin,
  the wah's and phaser's settings, the rooms wet enough? Decides the GS delay and the effects' settings.
- **Louder forms** (`?listen=louder-forms`): a clean guitar turned driven when loud. Decides its chance by genre.
- **Rock's rhythm guitar** clean (53%) or muted (20%) beside driven chords and riff: a contrast, or wanted driven?
  Decides the exclusion's lean.
- **The bass under E1** (9% of its notes, `RegisterReportTest`). Decides the bass's range.
- **Instrument switches** within the family, 58% of a middling song's. Decides their families.

### Genres

- **The presets against their sentences** (`GenrePresetsReportTest`, `GenreDevicesReportTest`,
  `GenreIdiomsReportTest`): Pop's and Metal's beat without groove hold; Jazz's ride's time figure; Latin's and
  Metal's solos (66%, 91%); Blues' twelve bars loose enough. Decides each preset's choices.
- **The presets made in October 2026:** Orchestral's and Pop's crescendos; Funk's, Techno's, House's and Djent's vamps
  (30-66% of sections); Djent 70% polymetric; Nu metal's loud sections driven (97%); Medieval's and Dungeon synth's
  drums marking the form. Decides each preset's choices.
- **The dark presets' mood** (`MoodReportTest`): still happy? Decides their degrees leaned (`rootDegree`).
- **Rock's and Metal's degrees** (`RockAndMetalReportTest`): Rock's ♭VII–I in 45% of sections, Metal's ♭VI in 19% of its
  chords; a rock song's I–♭VII–IV loop, which the progression's home, away, preparation and cadence come near only
  through the cadence? Decides a loop's template of its own.
- **Rock and metal against their bands** (`RockAndMetalReportTest`): the organ's solos, the shuffle, Metal's clean
  quiet sections. Decides the presets' weights.
- **The wild ends** (`EveryPreset_ChoicesByEnd`): the wildest Rock 34% in 4/4, the wildest Jazz 56% swung; a wild
  song's homes plainer than its chords. Decides raising the experimental shapes' amounts.
- **How the wild end swings:** as often as a middling song (`Groove.Generate`). Decides a heavier swing at the wild end.

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

- **A sparse song's silent start** (P2): an Ambient or a Dungeon song now and then silent for its first 16 beats or
  more, its parts resting through their first phrase's start and its drums out of its first section (Ambient seed 37,
  Dungeon seed 54 of the corpus); a song's first part to sound coming in on its first beat, or the intro left out.
- **A flaky page test** (P2): `GenreEditorTest.ALength_IsShownAndGivenInMinutesAndSeconds_ATempoInBeatsAMinute`
  fails now and then on a request answered 400, master's too; find which request and why.
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
  line cliché rising (the fifth, its sharp, the sixth) beside its falling one (`InnerLines`), should
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
- **Plain doubling,** a part doubling another in unison, now that the parts play by their pocket (`Pocket`).
- **A motif stretched,** its rhythm doubled, beside moved and inverted (`Soloist.MotifInversion`), should listening ask.
- **A solo's runs in fives and sevens,** a fusion player's grouping, which the feels give a whole part but perhaps not
  a run; if not, a case of segments in any pattern (see *Parked*), not a mechanism of its own.
- **Legato slides** (P3, a case of legato): a slide strikes its note anew, where a player's note before goes on
  sounding into the next; holding the note before into the slide and leaving the next unstruck edits two notes at
  render. Built if the struck slide sounds like a stutter.
- **Long fall-offs** (P4): a fall of an octave into nothing at a note's end, a slide's kind of its own, if missed.
- **A filter drawn over time** (P3, if a wobble's restart on every note is heard): a part's brightness drawn through
  its rests rather than while its notes sound, and the filter's shapes a vocabulary as data anchored where they mean
  something; the part's one curve now adds its tone and a filter's closing note by note (`ExpressionRender`).
- **A swell brightening** (P3, by listening to `?listen=tone`): brass and strings brightening as they swell, back up
  to their sound's own, never past it on an acoustic instrument (*The brightness's range*).
- **A dip in place of a drop** (P3, by listening to `?listen=mix-filter`): the band filtered over the bar before a
  drop rather than silent, as a genre chooses; a dip skips a drop's bar now (`MixFilter`).

### Groove

- **A drum's pocket by its section's role** (P3, if a section's change of role is heard): the timing takes a drum's
  offset by its role in the song's groove (`Pocket.OffsetOf`), not a section's, which may move a ride to the time.
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
- **Approach chords, the rest** (P3): turnarounds, passing chords, a chord solo's planed approaches, chromatic passing
  chords from above and a later appearance reharmonised are built. A plain song is never reharmonised, by any of them,
  the songs' chance 0 at the plain end; a preset's may be above it, as Soul's and Bossa nova's turnarounds. Left: the
  passing diminished between roots a whole step apart, and chromatic approaches from below, a chromatic root wanting the
  whole chord moved a semitone with the lines over it keeping the target's scale; a chord solo's approaches on the
  strong beats at the wild end, and the dominant as its approach; the tritone substitute for V7 in a reharmonised
  appearance. Today a chord solo stacks the band's chord or planes its shape, never another chord.
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

- **Long cycles** (P4 since October 2026: a section's bars repeating, AABA to ABCD, already give every part, the drums
  too, figures of two and four bars, a varied repeat (A′) starting as its bar did and then changing, near an answer;
  the clave and the montuno are figures, *Figures as a genre's data*; built only should listening find the second bar
  no answer, or a figure want a note across the bar line): a bar
  pattern is one bar long, so a slower cycle, such as the kick's slowed to two bars, plays its first half and starts
  again at every bar line. Patterns as long as their cycle would let slow
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
- **The page's tests of sound** (P4): the page tests drive asking for songs, not hearing them; playing, seeking, the
  MP3 export and the lock screen's controls are tried by hand, as are Safari and phones.
- **A report of thousands of songs** would make, measure and let go of its seeds past the shared corpus rather than
  cache them; a corpus song holds about 5 MB.

## Genres

A genre is the page's data (`Genre`, `GenreChoices`, `Client/genres.json`), every option three amounts, at the plain
end, the middle and the wild end; what each choice does is in the code and the numbers of each in its commit. What is
left:

- **Any genre** (P3, a plan in steps): a song asked for with no genre drawing a preset, or the songs' own as one of its
  outcomes, evenly, so that the plain band stays among them; the seed alone making the song again, the CLI's and the
  API's too, and the ratings telling which genres are liked. Steps:
  1. The presets the server's data rather than the page's alone (`Client/genres.json`), served to the page, which the
     tests and the CLI read too (`--genre`).
  2. The server drawing one when asked for any, from a stream of its own, so that the rest of the song stays as the
     seed made it; the preset drawn in the response, the song's link and every analytics event about it, as its version
     and settings are.
  3. The page's Any genre, its preset drawn by the page for every new song, drawn by the server instead.
  4. The default (the songs' own, as now, or any genre) decided by the ratings by genre once a version has enough, and
     not before the retuned presets, Pop and Metal most, are heard.
  Not weighed by the presets nor leaned by conventionality: each preset spans the plainest songs to the wildest.
- **Rock and metal as their bands play them** (P1, 8 October 2026): what each lacks against the sound of a riff-driven
  hard rock band, a blues-rock band's shuffle, a twin-guitar heavy metal band, thrash and groove metal
  (`RockAndMetalReportTest`). Their solos (73% and 78% of songs) and their riffs of two and four bars (AAAB and ABAB a
  third of their sections) were there already; the organ's solo, Rock's shuffle at its plain end and Metal's clean
  quiet sections are presets now. Left:
  - The solo harmonised (P3): a second guitar a third under the solo, as the riff's twin plays the riff
    (`TrackRole.RiffTwin`); a twin-guitar break.
  - The riff as the song's signature (P3): the riff alone opening the song and between its sections; one part alone
    is a texture already (`TextureKind.Alone`), the part not chosen as the riff, nor placed as an interlude.
  - A breakdown (P3): a section slower and sparser, the riff at its lowest, half time leaning, beyond half time alone.
  - Tremolo picking (P3, measured first): fast repeated notes unmuted, the riff's speed perhaps enough already.
  - A melody sung rather than played (P3, a listening question first): no singer in General MIDI; the melody's
    rhythms word-like, its breaths and held ends, should it sound like a lead guitar playing a tune.
- **A pitch-bend dive at a section's end** (P3, by listening to `?listen=djent-stutter`): Djent's riff diving before
  the heavy section; bit-crushing, tape stops and glitches are beyond General MIDI (*Parked*).
- **Black metal and IDM, the rest** (October 2026, `NewGenresReportTest`): Black metal tremolo-picks 51% of its
  sections, blasts 20%, double-kicks 20%; IDM, warm and swung at its plainest, an acid line, odd meters (47% of its
  wildest songs), polymeters and rolls at its wildest, breaks 24% of its sections. Left:
  - Rolls splintering into 32nds, a breakbeat chopped and drilled, figures finer than a 16th (P3, by listening): the
    figures' grid in 32nds, should IDM's wildest want more than its hi-hat rolls and fills give.
  - A riff on an open string's drone, its notes alternating with the pedal, black metal's and folk metal's (P3, by
    listening): the riff's pedal (`Line.PedalShare`) leaned by a genre.
  - Black metal's interludes: its pad alone now (`GenreChoices.Alone`); an acoustic guitar alone between the storms
    (P3), the part alone switching to a quiet instrument of its family in a quiet section.
  - Field recordings, a prepared piano, glitch edits (*Parked*, beyond General MIDI).
- **A line's tensions beyond Djent** (P3, by listening to `?listen=djent-tension`): jazz's and IDM's melodies and
  counter-melodies on a tension now and then (`GenreChoices.Tension`).
- **More genres** (P3): chiptune, once a square wave may play the chords (*Instruments outside a role's pool*).
  What the genres made in October 2026 still want: Fusion's
  roots off the scale (the progressions move by a 7-note scale's steps, chromatic and constant-structure motion
  coming only of planing and a chord's own scale) and a lead's wide-interval legato lines (*A melody's leaping by
  genre*); Soul's tritone substitute and passing diminished (*Harmony for Jazz and Soul*); Country's bass on the root
  and the fifth within the bar and Bossa nova's two-bar clave (below and *Long cycles*).
- **The hook and the phrase scheme as one** (P3): a hooked section a scheme sung literally, should a trial keep the
  report's repeats (50% of a hooked section's notes at their first bar's places); otherwise kept apart, with the reason.
- **Harmony for Jazz and Soul** (P3): the tritone substitute, the passing diminished and a ii–V elsewhere (*Approach
  chords*, *Harmony's devices*); call and response (*The band's devices*); a part resting by its section's role.
- **The choices grouped into families** (P4): bass, harmony, form, drum devices, one choice of several chances rather
  than fifteen of one, which would read better in the editor; should the editor grow hard to use.
- **What the genres cannot play yet** (P3, each by what listening to the presets says; `GenreDevicesReportTest`,
  `GenreIdiomsReportTest` the measures):
  - The loop's figures shared, the bass's and the riff's, beside its harmony (`GenreChoices.Loop`).
  - A riser as a lift cue, into a louder section where the band plays on, which the riser into a drop
    (`Risers`) plays on a channel its silence leaves free: a program change there would cut the part's
    chord, so a channel of its own, should a lift want one.
  - The pump on the kick wherever it falls, should a pumping genre's kick leave the beat; it ducks on the beat now.
  - The pinned drums (`GenreChoices.Pins`) in the bars a drop or an entry clears, and the wilder songs that leave a
    role free.
  - A snare's drag and its ghost notes, which want the backbeat's own figure, the pinned backbeat keeping its hits
    clean.
  - The piano's montuno, a broken chord off the beat, an arpeggio's pattern of its own (`Arpeggios`) rather than a
    figure, the figures striking a player's chord or note; a second time keeper playing straight under the tresillo.
  - More figures (P3, by listening): Bossa nova's clave, a figure for the hi-hat or the snare, a figure of more than
    two bars; the double kick following the riff's accents alone, should every 16th sound a machine.
  - A hook of one or two bars sung through a whole section, its phrase ends let go of; a hooked melody's bars repeat
    about half at most now, a phrase's last bar ending on its held note.
  - A fill's drums leaning to the toms by genre, which the drum groups' state draws, not a genre's choice.
  - A melody's leaping by genre, a lean of its register's freedom and its leap's size: leaning its stepwiseness moved
    the mean move by a tenth of a semitone and the leaps not at all, a melody's leaps coming of a phrase starting
    afresh in another register, its echoes and its landings.
  - The bass's root and fifth figure within the bar, and Jazz's walking quarters, as its rhythm settings have them.
  - A part resting by the section's role (a dub's breakdown), with *A genre's sections differing*.
  - A genre's lean of the phrases' place rules, should it press them, and the songs' own sections of eight bars now
    and then.
  - Articulations played by a plugin's output as a keyswitch or a controller, the one place in `Render` the seam, once
    an output differs from General MIDI.
  - Beyond General MIDI, parked: the gated reverb (one reverb send for the whole drum channel, no gate); an acid
    line's slides and accents past the filter's squelch (`StrongEffect.Squelch`), which has no 303.
- **A head with solos** (P4, parked): a jazz tune's head, choruses of solos over its form, and the head
  again, which AABA (`SongForms.Forms`) and a genre's solos (`GenreChoices.Solos`) come near already; a form of its own
  only should listening ask.
- **A genre's sections differing** (P3, perhaps never): a value given a section role, such as a solo swung harder
  or a chorus louder, the genre's for the song otherwise; the generator asking the genre with the section's role,
  every choice's tile a role's override beside it. The dynamics' contrast and the phrases' AAB give most of it.
- **The rhythm's values against the part's own:** a value moves a setting by steps from where the part rests, so that
  one part's "faster" is another's "as fast as can be"; a value set outright, where it lands, would read the same for
  every part, should listening ask for it.
- **A link replacing the settings kept** (a decision): opening a shared song makes its settings this browser's own,
  so that the next song goes on in them; keeping one's own and offering a way back to them is the other way, should
  listening to shared songs ask for it.
- **Instruments outside a role's pool,** with their leading drawn from their family.

## Parked

Ideas with a reason, not planned: none has shown a need. Taken up only when listening or another item asks for one.

- **The drums one by one** (P4): each drum's tuning, pan and sends as GS drum parameters, a dry kick under a wetter
  snare, Trap's kick tuned down, the toms panned: a mix's realism.
- **A flanger** (P4): the chorus's type set to a flanger, which every part sending to the chorus would take at once.
- **A song's mood** (P4): a value drawn per song that a preset's options lean dark or bright by, so that one preset
  spans liquid and dark drum and bass coherently; a preset of each until a third genre asks for a span.
- **The percussive family but the steel drums and the reverse cymbal** (P4): the woodblock, the agogo, the melodic tom
  and the tinkle bell are the kit's own sounds again; the synth drum's 80s toms are fills, on the drums' channel; the
  taiko wants a cinematic genre none is.
- **Sound effects** (P4): General MIDI's 120-127 and the drums' sound-effects kit sound unlike in each soundfont and at
  each pitch, and read as a mistake rather than as meant; a gunshot, a telephone or a helicopter never, applause a
  gimmick, breath and fret noise realism. One experiment, should listening ask: a bed of the sea, birds or rain held
  under an Ambient intro or outro, a part of one held note and no harmony.
- **Sound design beyond General MIDI** (P4): bit-crushing, tape stops, glitch edits and a high-pass filter, a modern
  metal or electronic producer's, which no General MIDI controller moves; a pitch-bend dive the one near it.
- **Ping-pong echo** (P4): an echo alternating sides, from the part's pan to its mirror, which the GS delay's left and
  right times may make once the echo is the synthesizer's.
- **Modulation** (blocked until a soundfont maps it): CC1, which none of the soundfonts offered maps to tone. The rule
  to keep: modulation and a vibrato never at once, modulation and bends together.
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
- **A sustain pedal** (P4): a piano's or a harp's notes held by the pedal (CC64) across a chord, left from the
  review of October 2026; the notes are held to the next already.
- **A parallel CLI batch** (P4): `--count` making its songs in parallel; a song takes about a second.
- **Memory:** every `RealizedNote` keeps the state it was decided from, about 1.7 MB a song: nothing to do.

## Decided, and kept

- **The brightness's range** (measured October 2026, spessasynth rendering one note of each family in both soundfonts
  served): closing the brightness (CC74) below the middle darkens every family, the distorted guitar too, to half its
  brightness or less by 48 for a few dB; below 40 most lose 10 to 40 dB, the pads first, so a filter's floor is 40.
  Above the middle only the synths brighten (two to ten times), the acoustic instruments open already, so a tone
  darkens from the sound's own and brightens back to it, never past it but on a synth. The resonance (CC71) adds up to
  8 dB at 84.
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
