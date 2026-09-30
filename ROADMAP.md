# Roadmap

Planned work by priority, the questions listening is to settle, the ideas parked, and the decisions that keep what is
built so.

## Priorities

Every section is a 4-bar pattern in the song's meter, played once, twice or four times, its chords changing every
half bar, bar or two bars; the song's shape still comes before the drums' details, which are paused. Each item is
measured before and after by a report test, by the measure its entry names.

**Now** (P0): done.

**Next** (P1): **conventionality with two ends, by facet** (see *Conventionality*), step by step; the meter is built,
to be listened to (see *Meter*); styles once they are chosen.

Alongside, continuously and never as a gate: **listening** through the checklist (see *Listening*).

**Later** (P2), roughly in this order: the ratings report (see *Listening data*), once the dashboard shows songs
rated enough; styles (see *Styles*), once they are chosen.

**When the need shows** (P3): the melody's contour (see *Melody*); occasional chords (see *Instruments*); harmonised doubling and articulations, after styles; solos; long cycles, then
overlapping polyrhythms, needed someday; timing by role; intros of their own material; scales of other sizes; the
fills' loudness and sounds by convention; note keys.

**Parked** (P4): see *Parked*, kept for their reasons, not planned.

## Listening data

A song's version (`VERSION`, `SongsVersion`: a number such as 0.5.001, which `deploy.sh` bumps and commits when the
songs' fingerprint changes) is stored with every analytics event about a song, and the page rates songs, liked or
not; the dashboard shows the listening and the ratings by version, and the songs of the latest one rated.

- **The ratings report** (P2, once the dashboard shows a few hundred songs rated in one version): an export of a
  version's ratings and time listened (seed, rating, seconds), behind the dashboard's token, and an explicit report
  test that reads it, keeps the seeds of the build's own version, generates them again with the trace and holds the
  ratings against its values (`StateTraceEntry.Value`): which endings, intros, drum setups, modes, tempos, energies or
  improvisations are liked or skipped, with their counts, so that a small sample shows itself. Measured by: ratings
  per version, enough to tell two versions apart.
- **Targets from outside** (P3): a few measures (the notes a bar by role, the intervals, the syncopation, the chord
  changes a bar) of a corpus of real MIDI songs to tune towards.

## Listening

What listening is to settle, each a question and what it decides; continuous, alongside the work, never a gate. A
report test picks the corpus seeds to hear for each from the trace; the seeds named here are ones already found.

- **The sections' energy,** tuned by measurement only (see *Section dynamics*): do quieter sections sound as sections,
  and louder ones lift? The fills land into a quieter section 54% of the time: too often?
- **The melody's question and answer, and the improvised appearances:** then a bar of a later letter within the
  phrase.
- **Varied repeats (A′):** a repeated bar is varied by a flat chance of 0.25 and a fixed strength
  (`PhraseSchemes.VariedRepeatChance`, `VariedRepeatVariation`). Should plain sections sound too varied or wild ones
  too literal, lean its chance alone, gently: which way convention runs is unclear (a varied repeat is as conventional
  in songwriting as a literal loop is in electronic music), and the scheme's choice already leans to more distinct
  bars in wild sections, so leaning A′ the same way would push wild sections to no repetition and plain ones to loops.
  The answer's lean (more mutation the wilder) the same.
- **Where the melody leaps:** a phrase or a section starts afresh at its aim, and a pattern's end wraps to its start,
  so the melody leaps there about a third of the time, mostly after a rest: a new phrase, or a break? Two rules tried
  to close a phrase onto its start and were dropped: aiming the last bar back moved little, and landing the last note
  near the start only moved the leap one note earlier.
- **The gaps before chord changes:** half the changes have no melody note in their last beat, a rest or a held note.
  Empty? Then the pitched fills (see *Fills*) and a counter-melody (see *Appearances*) move there.
- **The section modes:** does the cadence into a change prepare it? The parallel major or minor and the harmonic minor
  sound clearly (corpus seeds 196, 10, 36 and 171), the one-note modes more faintly.
