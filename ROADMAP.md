# Roadmap

What is to be done, what listening is to settle, the ideas parked and the decisions that keep what is built so. What
is built is described where it is built, in the code, and its numbers are in the commits that made it; an entry here
is removed once it is built, its leftovers kept under *Open work*.

## Priorities

1. **Now** (P1): *Planned: a mood every genre plays* (*Open work*), its brightness measured first; then a soloist's
   ghosts and the counter-melody's third over a sus4 beside it.
2. **The review of 16 October 2026** (P1, *Open work*): its packages, the failing tests first at the review's commit.
3. **The rules on the other corpora** (P1, *Older rule failures*): the failures older than the second order of work,
   each traced at the committed code first.
4. **Listening** (P1, alongside): every deployed question answered or let go of before the next round, the rounds
   built by the rules of the full review's skill (a song without that changes the device alone, a difference measured
   before it is asked).
5. **The ratings report** (P2, *Listening data*) once the dashboard shows songs rated enough; the default genre by
   the ratings with it.
6. **When the need shows** (P3): the leftovers and the put-off items under *Open work*, each by what listening says,
   with its reason; no new presets until the owner asks (15 October 2026).
7. **Parked** (P4): kept for their reasons, not planned.

The owner's calls waiting: a guitar solo in Rock's and Metal's plainest songs, which the curve rule leaves out.

## Listening

What listening is to settle: each a question, its measure, what it decides, and where the page's listening mode asks
it, `?listen=` its id on the test instance (`ListeningQuestionsTest` writes them, ten at most, the dashboard tables the
verdicts, by song too). A question's songs are those of the corpus's first 24 that play its device where it is heard
the most, by how much its song with the device and without differ as heard (`HeardDifference`), from a pattern's start
in the sections its device plays in; its song without lays its patch over the genre's options. Done for a version when
every question is answered or let go of. Nothing is asked now: the fourth round (16 October 2026) is answered.

**Settled by listening**, so that no plan or review asks again unless the code it heard has changed:

- **Kept** (the device as built, its amounts in the presets): every question of 8 to 16 October 2026 not named below,
  among them the clave, stop time, ghosts, pushes, the drums' marks, louder forms, ornaments, the pocket, the solo's
  answers, doubling, figures, the echo, arpeggios, the breakdown, the riff intro, the twin solo and its lead up the neck,
  the riff's hook, riser, rudiments, phrase lengths, short bars, held tones and let ring, calm sections, call and
  response, the big ending, ensemble hits, bass on the kick, the waltz, the drone, imitation (now bent to the melody),
  the mood's spans of Synthwave, Drum and bass and Thrash, and the stepping-down bass of Cinematic piano and the
  Power ballad.
- **Taken out or lowered:** Disco's pump, Metal's turnaround, Trap's reharmonised last hook, Funk's swung 16ths and
  Folk's (their swing the songs' own, an 8ths' shuffle above about 107), Fusion's AABA, Ambient's swing (Bossa nova's
  root and fifth too, back at 30-50-50 since the 14 October retune and the cycle's notes nearest since, to ask again),
  the riff's twin a third over a lowered riff (now an octave up), Techno's rolling 16ths bass (its offbeat figure
  alone), the songs' double time halved (a scramble), Doom's blues scale reduced, Rock's key lift halved, the melody's
  inversion, the pinch's squeal.
- **Let go of, not told apart or too faint to ask** (as they are): Djent's polymeter frames, `pop-melody-chords`,
  `trance-raise`, `pop-eighths`, IDM's 64th rolls, Dark jazz's and Black metal's darker moods
  (`darkjazz-dark`, `blackmetal-minor`), and Trance's and Trap's mood ends, unheard because a mood leans only a genre's
  own pool (*Planned: a mood every genre plays*).
- **Tied and kept as they are:** Pop's held tones, Classical's cadence, the jazz ride, Pop's instrument switches,
  Black metal's two endings.

### The song and its sections

- **How plain by default:** drawn songs centred at 0.5 (`Unconventionality.BaseCentre`), 5% plain in every convention
  (`ConventionalityLevelsReportTest`): too adventurous for songs of no genre? Decides a centre of 0.375, or not.
- **The forms:** verses and choruses heard as such, the chorus lifting; sections of 80 bars at the median; an intro or
  outro of its own material (a third and a seventh of middling songs) the same song; a last chorus twice (17%). Decides
  the forms' weights and the last repeat's chance.
- **The sections' energy:** quieter sections heard as sections, louder ones lifting; a last appearance 0.10 louder
  than the first; fills into a quieter section 54% of the time. Decides the energy's contrast and the fills' landings.
- **Rests, entries and textures:** breakdowns and melody-less sections as arrangement or parts gone missing; parts held
  back at the start (15-18%); one part alone (`orchestral-alone` heard better, 12 October 2026) or parts dropping out, leaned to the quieter sections (`TextureTest`).
  Decides the textures' weights and their lean.
- **The lift** (`lift-cues` heard better, 8 October 2026): a crescendo of 8 of 127 into a louder section, and the drums' cues, 2.6 a song.
  Decides whether the cues stay, and how often.
- **Key changes:** two a song in the fourth fifth; prepared ones, pivots, pedals, sequences heard. Decides their
  chances and devices.
- **Tempo:** 6/8 counted in quarters; 13/16 too busy? Decides the odd meters' weights.
- **The fade-outs:** a ritardando into the fade, the drums fading first, a tag after it? Decides whether to build one.
- **The settings panel:** a song made again with changed settings sounding moved rather than jumped; the pad in 80% of
  songs, the counter-melody in 60%, the drums in 95%. Decides the parts' chances.

### Harmony and lines

- **The section modes:** a mode change prepared by its cadence; the parallel and harmonic minor heard (seeds 196, 10,
  36, 171). Decides the modes' weights.
- **Harmonic rhythm:** a chord every half bar busy at 175 bpm, one every two bars static at 90? Decides leaning the
  span by its length in seconds.
- **Phrases:** question and answer, varied repeats (A′ at 0.25 in a middling form, none in the plainest and every one in the wildest, `PhraseSchemes.VariedRepeatChance`), the melody's
  leaps at a phrase's start (a third), the sequences rising with the phrase. Decides their chances.
- **The gaps before chord changes:** half the changes with no melody in their last beat, the solo part answering now
  (`solo-answers` heard better, 8 October 2026). Decides filling them.
- **The walks into a section:** the melody at 15% of changes, the bass at half of the runs. Decides their chances.
- **Double stops and the riff's chords:** the melody's on a chord instrument's downbeats in a quarter of sections.
  Decides their chance.
- **The bass's pickup** always on the beat: square? Decides an 8th before the bar line, leaned by convention.
- **A walking bass** (from the review of 9 October 2026): its weak quarters keep to the chord's notes, so a ii–V may
  walk arpeggios rather than a line. A report of the share of Jazz's bass notes on beats 2 and 4 outside the chord
  (about 30-50% in a walk) decides letting them take scale steps and chromatic passing notes where a genre leans its
  leading high.