- **The velocity:** how even the bass and the chords play, tuned by measurement only; the bar layers (a bar's and a
  bar pattern's loudness) add 3 to 5 from bar to bar, and go should bars sound to jump.
- **The drums:** the percussion songs; the wild end, where the kick or the snare in a role not theirs may sound broken
  rather than bold (the snares keep time in 2 to 4% of their sections, a train beat); bound drums, too rare to hear at
  a doubling chance of 0.15?; the clap's ghost notes, soft hits around the backbeat, built only if missed.
- **The bass's pickup** always falls on the beat: the 8th before the bar line, leaned by convention, if it sounds
  square.
- **The fills:** wild songs play about 1.8 times the tuplet notes of plain ones, as a run plays the snare's feel only:
  too straight? Then read the tuplet the drums play most. Three runs in ten play quarters or slower, as a run is two
  ranks finer than the snare's sparse groove: weak? Then aim a run's finest rank at the finest the tempo allows, a
  step or two less, keeping only the groove's cycle and phase.
- **Repeated cycles** replay their beats' accents: mechanical? Then a small fresh loudness draw over the replayed
  accent.
- **Swing and panning,** set by measurement only: is a triplet's swing too heavy at 16ths, and do the tuplets and
  grouped cycles of wild songs, swung with the rest, sound played or broken? Are the chords (a half to all of 0.6 to a
  side) too far out, or the melody (up to 0.15) too near the middle?
- **Harmonic rhythm,** set by measurement only: do sections of a chord every half bar sound busy or rushed, and those
  of a chord every two bars static? Does the silence where a clipped note leaves a change unstruck sound like a gap?
- **The forms:** do the songs of a form sound like verses and choruses, the chorus lifting? A song is shorter, 80 bars
  of sections at the median against 116: too short?
- **The rests:** do the breakdowns and the sections without a melody sound like an arrangement, or like parts gone
  missing? Is a drumless section before a chorus a lift, as the fill out of it means it to be?
- **The lift:** is a crescendo of about 8 of 127 over the bar before a louder section heard as a lift?
- **Half time and double time:** does double time, 22 drum notes a bar, sound like a lift or a scramble?
- **The fade-outs:** a ritardando into the fade, the drums fading first, or a tag after it, if asked for.

## Conventionality

Planned in September 2026 (see CLAUDE.md's *Conventionality has two ends*): a song's conventionality from 0, the
plainest, to 1, the wildest, a value of it for each facet, and every choice leaning by its facet through its plain,
tuned and wild weights (`ByConvention`), in place of the rhythm's and the harmony's unconventionality leaning through
`Tilt`, which never reaches either end. In this order, each measured against a baseline and committed on its own:

1. **The rule and its primitive** (built): `ByConvention`, a monotone cubic through an option's plain, tuned and wild
   weights, and a chance's.
2. **Baselines** (built, `HarmonyReportTest`): a harmony report (the chords' levels, the home's and the cadence's, the scales, the sections' scale
   changes, the key changes, the pentatonic sections, the progressions' strictness) and the feel's, the backbeat's,
   the grid's, the busyness's and the drums' reports kept as they are.
3. **The facets**, as plumbing (built, `Unconventionality`, with no spread yet): a song's base conventionality, spread as the rhythm's is now, and the feel, the groove,
   the fills, the form, the chords, the progression, the scale and the melody drawn around it, each from a stream of
   its own, the spread vanishing at the ends so that a base of 0 or 1 makes every facet so; a section moves each a
   little. The rhythm's and the harmony's unconventionality stay as wrappers of one facet each until every choice reads
   its own; a test makes songs with every facet at 0, at 1, and each alone at 1.
4. **The chords** (built; over the corpus the chords between home and cadence levels 0 to 5 in 32, 27, 13, 8, 9 and 11%
   against 35, 35, 13, 10, 5 and 1%, the facet centred where the anchor leant plain; the plainest fifth of sections
   90% triads and level 1, the wildest 83% levels 3 to 5): a chord's level a choice by the chords facet, the plain end triads and the colours of level 1, a few
   of level 2, the wild end levels 2 to 5, rising; the home chord and the cadence choices of their own, with plainer
   tuned weights and the same ends; the anchor, peak and skew gone; the tuned weights the corpus's levels.
5. **The progressions' strictness** by the progression facet.
6. **The scales:** the plain end major and natural minor, Mixolydian and Dorian a little; the wild end harmonic minor,
   Phrygian and Lydian, Mixolydian and Dorian a little; the sections' scale changes a chance by the rule, in place of
   one multiplied by hand.
7. **The key change and the pentatonic melody** by the scale facet, in place of the rhythm's: a key change allowed at
   both ends and at every chance at 1, a pentatonic melody plain only.
8. **The feel:** a feel a choice of eleven, straight, the threes, fives, sevens, elevens and thirteens as tuplets and
   as groupings, the lowest layer's winning, in place of a prime index added up; the plain end straight and the
   threes, the wild end every feel but straight; a section's, a track's, a bar's or a fill's change of the song's feel
   never at 0 and every time at 1.
9. **The rest of the rhythm's leans**, a facet at a time: the groove, the fills, the form, the melody; then the wrappers
   gone.

Later, by the same rule: key changes anywhere and at any time, where the form now allows one; the voicings, the
cadence's raised seventh and the chord pool's size; more modes for the wild end (with *Scales of other sizes*); a
song's own coherence, how far its facets stray from its base; a user's conventionality setting, which moves the base.
To tune by listening: the facets' spread, the harmony between its ends, the chords' and the scales' weights.

## Meter

A song's meter (`Meter`) is its bar as groups of 16ths, a tree from the bar through its groups down to the 16ths,
every node splitting by the odd number of its steps first and in two after; a section is 4-bar patterns in every
meter. A rhythm's period and phase are counted in four (`Meter.ReferenceBar`) and the tree plays them: a straight
period on the level nearest as many of the meter's pulses (`Tactus`), a finer level counted half as far again; every
node a cycle of its own descendants; half a cycle late its other parts struck first, so the backbeat falls on 2 and 4,
on 3/4's 2 and 3, on 6/8's fourth 8th and on an odd meter's later groups. Tuplets play over the nodes of their span,
grouped periods from every node that holds two of them. Chords change on group starts, fills are the bar's last
nodes, the bass's pickup and the count-in fall on the pulses, and swing only where the groups hold whole pairs.

Drawn per song, leaned away from convention (`Meter.Options`): 3/4 and 6/8 now and then, odd meters (5/4, 7/8, 5/8,
9/8, 7/4, 11/8, 13/16, 15/16, their groups in a drawn order) rarely; over 400 songs 12% not in four, 5% odd. The drums
play 3.7 notes a beat in four and 5/4, 4.0 to 4.3 in 6/8, 3/4, 7/8 and 15/16, and 4.9 in 13/16, whose pulse is a
dotted 8th (`MeterReportTest`; `MeterSeedsReportTest` names seeds to hear).

- **To listen to:** whether 6/8 drags, its tempo counted in quarters so that 120 pulses at 80 (to draw the tempo by
  the pulse if so), and whether 13/16 is too busy.
- **A bar of two beats before a section, and a section's meter of its own** (P3, planned and put off): both make
  the meter a thing of a place in the song rather than of the song, the song map's bars no longer all alike, and
  every reader of the song's meter (the fills, the edits, the bass's fills, the realizer, the trace's bars, the
  tests' bar arithmetic) asking it at a position; the MIDI file a time signature at every change. Rare in the
  music it plays, against a cost across the song's assembly; to take up when the song map holds its bars.
- **Overlapping polyrhythms** (P3, after *Long cycles*, needed someday): a figure of a length of its own, such as a
  riff of 23 16ths, running on against the bar, as the kick and the guitar do against the hi-hat and the snare in
  Meshuggah, meeting the bar again at a phrase's end; it covers cross-rhythms, a grouped cycle running on across the
  bars.

## Chords

A section's chords change every two bars, every bar or every half bar (`HarmonicRhythm`, 0.25, 0.6 and 0.15, leaning
faster the more energy the section has), its progression of as many chords, the first home, the last the cadence and
the one before it its preparation (`Progressions`); every per-bar decision that meant per chord is per chord change
(`StateKinds.ChordChange`): the bass's and the lines' leading and landing, the bass's pickups, the role chords and the
cadence's raised step. A note of the chords or the bass ends where the chord changes, where 15% of the chords' notes and
more of the bass's sounded the old chord over the new before. Over 200 corpus songs: 27% of the sections change every
two bars, at an energy of -0.26 on average, 52% every bar (0.05) and 21% every half bar (0.31); the bass leads 15, 48
and 62% of its changes by its instrument, off the new chord 1 to 3%, the melody's chord notes on the beat 88% against
90% (`HarmonicRhythmReportTest`, `BassLeadingReportTest`).

- **A note held into a change, struck again** (built): a note of the chords or the bass that would sound across a
  change stops there and is struck again on it, where the track plays on within a bar (`Realizer.RealizeStruck`): the
  bass on the new root in the octave it was in, the chords in the shape of their next note where it is of the same
  chord, voiced from the chord before. The chords strike 87% of the changes they play through and the bass 90%, where
  they struck 77% (`HarmonicRhythmReportTest`); the bass leads into its changes as before (`BassLeadingReportTest`).
- **The span by tempo:** a chord every half bar at 175 BPM lasts 0.7 s, and every two bars at 90 BPM 5.3 s; should
  either sound hurried or static, lean the span by its length in seconds.

## Form

- **Sections with a role, as the form** (built): a song takes one of the forms songs are written in by a chance of
  0.7, less the less conventional its rhythm (`SongForms`: V C V C B C, V P C V P C B C, V C V C B V C, V C V C,
  C V C V B C, V P C V P C), a section for every role, or else a form of its own as before (`SongStructureGenerator`),
  whose sections have no role. A role leans its section's length (`SectionLength.GetRolePlays`, odds of 8: a verse and a
  chorus to sixteen bars, a pre-chorus to four, a bridge to eight) and a bridge's home away from the tonic (odds of 4);
  the energy comes from how often and where a section plays, as before, which puts the chorus, recurring most, on top. A
  drum bound to a lead comes in with it in an intro of entries. Over 200 corpus songs, 134 take a form, the chorus the
  loudest section in 127 of them; the energy of a chorus 0.50 on average, a verse -0.17, a pre-chorus -0.12, a bridge
  -0.24; half the verses and choruses play sixteen bars; a song's sections take 80 bars at the median, 48 to 152 from
  10% to 90%, against 116 before (`SongFormReportTest`). Left: the role leaning the arrangement and the solos (see
  *Appearances*, *Instruments*); an intro and an outro as roles of their own, should the form's intros and endings not
  do; a form with a last chorus played twice over, as a section may not follow itself.
- **A section's length** (built): a section plays its pattern once, twice or four times (`SectionLength`, 0.2, 0.65
  and 0.15), other than twice the likelier the less conventional its rhythm; once plays its lines' question alone, four
  times the question and the answer twice, the second time as a further appearance, improvised as far as the song
  improvises; a fade plays eight bars at least. Over 200 corpus songs, the plainest sections play twice 81% of the
  time, the middle 66% and the wildest 44%; a song's sections take 116 bars at the median, 64 to 180 from 10% to 90%;
  the melody as before (`SectionLengthReportTest`, `MelodyRepetitionTest`). Left: **a tag,** half a pattern before a
  section changes, which breaks the pattern's grid for the fills' lines and the phrases, with the meter as state; and
  the energy's recurrence, which counts a section's appearances, not its plays, should long sections sound too quiet.
- **Intros of their own material** (P3), such as a riff the song does not play otherwise.

## Section dynamics

A section's energy (`SectionEnergy`) leans its loudness, its drums' fullness and density, which drums play, and the
fills into it. Over 200 corpus songs, in plain sections and wild ones, it correlates with loudness 0.51 and 0.27, with
how many drums play 0.46 and 0.32, but with the drums' notes only 0.22 and 0.19.

- **The energy in the pitched tracks** (built): the pitched tracks' section layer leans by the energy as the drums'
  does, and a tilted layer leans its speed too, faster the more energy (`RhythmLayer.CreateSpeedGenerator`), as its
  fullness and density lean; the melody's busyness and its chance to play twice as fast lean by it
  (`MelodyBusyness`). Within a song, over 200 corpus songs (`SectionDynamicsTest`, `PitchedEnergyReportTest`), the
  energy now follows the melody's notes 0.30 (was -0.03), the drums' 0.41 (was 0.27), the bass's 0.11 and the chords'
  0.10 (were 0.07 and -0.05); the drums play 14.2 notes a bar in the plainest sections against 12.6, a faster
  rhythm adding more notes than a slower one takes away. Left: **the melody's register,** which a shift of the
  contour by the energy did not move (0.03 at eight semitones), as the line follows its aim only weakly; it waits for
  the contour (see *Melody*); **the chords and the bass,** which follow weakly, should choruses not sound fuller.