- **Octaves and twins** (`doubling` heard better, 8 October 2026): a line in octaves in 10-20% of appearances, the riff's twin in 15% of riff
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
- **Trading with the drums** (`trade` heard better, 8 October 2026): Jazz's solos trading with the drums by turns, the band laying out. Decides
  the trade's chance by genre, and whether the band keeps time under the drums' turns instead.

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
- **The drums' marks** (`drum-marks` heard better, 8 October 2026): a strike every three bars and a swell into a section. Decides the marks'
  rate and the swell's length.
- **The ornaments** (`ornaments` heard better, 8 October 2026): one in eight to ten long notes, 2.5% of Folk's and Medieval's lines; a trill
  up to 1.5 s. Decides their rate and the trill's length.
- **The polymeters** (`?listen=polymeter` to be asked first, Djent's sections with and without): the snare on every
  cell's start; the chords joining a cycle. Decides who joins a cycle, and the frame below.
- **The pocket** (`pocket` heard better, 8 October 2026): the backbeat a little late, the bass a touch ahead, the lead laid back. Decides
  the offsets.
- **The figures** (`kick-lock`, `double-kick` and `gallop` heard better, 8 October 2026; `?listen=clave`): Metal's sections 27% with the riff, 14% a
  double kick, 15% galloping, Nu metal's 42% with the riff (`RockAndMetalReportTest`), Latin's claves on the clave.
  Decides their weights and leans.
- **Pushes** (`pushes` heard better, 8 October 2026): the band's changes an eighth early. Decides their chances by genre.
- **Stop time carried** (`?listen=stop-time`): the band hitting once a bar under the lines, the drums keeping time
  going on where the lines rest (Blues and Rock, none of 700 stopped bars silent past its hit, from 16%). Decides its
  chance, and whether the drums keeping time is the band's device or the band should stop with them.

### Sound

- **The velocity:** the bass's and the chords' evenness; bar layers adding 3 to 5. Decides the layers' spread.
- **The echo as the synthesizer's delay** (`echo` heard better, 8 October 2026): the Roland GS delay's tone in the player, its feedback
  (100, about half), its darkness (2 of 7), the send (100), and a dub snare's quiet strokes echoing with its backbeat,
  the delay's send being a drum's, not a stroke's. Decides those settings, or the echo's notes back.
- **Metal's arpeggios** (`metal-arpeggio` and `djent-arpeggio` heard better, 8 October 2026, `MetalArpeggiosReportTest`): a third
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
- **The filter:** wobble, squelch and opening, Techno 11%, Trance 28%, Dnb 26% (`FilterTest.Report`); a General MIDI
  synth bass wobbling thin? Decides its depth and resonance.
- **Double-tracking:** Metal 2.8 parts a song at the sides (`DoubleTrackingTest.Report`): a wall, or mud? Decides the
  delay and which parts stay single.
- **The dub echo, the insertion effect and the rooms** (`DubEchoTest`, `InsertionTest.Report`): echoes as notes thin,
  the wah's and phaser's settings, the rooms wet enough? Decides the GS delay and the effects' settings.
- **Louder forms** (`louder-forms` heard better, 8 October 2026): a clean guitar turned driven when loud. Decides its chance by genre.
- **The bass under E1** (9% of its notes, `RegisterReportTest`). Decides the bass's range.

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

## Open work

Each item with its plan, and what it waits for.

### Planned: a mood every genre plays, and a soloist's ghosts

Planned 16 October 2026 from listening: `trance-bright` and `trap-dark` heard as their neutral songs, and Trap at full
brightness as at full darkness. The mood leans only the options a genre's pool names (`Mood.Leans`: the scale and the
chord shapes by darkness), by odds of 4 at its ends, and Trap's scales are all minor: at darkness -1 and 1, 64% and 66%
of its chords minor, 23% and 26% major, its scales minor either way (Dorian 7 songs and 1 of 64); Trance's major songs
31 and 4 of 64, but its major chords 33% and 28%. `HeardDifference` measured the questions' songs apart (0.35 and
1.07), notes the mood's draws moved, not how bright they sound: a mood's question wants a measure of brightness first.
Not implemented. Rewritten the same day from the owner's proposal, one scale of mood every genre shares: at a level of
mood every genre takes comparable scales and chords, a genre leaning only to a level of its own, so that Black metal
given its brightest plays bright and is heard so. Reviewed against a strict reading, one palette for every genre at
every mood, which would cost a genre its harmonic taste inside its own span (Blues' Mixolydian and its blues scale and
Pop's major alike at one darkness); kept, the blend below. Each step through the implementation loop:

1. **A measure of brightness**, built (`MoodReportTest.Ends`, 94e20e2, the review's `run/mood.txt`): every preset at
   darkness and intensity -1, 0 and 1 given, 16 seeds. The target, every genre's major share at least 20 points apart
   between its darkness's ends, is met by none but a few: the songs' own 61% to 43%, Medieval 89% to 58%, Ambient 84% to
   58%, Djent 50% to 0%, Dub 74% to 47%; Funk 87-92-90%, Black metal 15-17-19%, Trap 28-33-33%, Rock 65-57-54%. The
   intensity moves a genre's tempo by its range, but leaves the songs with no genre at 126 bpm and 7.3 notes a second
   at either end (the tempo's lean a genre's alone, `Mood.Leans`).
2. **The chords' degrees leaning by darkness**, put off from the mood's first plan: a progression's chord on a degree
   weighed by its chord's quality as the scale has it, major the brighter, minor and diminished the darker, by
   `Mood.Odds`, in every genre's scale without an option added: a minor key's ♭III, ♭VI and ♭VII the brighter, its i,
   iv and v the darker, a major key's vi, ii and iii the darker. What makes a mood heard inside a genre's span.
3. **A mood past a genre's span reaching the songs' own options**, the farther the more: inside its span (its
   `darkness` from and to) a genre's pool as it is, leaned by the mood; past it the songs' own options blended in,
   none at the span's edge and wholly at the end beyond it, then leaned by the mood, as `ByConvention.Filled` plays the
   songs' options at an end a genre allows none at. On the darkness's choices alone, the scale and the chord shapes,
   by the darkness every option has already (`Scale.Darkness`, `ChordShape.Darkness`): no table of moods, no data per
   genre. A drawn mood keeps inside its span, so a genre's drawn songs change only by step 2; a given one past it is
   heard (*Drawn or given*). CLAUDE.md's *a genre's pool keeps to the options it names* amended: a mood past a genre's
   span reaches the songs' own options, the farther the more.
4. **The sound by darkness**, only if 2 and 3 are not heard enough: a synth's and a pad's filter closed the darker,
   its room longer, the chords' voicing centred a few semitones lower, never an octave.

Review. *Overengineering*: one lean of the degrees' weights and one blend by the distance past the span, both on
values the options carry already; the intensity left out of the blend, as its kit and instruments past a span would
swap a genre's sound (brushes in Black metal) where its tempo, fills and density are heard in every genre already;
step 4 only on measurement. *Musicality*: the ends are sounds of their own, a bright black metal tremolo-picked in
major, a melodic trap in major, a dark trance; a bright minor song plays its relative major's chords (♭VI-♭VII-i), a
dark major song vi and the minor iv; the genre's instruments, drums, rhythm and form kept, heard in another colour.
Risks: Locrian at the dark end, its tonic diminished, kept off a plain song by its conventionality's plain weight of
0, so plain dark stops at Phrygian; the exotic scales' darkness (harmonic minor, Phrygian dominant, whole tone) set
by rule, to check by ear before the ends are trusted; a dark major song turning so minor its key is lost, the degrees
leaning by the odds and never leaving the scale. Then `trap-ends` asked, Trap's brightest against its darkest, and a
genre past its span (Black metal at its brightest) against its own.

**A soloist's ghosts** (16 October 2026, from listening, `funk-dead-notes`: dead notes useful in any solo that plays
percussively, never in its fast tremolos). Now a Funk-family soloist mutes its 16ths between the 8ths by the band's
rule (`Articulations`), a pedal's repeated notes among them. The plan: decided where the solo is written
(`SoloLines`), where its gesture is known, the Solo part out of `Articulations`: in a lick, the percussive gesture, at
16ths or slower, not a pedal shape, an unaccented note now and then muted, never a run, a held note, a release or
anything faster; in every genre, a genre option `articulation.soloGhosts` the songs play, Funk's leaning it more, so
that a question can switch it apart from the band's; a stream of its own per soloist. Reviewed: no state kind and no
table of instruments; a piano's ghost only a soft short note, accepted; a twin lead mutes as its riff does; measured
by soloist, the chance lowered where a soloist's vocabulary of licks mutes 10% of its notes or more; then
`solo-ghosts` asked.

**Over a sus4, the third** (16 October 2026, song 2b of `chamber-canon` heard dissonant on its sus4 chords). The bass
landed its inversions on the scale's third whatever the chord, 13.1% of its notes over a sus4 its missing third, a
semitone or a tone under its fourth; now a landing on a note its chord leaves out takes the root, 3.5%, its walks'
passing notes, as over a major triad its fourth (7.7% weak, 2.4% strong). Left: the counter-melody's weak notes over a
sus4 its third 12.3% (5.0% a triad's fourth), likely its harmony a third from the melody's fifth by the scale; to
measure by doubling and, if so, a harmonised note its chord leaves out moved to the chord's.

### From the review of 16 October 2026

The review's fixes are its plan (outside the repository, `rmg-reviews/2026-10-16/plan/plan.md`); its three architecture
proposals are packages there, the holds written by their makers, the pulse on a place and one list of plays. What it
proposed beyond fixes, reviewed as plans, each one mechanism over a case each:

- **Figures as chances** (the foundations reviewer, #39), its experiment built (package 03b of the review's plan): a
  loose figure (`Figure.Loose`) plays its onsets and the player's own notes each by a chance, a note on both always, by
  how strictly the section plays it (`Figures.Strictness`, the feel facet's 1-0.9-0.6), exact at the plain end. Latin's
  bass plays the tumbao so (`tumbaoChances`): its notes on the figure 76%-73%-57% at feel 0, 0.5 and 1 against the
  tumbao laid's 76%-76%-71%, its bars the figure exactly 26%-16%-1% against 26%-23%-12% (`FigureChancesReportTest`).
  Asked as `latin-tumbao-varied`; a migration of every figure is planned only if listening prefers it, else the option
  goes and the verdict to `decided.md`. Built where the figures are laid, after assembly, rather than inside the rhythm
  generator, whose patterns are drawn before a section's figure is.
- **Waiting on figures as chances**, as each needs a player the figures do not reach: the conga's tumbao (#354, twelve
  presets name the conga), the reggae organ bubble on the pad (#355, Reggae, Dub, Ska), the boogie comp on the rhythm
  part (#340, Rock, Blues, Country, Punk, Funk rock; the bass's boogie steps shared).
- **Figures as they are:** the thrash or skank beat, the snare on every 8th's offbeat over the kick on the beats, a
  backbeat figure (#370, Thrash, Punk, Black metal, Ska, Metal); the arpeggiated bass, the bass breaking its own
  chord over `Realizer.GetChordNotes`, beside the octave bass (#402, Chiptune, the 16-bit game, Synthwave, Disco).
- **Form:** the turnaround intro, the first section's last phrase as an intro played as that section (`PlayAt`)
  (#363, Blues, Jazz, Soul, Country and five more); the AABB tune form, two forms of `SongForms.Forms` at weight 0 for
  a genre to take (#360, Celtic, Bluegrass, Medieval, Folk, Chamber); the last chorus up an octave, a chance of
  `lastRepeat` on the line raise the twin uses (#366); the tag ending, an `EndingKind` repeating the last bars with the
  cadence turned away by the turnaround's kinds (#342), after the others.
- **Lines and harmony:** the prepared suspension, an option of the appoggiatura holding the line's note over the change
  (#395, nine presets); the hemiola, a cadence's two bars in a meter of their own as the short bar is (#396), later.
- **Drums and sound:** the sub drop, the bass's root falling an octave on a drop's first beat by the riff's dive
  (#381, the ten presets with drops); a pocket ahead of the beat, the pocket's lean below zero (#387, Bluegrass, Punk,
  Thrash); the chopped loop, `Mutations`' swap and ratchet laid on a looping section's pitched parts (#352, Hip-hop,
  Trap, IDM, House, Techno, Drum and bass).
- **A scale's darkness by its third** (#117), with the mood's plan above, whose steps lean by it: the sum of a scale's
  offsets ranks the seven modes but makes Hungarian and melodic minor as bright as Mixolydian and harmonic minor as
  dark as Dorian, so a dark mood and a loud section's brighter scale misjudge them; its third first, minor against
  major, the sum breaking ties, measured on `MoodReportTest.Ends` before and after.
- **The swing's clamp at a section's own tempo** (#64): the pair's second note kept to 0.1 s at the song's tempo, so a
  section a fifth to a half faster (`SectionTempo`, 4% of sections at the middle) may play it near 0.07 s; the swing
  is song-wide state, and a clamp per section a swing of the section's own, put off until a fast swung section is
  heard rushed.

### Older rule failures

Rules that fail on other songs than the corpus's own, each older than the second order of work (15 October 2026), to
trace at the committed code before blaming a change:

- **At offset 816000000:** the rhythm part's realized note at a change keeping the chord before in its state (seed 57
  at 180, its pitches the new chord's, so the realizer's merge); a chromatic passing chord a semitone from the next
  (seed 1 at 6); a fill decision recorded at bar 5 of a four-bar phrase (seed 6, a hook intro's song); a palm-muted
  note's length; seed 21's bass at 90 landing a semitone over its chord's root, placed so by `LinePattern.Place`;
  shapes apart (seed 5 at 173.5 after a reharmonisation, seed 49 at 44 the melody's triad over the chords' cluster).
- **At offset 716000000:** a polymeter's chords off its cell (seed 26 at 248, 6-16th cells); a soloist's stacks; a
  line's fall from the root; a passing chord (seed 15 at 22, seed 4 at 2); a filtered drop's band (seed 29 at 608); a
  section's own key (seed 50); a pushed change (seed 58 at 192 and 224); a strum's direction (seed 15 at 148); the inner
  line; every chord note in its scale; the render's notes; a djent breakdown in 15/16 whose kick pattern plays no note
  (seed 48, section 7), so the kick with the riff has nothing to lay on.
- **From the review of 16 October 2026,** both failing at its commit already:
  - **At offset 942000000:** seed 5's bass at 552 plays F#, the fifth of its Bb chord raised, its own alteration +1,
    where the raised step (G minor's F#) ended at 550. The note is an anticipation (`ChordApproach.Anticipation`) and a
    key preparation leads into 558: likely the line placed against the chord before the change, then read against the
    next.
  - **At offset 619000000:** seed 47's shapes apart at 256 and 400. Every part sounds the same chord (pitch classes 2,
    5, 10), but the chords and the melody write its third in semitones (5/12 of an octave) where the bass and the pad
    write it in scale steps (2/7), on a chord approached a semitone below (`ChordApproach` 2). The same kind as seed 5
    at 173.5 above: one stage rewriting a chord's offsets on some parts only.
- **A long lift moving notes outside its swell** (#224): about a hundred notes of 64 songs moved outside the swell's
  bars by more than 0.01 of the velocity, through a stage that sets a part by the song as heard (`LiftTest`).
- **Two page tests seen failing once each,** their messages not kept: the seek readout's
  (`TheKeyChordTempoAndMeter_AreShownAsTheSongPlays_AndFollowASeek`) and the length's
  (`ALength_ShowsHowLongTheSongPlayed_AndGiven_IsAskedFor`), to read when they fail again.

### Put off, with their reasons

Each taken up when its reason no longer holds or listening asks:

- **The false ending** (#372): the band hitting the home chord, a beat or two of silence, the drums counting back in;
  its hit a harmony edit before `Realizer`, for a gimmick that wears thin, rare even where it plays.
- **The tape stop** (#358): every pitched part bending down an octave over a slowing tempo into a drop or as the
  ending; a tempo map and every channel's bends together, the most cost for the least.
- **Two toms at once:** a drum note's sound a list (`StateKinds.ArticulationIndex` one now, the toms one track), a
  render change, until listening asks for the weight (*Fills and lines*).
- **A riff's dissonant stabs** (Nu metal, measured twice): its ♭2 and ♭5 already 3% of its chugging notes off the beat,
  and leaning the riff scale left it at 3%; a stab palette leaning to them, the scale rule weighed against a chromatic
  stab, first asked as a question.
- **The bar patterns' finest note by the tempo:** a shortest note in seconds lets 32nds through at slow tempos and
  moves every pattern bouncing off the cap (`ResolvedRhythm.Of`); taken up with rolls splintering into 32nds.
- **Interlocking parts** (Funk): the rhythm part striking where the chords do not; the rhythm part is no figure's
  player and would need its turn after the chords'.
- **The punk beat's offset kick,** until a transcription confirms where it falls.
- **The cymbal choke before a stop,** with the crash the ensemble hits want, left to listening.
- **The press roll into a solo,** a fill of its own kind before a soloed section.
- **The echo on a chord stab,** with the dub techno preset that wants it.
- **A part's own swing** (the ride harder than the soloist): the render's one swing for every part of a song.
- **The ♭vi turned minor and the diminished seventh's roots** (Black metal): a chord's scale never moves the key's
  own tonic; should listening miss them beside the minor mediant.
- **The melody's fanfare run into a section:** a sung melody is reshaped after the lines, no note finer than an 8th, so
  a run's 16ths collapse; to build with a run kept from the singing.
- **The post-chorus quoting its chorus's chords and second half:** the hook intro's quote carried to a section, which
  every stage then treats as its source.
- **A power metal preset** (the owner's choice), and every new preset until the owner asks: psytrance, dub, Celtic
  and bluegrass are built; jazz-rock, darksynth, pop punk and the acoustic rock ballad as the mood leaves them;
  later, by listening, Latin jazz, modern pop, ska punk, epic trailer, G-funk, tavern, alternative, drill'n'bass,
  groove metal, delta blues, big band, dub techno, drill, a Renaissance preset and a piano ballad. Not planned:
  symphonic black metal, gothic metal.

### Left of the built plans

What the plans of 8 to 15 October 2026 left, each by listening unless said:

- **Phrases:** a genre's section lengths named in bars of four (`bars16`) play their patterns as often whatever the
  phrase, so a section of "16 bars" in threes has 12; an intro's window of four bars, which a first section in threes
  does not end a phrase with.
- **The review of 10 October's open findings** (`reviews/2026-10-10.md`): the chance shapes' per-section shares, the
  outro's and a kept tempo's shaped (110); a preset's copy of the songs' amounts written as "own" (467), should the
  copies drift.
- **A section's form:** a calm section's bass held on its roots; a fill or a fade into it; Heavy metal's and Rock's
  calm; Black metal's drone, its tremolo figure repeating a note.
- **The metal family:** a slap bass in Nu metal's quiet sections alone; Doom's slow ♭3–4–♭5 motion heard as a blues
  lick or not, and a held or bent ♭5; a melody spoken rather than sung (repeated notes, a range of a fifth), with *A
  melody sung rather than played*.
- **The researched idioms not built:** the bass gallop; Metal's plagal motion (low confidence); a riff's AAAB; tempos
  over 240; parts muted in and out on a loop; a long instrumental stretch before the first vocal; stoner doom's wah and
  pentatonic riff; the octave whammy squeal; a china on a cell's start; a kick moved off the snare; House's offbeat bass
  and congas; darksynth's split of Synthwave; IDM's vignettes (a one-minute song as one section); a bass note pushed a
  16th early; a wah on the bass; the bass playing the head while a synth takes the bass line; a meter laid over another;
  the triangle's bass (General MIDI has none); sonata form's recapitulation; a drum part with no steady pulse; a
  whole-piece swell in register; the kill-switch stutter; a tresillo bass arpeggio with chromatic walk-ups; Jazz-funk's
  two strands along its conventionality.
- **Devices' leftovers:** the ostinato's clash on a strong beat kept at the wild end; a genre's figure for a section's
  muting (ska punk's choruses); two soloists trading still rare (Thrash 6% of its pitched solos, Jazz 15%), as few
  songs have two parts free; the carrier's broken chords in even, short 8ths under the echo cascade; 8-bar blues
  patterns left free.
- **Guarded, built only if listening asks:** the brushes' swirl keeping time (Dark jazz), if the ride's taps are heard
  too busy; turntable scratches (Lyrical guitar), if both soundfonts sound them; legato slides, if a slide is heard as a
  stutter.

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

- **A polymeter's bar giving way to its riff** (P3, the frame a power of two of bars done 11 October 2026,
  `Polymeter.Frames`, 4 bars 43%, 8 23%, 16 8% and 32 7% of the polymeters, Djent's leaned to 8 and 16, not told
  apart by listening, `djent-frame`): a section's last bar cut short or drawn out by the cycle's leftover, so that the riff ends its
  cycle with the section and the next starts on it, should listening ask.
- **Breakdowns beyond Djent, the rest** (P3, Punk's done 11 October 2026, 20% of its songs, heard
  and kept, `punk-breakdown`): more of Nu metal's, and a breakdown in a verse or before the last chorus beside the bridge,
  should listening want more.
- **Ghost notes beyond the backbeat** (P3): Funk, Soul, Fusion, Blues, Hip hop, Rock, Disco, Drum and bass, Reggae and
  Ska ghost on a backbeat's snare (`Ghosts`), Drum and bass fewer (`ghostNotes`, heard and kept as *dnb-few-ghosts*); a jazz drummer's comping on the snare and Latin's
  timbales are idioms of their own, a snare off the backbeat role and a kit without one, which ghosts do not reach.
- **The energy's recurrence** counts a section's appearances, not its plays: should long sections sound too quiet.
- **The chords and the bass,** which follow the energy weakly, should choruses not sound fuller; the melody sings a
  section's energy over the song's in its range's place (`MelodyLayers.EnergyRegister`), the chorus 2.6 semitones over
  the verse, higher in 61% of the songs.
- **The band's devices, the rest** (P3, each an edit after assembly among the stages, as stop time and pushes are):
  stop time's hits added where a part rests on the downbeat; a backing figure that recurs through the song.
- **Call and response, the rest** (P3, `call-response` heard and kept 11 October 2026, 5 of 7): the answer recurring as its bar
  patterns do by the scheme's letters, with the recurring backing figure. Built: a verse's melody singing each phrase's
  first half and the riff answering in the second, or the counter-melody's own line where no riff plays, a phrase none
  answers left whole (heard 11 October 2026: a cut melody with nothing answering it); the parts doubling a part cut
  with it (`CallAndResponse`, after `Doubling`).
- **Harmony's devices, the rest** (P3): a ii–V into a chord but the home, which a chord's own scale makes part of; the
  line cliché rising (the fifth, its sharp, the sixth) beside its falling one (`InnerLines`), should
  listening ask.
- **A section's parts chosen as its drum kit is** (`DrumKitGenerator.SelectKit`), leading and colouring roles, where
  now each part rests on its own (`Arrangement`).
- **The drums' changes by an appearance's energy** (their colour, a drum's stroke), which are the section's as it was
  made.

### Fills and lines

- **The melody's motif developed, the rest** (P3): the inversion cut in the review of 11 October 2026, heard on 8 October
  as hard to notice and worse in 5 of 6 Pop songs; a figure displaced by an 8th or its rhythm stretched, development a
  listener can follow, to try in its place and be heard.
- **Two toms at once** (P3, put off 10 October 2026, again with the cascade): a drum note's sound a list of
  articulations (`StateKinds.ArticulationIndex` now one, `TimelineEdits.Hit` replacing a hit at its place, the toms one
  track), so that a fill's wide window or a cascade's accent strikes a far pair of toms together, a rack and the floor;
  the cascade built without it, should listening ask for the weight.
- **Filling the melody's gaps before a chord change within a phrase,** should they sound empty now that the solo part
  answers.
- **The lift beyond the cues:** the chords' rhythm doubling, a part of the next section coming in early, should the
  crescendo and the drums' cues not lift.

### Instruments and expression

- **A song's drum stroke by its energy** (P3, by listening, October 2026): a genre leaning the open hi-hat as a stroke
  washes its loud sections more than its quiet ones only where a section draws the stroke anew; the song's own stroke is
  drawn with no energy lean, so Rock's quiet sections ride the open hi-hat in 11% of them against 17% of its loud ones
  (Punk's 5% against 25%). Should a quiet verse's wash be heard, the song's stroke leaned as a section's is.
- **A held riff restruck outside a gate** (P3, by listening): a decaying chord is struck again on its bar lines only
  where the gate holds it (`GatedChords.Hold`); a rock band's riff held over a bar restruck the same way, should a held
  guitar chord dying out be heard elsewhere.
- **A band leaning into a big entrance** (P3, deferred from the dive, October 2026; the dive itself heard as a launch):
  the tempo easing a little over the last bar before a louder section now and then, a ritardando back to tempo on its
  downbeat, where a riff dives into it. The tempo is decided before the notes (`SectionTempo`) and the dive after them
  (`Expression`), so the dive's section would be decided with the tempo.
- **The playing following the articulation** (P3; palm-muted chugs done): octaves popped in slap, short notes muted,
  without which an articulation is only another sound.
- **Plain doubling,** a part doubling another in unison, now that the parts play by their pocket (`Pocket`).
- **A motif stretched,** its rhythm doubled, beside moved and inverted (`Soloist.MotifInversion`), should listening ask.
- **A solo's runs in fives and sevens,** a fusion player's grouping, which the feels give a whole part but perhaps not
  a run; if not, a case of segments in any pattern (see *Parked*), not a mechanism of its own.
- **Legato slides** (P3, a case of legato): a slide strikes its note anew, where a player's note before goes on
  sounding into the next; holding the note before into the slide and leaving the next unstruck edits two notes at
  render. Built if the struck slide sounds like a stutter.
- **A filter drawn over time** (P3, if a wobble's restart on every note is heard): a part's brightness drawn through
  its rests rather than while its notes sound, and the filter's shapes a vocabulary as data anchored where they mean
  something; the part's one curve now adds its tone and a filter's closing note by note (`ExpressionRender`).
- **Drum solos, the rest** (P3, by listening; the plan of October 2026 built, `drum-solo` heard better): a samba's or
  a baião's kick and a timbales player's cascara as its feet; its flams; a motif augmented or diminished; a timbales'
  abanico, the quote of the groove ghosted in Funk, and an IDM's drill of ratchets, each a gesture of its own.
- **A drum's pocket by its section's role** (P3, if a section's change of role is heard): the timing takes a drum's
  offset by its role in the song's groove (`Pocket.OffsetOf`), not a section's, which may move a ride to the time.

### Scales and chords

- **Free chords, the rest** (P3): a chord moved by semitones (`StateKinds.ChordShift`), planing (`Planing`) and a
  chord's own scale on its root (`ChordScaleKind.Own`) are built. Left: planing choosing the next chord by the voice
  leader's least move among free shapes, should it sound mechanical. Its uses past these: a fusion genre, a long-lined guitar player's, its harmony and
  lines as a genre's data (free chords, melodic minor, octatonic and whole-tone line scales, leaping, stretched
  voicings, swells, odd meters); not its tone, which General MIDI has not, its long legato soloing, which the solo's
  gestures (licks, runs, held notes) are not built for, its drumming and its melodic bass, gaps of their own.
- **A section holding its spacing** rather than the voice leader drawing it anew a chord, should a spread chord's
  (`ChordVoicing`, "Spread") neighbours sound close; first whether the spread earns its place, heard in 0% to 2.6% of
  the wildest songs' chords, in no song to one of 64, as the instrument and register pull it back (#222).
- **Approach chords, the rest** (P3): turnarounds, passing chords, a chord solo's planed approaches, chromatic passing
  chords from above and a later appearance reharmonised are built. A plain song is never reharmonised, by any of them,
  the songs' chance 0 at the plain end; a preset's may be above it, as Soul's and Bossa nova's turnarounds. Passing chords of a genre's kinds are built too (`PassingChords.Kinds`): the
  tritone substitute, a half-diminished chord a semitone below, the next a semitone above or below. Left: a ii–V into a
  chord but the home, two chords passing where one does now; a chord solo's approaches on the strong beats at the wild
  end, and the dominant as its approach; the tritone substitute for a progression's own V7 in a reharmonised
  appearance. Today a chord solo stacks the band's chord or planes its shape, never another chord.
- **A melody in block harmony, the rest** (P3; `block` heard and kept, 9 October 2026): a block is the counter-melody
  stacking its chord's notes under its harmony note (`LineDoubling.BlockChance`, Soul's and Funk's 60%, Ska's and
  Latin's 50% of their harmonised sections), its passing notes alone, three voices on 56% of the notes struck with the
  melody, the melody on top 86%. Left: its passing notes harmonised too, a diminished chord or the scale's in parallel,
  should the two-voice passing notes sound thin; Jazz's soli, four voices close, which no part is free to play; the pad
  as a voice, which plays chords, not lines, and sat over the melody 81% of the time when tried.
- **Roots off the scale, the rest** (P3; `own-modes` heard and kept, 9 October 2026): a planed section's chords each in a mode
  of its own, major or minor as it makes them (`Planing.ModesChance`, `ChordScales.Planed`), the modes weighed by the
  progression facet and a genre's pool (`ChordScales.OwnModes`, `ownMode`: Fusion's Lydian, Dorian and melodic minor 82%,
  Phrygian 3%), Fusion's 72% of its planed sections, a chord's mode changing on 78% of the moves. The melody over
  planed chords is placed by where it aims, not moved with them: across a change it moves as the root does 24% of the
  time, against 14% elsewhere, and keeps its note 8% against 11% (`PlaningTest.ReportTheMelodyAcrossPlanedChanges`,
  Fusion's and Jazz's 128 songs). A mode chosen to keep a note of the chord before was tried and let go of (9 October
  2026): 38% of the planed moves keep one as drawn, a lean of odds 2 made it 42%, of 4 45%, and always the mode keeping
  the most 50%, as a move of a semitone or a whole step keeps no note whatever the qualities, the moves deciding it
  (`Planing.Moves`), which favour small steps so that the voices slide. Should the modes be heard as aimless, the move
  is the lever, a planed chord's move drawn with its mode, or a chord keeping the mode of the one before now and then;
  symmetric cycles (major or minor thirds) left out, Planing's moves holding minor thirds already.

### Tooling and tests

- **Plainer prose:** comments that say in a plain sentence what the code cannot, and commits' lines of about a hundred
  characters; CLAUDE.md's paragraph on conventionality as rules a test can check.
- **The server's figures read in full** (P4): the dashboard reads every song made in its window, nothing at hundreds a
  day and slow at the 200,000 a day the table keeps at most; then the spreads would be worked out a day at a time.
- **Tests that check less than their names** (P4, from the review of 9 October 2026; a breakdown's riff chugging and its
  kick on the riff's notes done 11 October 2026, and the solo's approach off the beat let go of, its player not told by its
  notes): the louder and quieter forms per appearance, both ways (with *A section's form*); each passing kind's shape
  (the tritone substitute's dominant, the half-diminished's whole step); the melody's joins at every pattern; the
  busyness reports' facets read from the trace; the MIDI file parsed for its program changes; the stages' hand-offs,
  each read after it is made.
- **A song's tempo below 60 on the page** (P4, #411): a genre's range reaches 40, but a song's tempo is reported and
  given by the request's scale, 60 to 240 in 62 steps, so a song drawn at 40 shows 60 and given its tempo is made again
  at 60; the scale's low end moved to 40 changes what every link's tempo step means, a decision of the links' first.
- **The page's song as it was made** (P4, #328): the events, a rating, the link and a download's name read the settings
  the song on the page was made with, kept as it arrives and moved by a change of its mix alone, never by a change that
  remakes it before the remake arrives, or for good where it fails.
- **The fine-tune window's drag** (P4, #335): a range's band drawn again whole at every pointer move, a canvas rasterised
  and encoded each time; profiled on a phone first, then its edges alone while dragging and the shading on letting go.
- **Helpers the review of 16 October 2026 left** (P4): the chord span around a place, its start and its end, one
  helper over the common map's changes by binary search, each caller keeping its own clamp, in place of sixteen reads
  and seven spellings of the end (#111); a song's tracks and notes by a part, `Song.Tracks(role)` and `NotesOf(role)`,
  for the descriptions, the harmony and some 25 tests (#188); the articulation walk, which only the drums read, built for
  them alone, when the songs are redrawn for another reason, as it changes every song's draws (#100).
- **The page's tests of sound** (P4): the page tests drive asking for songs, not hearing them; playing, seeking, the
  MP3 export and the lock screen's controls are tried by hand, as are Safari and phones.
- **A report of thousands of songs** would make, measure and let go of its seeds past the shared corpus rather than
  cache them; a corpus song keeps about 3 MB, every note's state the most of it, and its making allocates 47 MB
  more for a moment.
- **Tests the review of 16 October 2026 left** (P4): a dyadic pattern's variation over a cycle of another shape, 3+3+2
  or 7/8, repeating no draw made for another place (#237); the improvisation measured on the same songs with it forced
  off, not against the songs that drew none (#276); a part switched to another instrument sitting in its octaves,
  after `RegisterReportTest` says whether any falls outside (#281); the mix filter's opening read as `MixFilter` reads
  it, beyond Trance's four-bar phrases and intros of entries (#289); the scale rule walking its timelines in step, not
  quadratically (#297); a silent start recognised by its trace, its holder exempt alone (#304); the harmony's N.C.
  wherever no pitched part sounds mid-song, and a named chord's root the chords part's (#318).

## Genres

A genre is the page's data (`Genre`, `GenreChoices`, `Client/genres.json`), every option three amounts, at the plain
end, the middle and the wild end; what each choice does is in the code and the numbers of each in its commit. What is
left:

- **Any genre** (P3, a plan in steps): a song asked for with no genre drawing a preset, or the songs' own as one of its
  outcomes, evenly, so that the plain band stays among them. Done: the presets the server's (`GenrePresetFile`, built
  in), the tests' and the CLI's (`--genre <id>`, 11 October 2026); the page's Any genre draws a preset other than the
  song's, and every event carries the genre, which the dashboard rates by. Put off (11 October 2026): the server
  drawing it from the seed, the page asking for any, as a draw by the seed cannot keep the page's rule of another
  preset than the last, and a link already names the preset drawn; taken up should the API or the CLI want any. Left:
  the default (the songs' own, as now, or any genre) decided by the ratings by genre once a version has enough, and
  not before the retuned presets, Pop and Metal most, are heard. Not weighed by the presets nor leaned by
  conventionality: each preset spans the plainest songs to the wildest.
- **Rock and metal as their bands play them** (P3, from 8 October 2026): what each lacks against the sound of a riff-driven
  hard rock band, a blues-rock band's shuffle, a twin-guitar heavy metal band, thrash and groove metal
  (`RockAndMetalReportTest`). Their solos (73% and 78% of songs) and their riffs of two and four bars (AAAB and ABAB a
  third of their sections) were there already; the organ's solo, Rock's shuffle at its plain end and Metal's clean
  quiet sections are presets now. Left:
  - The riff between the sections (P3): one part alone is a texture already (`TextureKind.Alone`), the riff a genre's
    weight of it, but not placed as an interlude of its own.
  - A melody sung rather than played (P3, `pop-sung-lines` heard and kept 4-0): no singer in General MIDI, so the melody
    stands for a voice by how it phrases, not by its sound. Measured (`SungMelodyReportTest`, 128 songs): its moves
    already sing (repeated 28%, steps 33%, thirds 19%, over a sixth 3%, legato 96%); every phrase of a pattern ends in a
    rest, and a sung section's at half its phrase too (`GenreChoices.Sung`, `PhraseRestTest`), a run outlasting two bars
    18% of the time; its rhythm does not: 20% of bars carry eight notes or more, 29% of its onsets a 16th or less apart,
    68% of phrases start on a downbeat. Done (14 October 2026), step 3: a sung section's melody no finer than 8ths
    (`sung.eighths`, the songs' 100-50-0, the plain sung genres' 100-80-60, Pop's, Soul's and Funk's 80-50-30,
    Hip-hop's, Trap's and the instrumental leads' 20-10-0, by the calm sections' cap, `CompositionStateKinds.ShortestNote`),
    a 16th or less between onsets 29% → 14% the songs', Pop's 30% → 17%, Folk's 25% → 7%, Blues' 22% → 8%, Hip-hop's
    14% kept; asked: `pop-eighths`.
    Cut on review: the busyness leaned down (the cap does it, eight notes a bar at most), the repeat chance up (28%
    already), a phrase starting after the downbeat (73% of Pop's start on it, against no measure of a sung line; by
    listening, should the cap's lines sound square). Not: lyrics or syllables, melisma,
    a singer's vibrato or scoops by pitch bend, Voice Oohs as a lead by default (a pad's slow attack; a weight only by
    listening). A genre's instrumental lines (Techno's and IDM's leads, a riff) keep the choice low, so it is a lean,
    never a rule over every melody.
- **Black metal and IDM, the rest** (October 2026, `NewGenresReportTest`): Black metal tremolo-picks 51% of its
  sections, blasts 20%, double-kicks 20%; IDM, warm and swung at its plainest, an acid line, odd meters (47% of its
  wildest songs), polymeters and rolls at its wildest, breaks 24% of its sections. Left:
  - Rolls splintering into 32nds, a breakbeat chopped and drilled, figures finer than a 16th (P3, by listening): the
    figures' grid in 32nds, should IDM's wildest want more than its hi-hat rolls and fills give.
  - Field recordings, a prepared piano, glitch edits (*Parked*, beyond General MIDI).
- **The hook and the phrase scheme as one** (P3): a hooked section a scheme sung literally, should a trial keep the
  report's repeats (50% of a hooked section's notes at their first bar's places); otherwise kept apart, with the reason.
- **The choices grouped into families** (P4): bass, harmony, form, drum devices, one choice of several chances rather
  than fifteen of one, which would read better in the editor; should the editor grow hard to use.
- **What the genres cannot play yet** (P3, each by what listening to the presets says; `GenreDevicesReportTest`,
  `GenreIdiomsReportTest` the measures):
  - The loop's figures shared, the bass's and the riff's, beside its harmony (`GenreChoices.Loop`).
  - A riser as a lift cue, into a louder section where the band plays on, the riser's own channel there now
    (`Risers`), should a lift want one.
  - The pump on the kick wherever it falls, should a pumping genre's kick leave the beat; it ducks on the beat now.
  - The pinned drums (`GenreChoices.Pins`) in the bars a drop or an entry clears, and the wilder songs that leave a
    role free.
  - The montuno's own rhythm, a two-bar figure anticipating the chord, should the part's syncopated rhythm under
    `ArpeggioPattern.Montuno` (root on the beats, the others off them, Latin's, heard and kept as *montuno*) sound loose.
  - More figures (P3, by listening): a figure for the hi-hat or the snare, a figure of more than
    two bars; the double kick following the riff's accents alone, should every 16th sound a machine.
  - A hook of one or two bars sung through a whole section, its phrase ends let go of; a hooked melody's bars repeat
    about half at most now, a phrase's last bar ending on its held note.
  - A fill's drums leaning to the toms by genre, which the drum groups' state draws, not a genre's choice.
  - A melody's leaping by genre, a lean of its register's freedom and its leap's size: leaning its stepwiseness moved
    the mean move by a tenth of a semitone and the leaps not at all, a melody's leaps coming of a phrase starting
    afresh in another register, its echoes and its landings.
  - A part resting by the section's role (a dub's breakdown), with *A genre's sections differing*.
  - A genre's lean of the phrases' place rules, should it press them, and the songs' own sections of eight bars now
    and then.
  - Articulations played by a plugin's output as a keyswitch or a controller, the one place in `Render` the seam, once
    an output differs from General MIDI.
  - Beyond General MIDI, parked: the gated reverb (one reverb send for the whole drum channel, no gate); an acid
    line's slides and accents past the filter's squelch (`StrongEffect.Squelch`), which has no 303.
- **The rhythm's values against the part's own:** a value moves a setting by steps from where the part rests, so that
  one part's "faster" is another's "as fast as can be"; a value set outright, where it lands, would read the same for
  every part, should listening ask for it.
- **A link replacing the settings kept** (a decision): opening a shared song makes its settings this browser's own,
  so that the next song goes on in them; keeping one's own and offering a way back to them is the other way, should
  listening to shared songs ask for it.
- **Instruments by listening:** the timpani as Dungeon's and Orchestral's bass, an organ's bass under Jazz and Soul,
  the church organ on the dark genres' pads, a music box and a kalimba on IDM's riff, Chiptune's square and saw on
  every part, General MIDI's triangle missing (its bass a bass-and-lead or a square).

## Parked

Ideas with a reason, not planned: none has shown a need. Taken up only when listening or another item asks for one.

- **The twin lead's share and busyness:** a twin leading half a twin song's sections (`doubling.twin` 50), its riff
  barely busier than elsewhere (7.1 notes a bar to 6.7, `LineDoubling.TwinLeadFullness` 0.3); heard good as it is (13
  October 2026), tuned only if listening asks.
- **Segments in any pattern:** bars that mix feels, such as three straight beats and a quintuplet beat; a fill's speed
  change, a rank more for one half of its span, would be a case of it.
- **A drum's sounds together:** a drum is one track, whose note plays one sound, so a run never plays two toms
  together; notes of several sounds on one track would allow it.
- **Patterns by measured features** (syncopation, evenness) as a family, should a target prove out of reach of the
  dyadic engine; euclidean patterns that fit no cycle; a library of idioms such as clave and bossa, which genres may
  take up; and drums generated together, the snare avoiding the kick and the hi-hat filling the gaps.
- **An endless song:** sections generated afresh as it plays, with no ending. The sections generated per appearance
  are a step towards it; the form's plan, its intro and ending, and the song's single pass are not. The time is no
  obstacle: an hour's song asked of the generator is made in one to two seconds, but plays 33 to 45 minutes at 120
  bpm, its form's parts running out (`SongLength`'s free form of up to 32), and the page's length stops at fifteen
  minutes (`LengthScale`); an hour and more is the endless song's, not the length's.
- **A tag** (put off twice, 8 and 10 October 2026): half a pattern before a section changes, which breaks the pattern's grid for the fills' lines and
  the phrases; with the meter as state, an engine's change for a rare idiom.
- **A parallel CLI batch** (P4): `--count` making its songs in parallel; a song takes about a second.
- **The rare devices pruned** (P4, the review of October 2026): a device that plays in under about 1% of songs, with
  and without genres, and was never heard as good, taken out or folded into another, the options at
  `ByConvention(0, 0, 0)` that genres alone reach first; should the stages grow too many to keep.
- **The wild end's one device** (P4, the review of October 2026): a wild song stacks key and tempo changes,
  polymeters, every gentle effect and a strong one; one or two devices a song as its signature, leaned by its facets,
  should the ratings fall at the wild end (the ratings by conventionality).

## Decided, and kept

- **Analytics written off the request** (decided 11 October 2026): an event is written in 0.042 ms
  (`EventStoreTest.Report_AnEventsWrite`), so a burst of ten thousand holds the lock under half a second; a queue's
  writer would answer no sooner that anyone could tell. The admin password's pause is built (`AdminArea.WrongPasswordPause`).
- **A heavy band's time and backbeat** (decided 11 October 2026): every section keeps time and a kit's bars sound both
  98.5% of the corpus's, Black metal's 98.8% (`KitRolesTest`); the snare under driven guitars is 8 to 11 dB down in a
  render's mids, the sound set's, not the MIDI's, so nothing in the song changes for it.
- **A mix written by the page** (decided 11 October 2026): a change of the mix asks the server for the file again for
  the download alone; writing MIDI in JavaScript to save that request would keep two writers of one format.
- **The diminished scale and tremolo picking as their own items** (decided 11 October 2026): the first is a scale of
  eight notes, against *Harmony in seven steps*; the second is Black metal's figure already, 51% of its sections.
- **Harmony in seven steps, lines in any scale** (decided 9 October 2026, after a trial on 8 October and two reviews):
  scales of other sizes as the harmony's, a section's chords built and moved in a pentatonic, whole-tone or octatonic
  scale, will not be built. The genres play as the code does: harmony from a seven-note scale, melodies gapping it,
  rock's, blues', soul's and metal's pentatonic and blues lines over it, jazz's melodic minor, whole-tone and
  diminished lines over chords moving by its degrees; the lines take any scale already (`LineScales`, the pentatonic
  68% of the plain end's lines). Harmony from another scale is another language, not this one with fewer notes: a
  pentatonic's chords are sus and quartal with no dominant, which the shapes play (Quartal triad, Quartal stack,
  So What, Sus2, Sus4); a whole-tone scale's are all augmented, every move alike, a passage's colour rather than a
  section's harmony; an octatonic's are diminished and 7♭9 moving in minor thirds, shapes and planing's moves both.
  And the seven steps are not arithmetic to generalise but the devices themselves: the root motions (down a fifth,
  ii–V), the degrees genres lean to (♭VII, ♭VI), the cadence's raised seventh, applied dominants, borrowed chords and
  the doublings' thirds and sixths mean something only in a scale with a fifth, a seventh and a parallel mode, so that
  counting them in semitones would leave most without a meaning in a pentatonic (from E in C's, a fifth up is not in
  it) and the code with a branch for every scale that is not seven, for harmony no preset plays; the trial failed at
  once at the cadence (`Progressions.GetCadenceWeights`). What it was wanted for is built or planned: the line scales,
  the shapes, planing, a chord's own scale, and roots off the scale (a planed section's own modes, *Roots off the scale, the rest*). Taken up only for a genre
  whose harmony itself is gapped, an East Asian pop or a modal pentatonic folk, as that genre's own progressions beside
  these rather than these resized.

- **Black metal ends both ways** (heard 8 October 2026, 2 better, 3 worse, 1 not told apart): a raw band's modal ending,
  ♭II, ♭VI or ♭VII into i, the most often, and a symphonic band's classical V–i now and then, its last chord V 22%, its
  leading tone raised 5% (`cadenceDegree`, `cadenceRaise`); the songs' chances of the raise and the chord into home as
  they were.
- **Long cycles, decided against** (10 October 2026, after two trials on 9 October): a part's drawn rhythm runs one
  cycle over a pair of bars or more, its second bar going on from its first; a bar whose period is longer plays the
  first half of a two-bar cycle (`Meter.GetCycles`), a sparser bar, which the counter-melody's bar patterns draw 70% of
  the time, the bass's 33% (`TwoBarCyclesTest.ReportSlowPatterns`). Played whole over a pair, it changed 230 of 256
  songs and left the second bars empty, its half a rank weaker (the counter-melody's 17% fewer, the bass's 10%,
  `TwoBarCyclesTest.ReportBarsByPlace`); with its notes held across the cycle, the lines' holes closed but the chords',
  the rhythm part's and the counter-melody's stayed, and every slow part grew sparser, the parts' densities tuned
  against a bar of the cycle. The bar is the engine's unit, of the scheme's letters, the state's layers, the melody's
  question and answer, the drums' presence, the fills' lines and the feels; and what two bars of a part give, a second
  bar that answers the first, the schemes (AABA, ABAB), the varied repeat (A′), the question and answer, the phrases of
  three and of four and two, two-bar figures (the claves) and chords of two bars give already. A cycle across the bar
  would cost the engine's unit and a retuning of every slow part for a refinement no listening has missed. A kick
  answered in its second bar or a crash every two bars, should listening miss one, is a figure's data or an edit after
  assembly, as the lift's cues are.
- **Targets from outside, decided against** (10 October 2026): measures of a corpus of real MIDI songs to tune towards
  (the notes a bar by role, the intervals, the syncopation, the chord changes a bar); the songs are tuned by listening
  and the ratings alone.
- **Decided against** (8 October 2026, the review of P3 and P4), each for little heard at its cost or a sound General
  MIDI has not: the drums one by one (GS drum parameters), a flanger, the percussive family's other sounds, sound effects,
  sound design beyond General MIDI, ping-pong echo, modulation (no soundfont maps it), a sustain pedal (notes are held to
  the next already), long fall-offs, the vibraslap's pickup and the drum groups' pulses fading by their sounds; the hook
  at the end (the last section is the chorus most often), a head with solos (AABA and a genre's solos come near it), a
  genre's sections differing (the dynamics' contrast and the phrases' AAB give most of it), the tempo leaning by the
  energy, a key change's common tone and the fills' loudness as layers; and as refactors with no sound, note keys read
  plainly, the drummer as one player or as state, a stage report of notes moved and the notes' memory.
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
- **Decided in the review of October 2026, not built:** a genre's one rule, which it keeps already, its amounts in
  place of the songs' but a choice drawn in roles, written down on `ByConvention`; a genre resolved once, which the
  request does already (`GenerateSongRequest` checks every choice and option); streams by name (they would move every seed and so lose the
  fingerprint as a refactor's check, for one line saved a stream); a stage's module taking the song's decisions whole
  (it moves the unpacking from `SongStages` into the module, and a module's parameters say what it reads); a stage
  declaring what it reads and writes (`SongDraft`'s fields say who makes them, and CLAUDE.md the two phases). A new
  stage takes `(SongDecisions, SongDraft)`, as fifteen do.
- **No machine learning** (decided 16 October 2026, the owner): no model trained on data and no corpus of other music;
  the generator is rules a person can follow, leaned by data, and another model is proposed as rules.