## Appearances

A section is made once and played again, only its melody improvised afresh as it recurs (`GeneratedSection.Appear`).
What changes from one appearance to the next is one mechanism, and the arrangement is its main means, so these are
planned together (P1):

- **The parts that rest** (built): a section leaves out now and then its drums, for a breakdown, its bass, its chords or
  its melody, for a section of the band alone, each the likelier the less energy it has, the melody all but never in a
  verse, a pre-chorus or a chorus (`Arrangement`, 0.08, 0.05, 0.05 and 0.1, odds of 8 against a tune's roles resting);
  where both the bass and the chords would rest, the chords play. No line leads into a section whose drums rest, nor
  marks its phrases, and the fill out of it is the drums coming back. Over 200 corpus songs, the drums rest in 10% of
  the sections, 15% of the quieter half and 4% of the louder, 18% of the bridges and 1% of the choruses; the melody in
  10%, 26% of the bridges; the bass 7%, the chords 6% (`ArrangementReportTest`); within a song the energy follows the
  bass's notes 0.16 and the melody's 0.38 (`SectionDynamicsTest`).

- **A pad** (built): a fourth pitched track (`TrackRole.Pad`, `InstrumentRoles.Pad`: strings, a choir, synth pads, an
  organ, never the chords' own sound), a chord at every change held until the next (`SectionGenerator.GeneratePad`),
  under the chords (`VelocityLayers.GetLevel`) and on the other side from them (`Panning`, the widest tracks taking sides
  first); it rests in a section by a chance of 0.6, less the more energy (`Arrangement`), and comes in in an intro of
  entries as a part of its own. Over 200 corpus songs it plays in 45% of the sections, 63% of the louder half and 28%
  of the quieter, 70% of the choruses and 22% of the bridges, at -12.2 dB against the chords' -8.5
  (`ArrangementReportTest`, `VelocityReportTest`).
- **A counter-melody** (built): a fifth pitched track (`TrackRole.CounterMelody`, `InstrumentRoles.CounterMelody`:
  strings, horns, reeds, a clean guitar, an organ, apart from the melody's and the chords' sounds), a line by the
  melody's rules (`CounterLayers.Line`: stepwise, no phrase shape, going on through the song), half as fast and sparser,
  in the register below the melody, placed over the song with the melody and the bass, resting in a section by a chance
  of 0.75, less the more energy. Over 100 corpus songs it plays in 46% of the sections as they play, with 29% as many
  notes as the melody, 8.2 semitones below it on average, a semitone or a major seventh from the melody note over it
  3.7% of the time. A stop now holds a part's last note only where it still sounds in the bar before, not a note of a
  part that rested since.
- **Pitched roles and the arrangement:** left: a second chord instrument, and a section's parts chosen as its drum kit
  is (`DrumKitGenerator.SelectKit`), leading and colouring roles, where now each part rests on its own
  (`Arrangement`); the page's mixer shows every track the file has.
- **Energy by appearance** (built for the parts): every appearance of a section has an energy of its own, the
  section's and the arc's step for how much later or earlier it plays than the section does on average
  (`SectionEnergy.AppearanceStep`), which a later appearance draws its parts again by, from a sequence of its own, a part
  that played the time before playing on, so that parts only join as a section comes back (`GeneratedSection.Appear`).
  Over 200 corpus songs, a recurring section plays 4.31 of its five parts the first time and 4.78 the last, 230 of 577
  growing; the pad plays in 85% of the choruses as they play (`ArrangementReportTest`). Left: the appearance's energy
  for its loudness and the drums' changes (their colour, a drum's stroke), which are the section's as it was made.
- **Key changes** (built): a song whose last section came back before goes up a key for it by a chance of 0.08, the
  likelier the more conventional its rhythm, a whole step or, by 0.4, a half step (`KeyChange`), as the song's key
  from the section's start, so that the whole band, the ending and a fade move with it and the lines, placed after, go
  on into it. Over 200 corpus songs, 22 go up a key, 13 of them a whole step (`SongFormReportTest`).

## Fills

The fills are the drums', and every track lands with them where a section lands.

- **Pitched fills** (built for the bass): where the drums play a run into a change of section, the bass walks with it
  by a chance of 0.5, the likelier into a louder section (`BassFills`), made once the lines are placed, as the fills'
  runs are kept (`FillGenerator.Runs`): on the run's rhythm, an 8th apart at the most, by the scale's steps from its
  note before to a step from the note the next section starts on, each kept as a line's note, a scale step over the
  chord where it plays and on the scale there; only where the bass plays on both sides of the line. Over 200 corpus
  songs the bass moves into a new section by step 68% of the time, where it did 47%, and leaps 9% against 11%
  (`BassFillReportTest`). Left: the melody or the counter-melody running into a section, and filling the melody's gaps
  before a chord change within a phrase, should they sound empty.
- **The lift** (built for the loudness): into a louder section, the band grows louder over the bar before, a step every
  quarter beat, up to how much more energy the next section has as far as the ending section's rhythm follows it
  (`SongFormGenerator.CreateLifts`, `FormLayers.LiftVelocity` 0.6), the next section then as loud as it plays. Over 200
  corpus songs the bar before a change into a louder section plays 2.9 of 127 louder than the bar before it (was -0.1),
  into a quieter one 1.4 quieter, as before (`LiftReportTest`). Left: the snare from its cross-stick to its head, the
  hi-hat opening, the chords' rhythm doubling, a part of the next section coming in early, should the crescendo alone
  not lift.
- **Loudness** (P3): a run's swell, its accents and a landing's hit are constants, where the groove's loudness is
  layers; a fill's velocity layer would make them cumulative with the section's.
- **Sounds by convention** (P3): runs and landings weigh a drum's sounds the same in a plain section as in a wild one;
  the weights could lean by conventionality, so plain sections crash and wild ones reach for the china and the splash;
  the idioms the draws no longer tie together, a lift on the open hi-hat, come back the same way.

## Instruments

Four pitched tracks, the chords, the melody, the bass and a pad, each on one instrument for the whole song; a section
may leave any of them out (`Arrangement`). Each sits where its role spreads it (`Panning`): the bass in the middle, the melody near it and the
chords out to the other side; a new role takes a spread of its own.

- **Occasional chords** (P3, planned in September 2026 and put off: a note of a line playing two or more notes is a new draw of every note, which moves the whole line even where it plays one, or a draw keyed by the note, and what it adds, a double stop in the melody, a bass chord, risks clashing with the chords for little): how many notes a track sounds at once is its role's (`Realizer`). As a note's state, a
  note, two or the chord, leaned by the beat's accent, the energy and the landings, the melody would play a double stop
  on an accent, a guitar a power chord where a section lands, the bass a chord now and then. Measured by: the notes
  sounding at once by role, on accents and off.
- **Harmonised doubling** (P3, after styles and the arrangement): a line doubled a diatonic third or sixth above, as
  twin guitars play a riff, on a track of its own panned against it: a line's notes are scale steps above the chord's
  root (`LinePlacement`), so the double is two steps up. Per section, leaned by energy. An idiom of some styles: over
  a nylon guitar and a harpsichord it would sound as chance. Plain doubling, the same part twice, waits for timing by
  role (see *Groove*): two copies with the same notes and timing only play louder. Measured by: the sections doubled
  by energy and style, and the double's notes off the chord on the beat.
- **Articulations** (P3, after styles): a finger, pick or slap bass, a muted guitar or its harmonics are General MIDI
  programs of their own. An instrument's articulations as data, picked as a drum's stroke is (`DrumStrokes`: the
  song's, changed by a section and by a bar of a later letter, the lowest layer winning), a program change where it
  changes; the playing should follow, such as octaves popped in slap and short notes muted, or it is only another
  sound, and that is the hard part.
- **Solos** (P3, after sections with a role and the arrangement): an appearance of a section in which the melody rests
  and a solo line improvises over its chords, more (`LinePattern.Mutate`), busier and over a wider range, the other
  parts thinned; a bass solo by the bass's profile loosened. Special solo instruments by a pool of their own. The form
  decides which appearance is a solo, by the section's role.

## Groove

A song swings by a chance of 0.2, leaned by its rhythm's unconventionality (`Groove`): its 16ths where they are long
enough to be heard swung (0.14 s, 105 BPM and slower), its 8ths otherwise, from 30% of a triplet's swing to all of
it, `Render` moving every note by one continuous stretch of each pair, so that no note crosses another. Over 300
corpus songs, 16% of the plainest swing, 17% of the middle and 37% of the wildest (`GrooveTest.Report`).

- **Half time and double time** (built): a section's drums play in half time by a chance of 0.08, the likelier the
  less energy it has, or in double time by 0.04, the likelier the more (`Groove.DrawTimeFeel`), as a step of every
  drum's period in the drum group's section layer, so that the snare's backbeat moves to the bar's third beat or to every
  beat, its phase following its period, and every other drum with it, over the same chords. Over 200 corpus songs, 9% of
  the sections play in half time, at an energy of -0.12, 6.5 drum notes a bar, the snare's beats on the third 46% of
  the time; 9% in double time, at 0.39, 22 a bar (`TimeFeelReportTest`).
- **Timing by role** (P3): the backbeat a little late, the hi-hat on top, should swing alone sound stiff; plain
  doubling needs it.

## Scales

Every scale has seven notes, and the progressions' rules are in its steps (`Progressions`, `StepCount`).

- **A pentatonic melody** (built): a section's melody is pentatonic by a chance of 0.35, the likelier the more
  conventional its rhythm (`MelodyLayers.PentatonicChance`, `CompositionStateKinds.LinePentatonic`): the line leaves out
  its scale's tritone pair, the two notes of its only tritone, F and B in C major and A minor alike, which in any
  diatonic mode leaves its pentatonic scale; a weak beat moves among the notes it takes, a strong one among the chord's
  as it would and then off the pair to the nearest other, and an echo on the pair moves to the nearest note it takes
  (`Line`); a scale with more than one tritone keeps all its notes. Over 200 corpus songs, 37% of the sections are
  pentatonic, 4.1% of their melody's notes on the pair against 23.1% in the others; they leap 9.1% of the time against
  6.7%, a pentatonic line moving by thirds where another steps (`PentatonicReportTest`).
- **Scales of other sizes** (P3): whole scales of more or fewer than seven notes, where the progressions' rules would
  need their steps as fractions of the octave. Then jitter on in-between heights: heights between two qualities, such
  as the third, move a little from chord to chord, so that a scale of more than seven notes picks sometimes one
  quality and sometimes the other.

## Styles

Put off in September 2026 for a decision: which styles, and what each leans towards, is taste, and every lean needs
data on the options it leans (an instrument's styles, a drum role's, a form's); made without it, a style would be a
caricature. Wanted first: a short list of styles, and for each a sentence of what it sounds like.

Genre is the answer deferred more than once: the tresillo keeping time is conventional in reggaeton, dancehall or
afrobeats; twin guitars, slap bass and the calls belong to some styles and not others. A style (P2) is a named bundle
of leans on the draws the generator already has (`Tilt` by style: the drums' roles and figures, the instruments'
pools, the scales, the swing, the harmonic rhythm, the meters, the forms), drawn per song, never a separate generator
and never deciding a draw. Leaning towards none of them is what the songs are now. Designed before the style-coded
items (harmonised doubling, articulations) are built. Measured by: per style, the measures its leans are meant to
move (the swing, the chord changes a bar, the drums' notes off the 8ths) against the unstyled songs, each moved and
the rest as they were.

## Rhythm engine

- **Long cycles** (P3, needed someday): a bar pattern is one bar long, so a slower cycle, such as the kick's slowed to
  two bars, plays its first half and starts again at every bar line. Patterns as long as their cycle would let slow
  figures run whole, such as a crash every two bars or a kick figure answered in the second bar. Overlapping
  polyrhythms and a chord of two bars need them.

## Melody

- **The contour shapes the melody weakly** (P3, tried in September 2026): a bar's mean pitch follows the register it
  aims at about 0.2 (`MelodyContourTest`), as the echoes replay a note's step whatever the aim. Moving an echo run by
  as many scale steps as its phrase now aims away from where it aimed when the note was heard, a figure repeated at
  another place in the phrase as a sequence, was tried and dropped: the melody followed its aim 0.15 against 0.19, and
  a recurring section kept its first appearance's notes 87% of the time against 94%, as the echoes' notes are heard
  first anywhere in the song. The wave (`MelodyLayers.Periods`) and the lean towards the aim (`MelodyLayers.AimOdds`)
  stay as they are; should the melody's shapes sound flat, the aim wants a way into the line other than the echoes.

## Architecture

- **Memory** (P4, measured in September 2026 with five pitched tracks): every `RealizedNote` keeps the state it was
  decided from, which a song holds on to: 1.7 MB a song, generated in 86 ms, so nothing to do.
- **The tests' speed** (measured in September 2026): a song is made in 119 ms alone; the corpus makes its songs in
  parallel (`TestCorpus.Range`, `Measure`, `InParallel`), traced for their entries but not explained (`StateTrace`'s
  `explains`), with server GC and no tiered PGO, so that 200 songs take 3.3 s and the full run 15 s, against 142 s and
  169 s; a report's own measuring is negligible beside the songs'. A corpus song holds about 5 MB, the full run 1.6 GB
  at most. Should a report want thousands of songs, its seeds past the shared corpus would be made, measured and let
  go rather than cached.
- **Note keys** (P3) are hashed seeds where a plain key of the bar pattern, the cycle and the place would do.

## Parked

Ideas with a reason, not planned: none has shown a need. Taken up only when listening or another item asks for one.

- **Segments in any pattern:** bars that mix feels, such as three straight beats and a quintuplet beat; a fill's speed
  change, a rank more for one half of its span, would be a case of it.
- **The drummer as state:** the favourite walk (one way, turn, loop and random, which could be one walk with a few
  values) as a pool like the chord pool's, and busyness as a fullness and a span length that the energy adds to.
- **A drum's sounds together:** a drum is one track, whose note plays one sound, so a run never plays two toms
  together; notes of several sounds on one track would allow it.
- **The vibraslap's pickup,** the last beat before a landing.
- **Patterns by measured features** (syncopation, evenness) as a family, should a target prove out of reach of the
  dyadic engine; euclidean patterns that fit no cycle; a library of idioms such as clave and bossa, which styles may
  take up; and drums generated together, the snare avoiding the kick and the hi-hat filling the gaps.
- **An endless song:** sections generated afresh as it plays, with no ending. The sections generated per appearance
  are a step towards it; the form's plan, its intro and ending, and the song's single pass are not.

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
- **A drum keeping time plays grouped cycles (a tresillo) unleaned,** 7% of the plainest sections' bar patterns to 23%
  of the wildest, as the other drums do: its convention is a style's to say (see *Styles*).
- **Seeds are not stable across versions;** the version labels what was heard (see *Listening data*).
