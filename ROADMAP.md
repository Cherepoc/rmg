# Roadmap

Planned work that has been decided but not built yet.

## Next

In this order, each measured before it is planned:

1. **Listen and tune.** The runs, rebuilt on the groove (whether they sound part of it now, whether the drums they
   draw, their walks and windows sound interesting or broken, how often they play drums other than the snare and the
   toms, about one run in five), intros (whether the band's entry after a chords-first or build-up intro feels earned)
   have been tuned by measurement only, as has the sections' energy (see *Section dynamics*). Heard and kept: the
   endings, the section modes and the chords' level.
2. **The melody** (see *Melody at generation*): it plays where it was placed, so a phrase that plays again is heard
   as the same notes, and its shape follows its contour weakly.
3. **The drums** (see *Drums*): grooves for the percussion, and sections of percussion only; listen to the drums
   coming and going by bar.
4. Smaller: fade-out endings (see *Form*).

## Section dynamics

Every section has an energy (`SectionEnergy`), an additive state of the song's and three steps (how often the section
recurs, where it plays on average, and its own). Its pull, the energy times the section's coupling, tilts the draws
(`Tilt`): the section's loudness, the drums' fullness and density in the drum group's and each drum's section layer,
which drums play (each drum's `Loudness`), and the fills by the energy they lead into. With no tilt the songs are the
same as without it.

Measured over 200 corpus songs, at `HighOdds` 32, in plain sections and wild ones: energy correlates with loudness 0.51
and 0.27, with how many drums play 0.46 and 0.32, but with the drums' notes only 0.22 and 0.19; loudness, the drums'
notes and how many drums play correlate with each other 0.2 to 0.4, where the plan aimed at 0.4 to 0.6. Fills into a
louder section average 2.5 beats and almost always land, into a quieter one 0.85 beats, none in half of them. Left:

- **The drums' notes** follow the energy weakly: the section layers' density and fullness move little, so even with
  energy near deciding (odds of 100,000) the drums' notes correlate with it only 0.48. The section's shared rhythm
  layer, or the drums' speed, would move them more, the first also moving the pitched tracks.
- **The melody's busyness and the chords' rhythm,** by the same pull.
- **Energy by appearance,** so that the last chorus plays louder than the first: the parts that change would be edits
  of the song as it is put together, as the fills are.

## Section modes

A section on the relative key already plays: a home on the relative step (a fifth of the sections) builds its chords
on the same notes. A section may also play in another scale on the song's tonic, a parallel mode
(`Scales.PickSection`): `ScaleOffsets` is each section's state, which its home, progression and cadence draw in, and the
intro and the ending take from the first and the last section. The first section keeps the song's scale, which sets the
key; another changes one time in ten, more the less conventional its harmony, to a scale weighed by its weight and how
few notes it changes, leaning brighter (by the sum of its offsets) the more energy the section has. Over 100 corpus
songs, 15% of the sections after the first change, three in four to a scale one note away, a tenth to the parallel
major or minor; those that turn brighter have an energy of 0.33 on average, those that turn darker -0.17. Left:

- **Heard:** the changes sound, the parallel major or minor and the harmonic minor clearly (corpus seeds 196, 10, 36
  and 171), the one-note modes more faintly. Left to listen to: whether the cadence into a change prepares it.
- **Key changes,** such as a last chorus a step up: a section's key as its own state, as its scale is now.

## Melody at generation

The melody is placed where the other tracks' notes are decided, not in `Realizer`: once a section's bars are made, one
line places its notes in order over them, by the rules of `MelodyLine`, over the chords `Realizer` would work out
(`MelodyPattern.Place`), and every note keeps its scale step above its chord's root (`StateKinds.ScaleStep`), which
`Realizer` plays. The section then plays it the same wherever it plays: its second phrase and a section that recurs are
the same notes, where before, placed as the song played, a bar that came back played the same note over the same root
40% of the time. The melody's render flags are generation state now.

The melody plays where it was placed. `Realizer` used to choose every bar's octave from the note before, which varied
a phrase that played again by its octave, as a side effect; it no longer does, and what plays again plays the very same
notes. The song's last note is the chord's root in the register of the note it is made from, an edit as the song is
put together (`SongFormGenerator.LandOnRoot`).

Measured over 200 corpus songs, against the melody placed as the song played: chord notes on the beat 87%, up from 81%;
leaps 3.4% of the moves, against 2.2%, where a phrase starts again 14% (88% of them after a rest, as a new phrase
starts after a breath) and into a section 14%, against 3% before; the song's last note leaps from the note before 18%
of the time, as before. The octave chosen per phrase left 24% of the leaps where a phrase starts again, and chosen per
bar anywhere in the track's range let the melody drift, spanning up to 64 semitones in a song against 41.

Played where it was placed, against the octave chosen per bar (over 100 corpus songs), what plays again is the same
note 100% of the time, against 72%, and the melody spans 15.6 semitones on average and 20 at most, against 30.8 and
41; it follows its contour 0.30, against 0.14; but by ear every phrase that plays again sounds the same, and it leaps
where a phrase starts again 37% of the time (84% after a rest) against 14%, into a section 35% against 15%, and into
its last note 35% against 13%. Left:

- **Variety in repetition, on purpose:** a phrase that plays again is the same notes; the octave chosen per bar
  varied it only as a side effect. A varied repeat as its own decision at generation: a phrase or a bar that plays
  again moved or changed, leaned by conventionality.
- **The contour shapes the melody weakly:** within a 4-bar pattern a bar's mean pitch follows the register it aims at
  0.30 (`MelodyContourTest`, over 100 corpus songs), as the line turns towards the aim only past
  `MelodyLine.RegisterPull` and otherwise goes on or turns back by its draw alone. A phrase's shape may start again
  every half phrase, a wave (`MelodyLayers.Periods`, 19% of the patterns, the more the less conventional the section's
  rhythm), and a note's draw of going on or turning back (`CompositionStateKinds.MelodyTurn`) can lean towards the aim
  (`MelodyLayers.AimOdds`), off at 1; with the octave still chosen per bar, odds of 32 moved the following only from
  0.14 to 0.20, so retune it now. The echoes mask the rest (without them 0.60), as they replay a note's step whatever
  the aim: an echo run's octave or transposition chosen towards the aim would let a repeated bar follow the arch as a
  sequence. The wave and the lean wait for this work.
- **Where it leaps:** a phrase or a section starts afresh at its aim, and a pattern's end wraps to its start, so the
  melody leaps there about a third of the time, mostly after a rest; whether that sounds like a new phrase or a break
  is to be heard. Two rules tried to close a phrase onto its start and were dropped: aiming the last bar back moved
  little, and landing the last note near the start only moved the leap one note earlier.
- **The bass** could be placed at generation the same way, and its notes played again in a repeated cycle.

## Velocity

A note's velocity is the sum of its layers, which `Render` plays on a fixed scale (`Render.ToMidiVelocity`), where it
spread every song's over the MIDI range, which stretched whatever variety was left: a track's level by its role
(`VelocityLayers.GetLevel`), where it was drawn at random, the melody about 3 dB over the others and, by ear, the
chords 1 to 2 dB over the bass and the drums (-8.6 dB against -9.9 and -9.1, the melody -6.8); a section's, leaned by
its energy; a bar's and a bar pattern's; and a note's, its beat's accent, fixed, and a variation, both as far as the
track's dynamics have them (`CompositionStateKinds.NoteDynamics`, bass 0.4, chords 0.5, melody 0.8, drums 1, times
a section's chance scale to the power of 1/4). A chord's notes play at n^-1/4 for n notes. Measured over 100 corpus
songs, against the velocities before: the accent of a bar's downbeat over an 8th off the beat 9 for the bass and the
chords, against 23, 16 for the melody and the drums; a chord sounds as loud as a note, where it was 5 dB louder, and
the melody 2 to 4 dB over the others, where every track was as loud; a note at the same place from bar to bar within a
section varies by 3 to 5, and the sections' loudness follows their energy 0.59 in plain sections, against 0.51. Left:

- **Listen** to how even the bass and the chords play; the dynamics and chord softening are tuned by measurement
  only, the levels by ear too (the chords raised from under the others).
- **The bar layers** (a bar's and a bar pattern's loudness, drawn) add little, 3 to 5 from bar to bar; they could go,
  should bars sound to jump.

## Architecture

From the review of September 2026; the two small errors it found, the melody read only where a note
has a chord note offset and wiring left to silent defaults, are fixed, and a trace entry carries what was decided as a
value (`StateTraceEntry.Value`, such as a `FillDecision`), at a point named in `TracePoints`, which the tests read in
place of its words. The item that placed the chords and the bass at generation, as the melody is, was wrong: they
already repeat all their pitch classes where a section plays again, and `Realizer` only chooses their register from
what came before, as it now does the melody's (see *Melody at generation*). The rhythm's unconventionality leans its
choices as a `Tilt` (`RhythmicUnconventionality.Tilt`), every option by how unconventional it is, as the energy leans
by how loud: a weight times the odds, a chance by its odds, so that none is capped at certain; the toms' order of pitch
leans conventional twice as much (`FillLayers.PitchOrderLean`), which keeps it in 97% of the plainest sections' runs.
A section's scale leans by its energy as far as its harmony follows it (`HarmonicUnconventionality.Coupling`).
Every line the drums mark weighs (`FillLine.Weight`): a section change nothing, a phrase line less
(`FillLayers.PhraseWeight`), and the energy it leads into on top; the weight leans one table of spans and one of
landings, and moves the fill's fullness, where a section's and a phrase's tables were, and the energy's direction alone
leans whether a fill stops the groove. Over 100 corpus songs phrase lines fill as their table had them (70% none) and
land a little more often (a kick 15% against 10%); at a section change the fills follow its energy more, a fill's
fullness correlating 0.6 with it against 0.24, and the drums landing into a quieter section 54% of the time against
78%, which listening should judge. A track's definition says what it plays (`TrackRole`), which the generation asks in
place of its number, and a section is generated by its plan (`SectionPlan`), its energy, scale and render state each
drawn or kept by a method of its own. A stop ends the notes before it in one edit after `Realizer`
(`TimelineEdits.CutNotes`): a pitched track's last note held until there, and every note that would sound past it cut.

The tables, audited: most are how often musicians do a thing (the fills' spans, treatments, speeds, widths and walks,
the landings, the intros and endings, the progressions' homes and root motions, the cadence shapes, the scales, the
bass's approaches and arrivals, the drums' weights and loudness, the phrase ends), convention the draws are pulled
towards, and are kept. Two were shapes and orders chosen by hand, and are drawn or derived now: a phrase scheme's
unconventionality is how many different bars it brings (`PhraseSchemes.GetUnconventionality`), and a phrase's contour
rises to a peak in a bar drawn and falls from it by a slope drawn (`MelodyLayers.GenerateContour`), which covers the
arch, the fall and the rise the table had, and loses its wave; the melody leaps less where a phrase starts again (24%
against 28%) and into a section (25% against 31%). The review's items are done; the tuning constants are left to
listening.

Smaller, when the code is next touched:

- **Chances multiplied:** the rhythm layers' chances (`RhythmLayer.Scale`) and the fills' rarer chances, layered
  state (`FillLayers.Chances`), are still multiplied by the chance scale and capped at 1; tuned so, they could lean by
  their odds as the choices do, should the capping show.
- **Offsets as lists:** the chord root, the chord note and the articulation are collections that their readers sum,
  and the scale a collection that two layers would silently merge into fourteen notes; additive kinds, and a kind that
  may be set once, would say what they are.
- **Echo keys** are hashed seeds where a plain key of the bar pattern, the cycle and the place would do.
- **Visibility:** the tables (`FillLayers`, `FormLayers`, `Drummer`, `MelodyBusyness` and more) are public though
  nothing outside needs them.
- `TrackEventStateTimelineMap.MergeStateTimelineMap` in place of merging a map of no tracks to add common state; one
  `Pick` over weights in `Generators` in place of the copies; `StateMap.With(kind, value)` for setting finished state.
- **Memory:** every `RealizedNote` keeps the state it was decided from, which a song now holds on to (about 2 MB a
  song, 8 MB with a trace). Recompute it on demand instead, should memory matter.

## Drums

A section chooses its drums (`DrumKitGenerator.SelectActiveDrums`): the kick and the snare, and one or two optional
groups (the timekeepers, the toms, the accents, the percussion), one drum of each, two of the percussion. Over 200
corpus songs the timekeepers groove in 85% of the sections (76% before their weight was raised to 3), the percussion in
34% (40%), and a section grooves on 3.2 drums (`DrumUseReportTest`). The triangle, the cuica and the whistle are left out of the song's percussion, as they grate in a
groove, until genres call for them.

Drums by bar (`DrumPresence`): the section's first bar pattern, the phrase scheme's A, plays all its drums, and an
optional drum sits out the bars of another letter 30% of the time, from about 10% in the plainest sections to 60% in the
wildest, less the more energy the section has, drawn from a stream of its own; the kick and the snare always play.
Bars of the same letter play the same drums. The drums a bar plays change from the bar before in 39% of the bars,
against 23% (fills, crashes and sparse drums), and a bar plays 3.7 drums against 3.9. Planned, in this order:

1. **Grooves for the percussion:** a percussion drum's fixed rhythm by its register, the low ones grounding the
   downbeats as the kick does, the high ones an offbeat or backbeat figure, as the kick's and the snare's fixed state
   anchor a drum kit's groove.
2. **Sections of percussion only:** a section may play its percussion without the drum kit, a chance as layered state of
   the song's and the section's, leaned by energy and conventionality, so that a song switches to percussion and back
   at section lines, and one that leans far enough plays percussion throughout. What it touches: the kick and the snare
   are always on only in a drum kit section; the percussion plays more drums in its own section, and only where the
   song has two or more; the landings take the roles of the section they lead into; a run draws its family (the drum
   kit, the percussion or both) from what its section has, which also makes deliberate percussion fills in a drum kit
   song; and the count-in clicks on a drum the song has, which fixes today's silent count-in in songs without a
   hi-hat.

Later:

- **Doubling the snare:** the tambourine or the clap playing the snare's rhythm, its strong ranks (the backbeat), as
  the same rhythm state with a sound of its own; the clap's ghost notes, the weak ranks, only after listening, as a
  clap there may sound busy. The tambourine would leave the timekeepers, where it replaces the hi-hat.
- **Shakers over the hi-hat:** layering them rather than replacing it, which needs care in how their figures fit.
- **Drums by appearance:** a section's later appearance changing its drums, as an edit after assembly (see *Energy by
  appearance*).

## Chords

Chord shapes are pitch fractions snapped to the scale, picked from a table ordered by unconventionality, laid out by a
voicing step and led from chord to chord (see `ChordShapes`, `HarmonicUnconventionality`, `ChordVoicing`,
`Realizer.SnapChordToScale` and `VoiceLeader`). Still to do:

### Jitter on in-between heights

Heights between two qualities, such as the third, could move a little from chord to chord, so that a scale with more
notes than seven picks sometimes one quality and sometimes the other. Worth it once scales of other sizes than seven
exist; in a 7-note scale it changes nothing.

## Fills

### Snare and cross-stick

A section whose snare group plays the cross-stick gives the run the cross-stick's state, which works as a rhythm, but
the run then plays a cross-stick or a clap as its snare. Better handling later: the song's main snare's sound over the
section's rhythm, or both where the song has both.

### Tuplets of the other drums

A run plays the snare's feel only, so where the hi-hat or the percussion plays a tuplet and the snare does not, the run
stays straight: wild songs play about 1.8 times the tuplet notes of plain ones, down from 2.1. Reading the tuplet the
drums play most, as the fills once did, would bring it back, should wild songs sound too straight.

### How fast runs are

A run is two ranks finer than the snare's groove, and the snare's backbeat is sparse, so over 200 corpus songs about
three runs in ten play quarters or slower, half play 8ths or 16ths. Should slow runs sound weak, a run's finest rank
could aim at the finest the tempo allows, a step or two less, keeping only the groove's cycle and phase.

### Left from the fill review

Every fill is a run now, and a landing a note of a few sounds; what is still fixed, best done with the work it belongs
to:

- **Loudness:** a run's swell, its accents and a landing's hit are constants, where the groove's loudness is layers; a
  fill's velocity layer would make them cumulative with the section's, which section dynamics will want.
- **Speed changes** are a rank more for one half of the span, a case of ranks changing along it (see *Segments in any
  pattern*).
- **Walks:** one way, turn, loop and random could be one walk with a few values, should they grow.
- **Idioms the draws no longer tie together:** a lift is now a half-beat run on any sounds, rarely the open hi-hat,
  and a landing takes any cymbal sound, the china and the splash as often as the crashes.
- **Sounds by convention:** a run or a landing draws among a drum's sounds evenly, so half the cymbal landings are the
  china or the splash, accents that mark a downbeat less than a crash, where a plain song would crash. Each sound could
  carry how conventional it is, as data on its drum like the toms' order of pitch: the crashes 1, the china and the
  splash less, their weights multiplied by the section's chance scale, so plain sections crash and wild ones reach for
  the others.
- **The drummer's walks and busyness as state:** the fills' rarer choices and each drum
  group's chance of a run are layered state now, and a signature a song layer over them; the favourite walk, a choice
  among four, would need a pool like the chord pool's, and busyness, which weighs the spans and moves the fullness,
  would become a fullness and a span length that the section's energy adds to.

### A drum's sounds together

A drum is one track, whose note plays one sound, so a run's window plays one sound of a drum at a time: never two
toms together. Notes of several sounds on one track would allow it.

## Form

- **Fade-outs:** an ending that fades over the last section needs channel volume automation, since `Render` spreads the
  notes' velocities over the whole song, so a fade in them would be undone.
- **Intros of their own material,** such as a riff the song does not play otherwise.

## Rhythm engine

A bar pattern's rhythm comes from the dyadic engine (`DyadicRankThresholdPattern`): a cycle (the period) is halved
again and again down to a top rank, and each position is kept by chance by its rank. Most of what fills, busier
melodies and repetition need is a small extension of it rather than a new engine, which keeps its settings and all
the tuning built on them, such as the snare's backbeat and the shares of speed and tuplets. Each step is off by
default, so it can first be shown to leave the recorded songs unchanged, and then tuned by measurement. Built so far:
fullness and repeated cycles (rolls, riffs and pulses), phrase schemes, rhythmic unconventionality, the melody's
rhythm (its busyness, riffs, and phrase ends with a held note and a rest), fills with landings and a drummer, and
echoes: within a section's melody, a note of a bar pattern that comes back, or of a cycle that repeats the one before,
plays the note it had again, as the scale step from its chord's root (`MelodyPattern.GetEcho`, `MelodyLine`).
A beat of a repeated cycle also plays the values its beat had in the cycle it repeats, for every track: its accent,
its walks (such as the hi-hat's open or closed sound and an arpeggio's chord note), its lengths and the melody's step;
only what depends on its position, the chord there, is its own (`DyadicRankItemPattern`). Over 100 corpus songs about
half the hi-hat's notes are in a repeated cycle, 35 to 40% of the snares', a quarter of the kick's and the ride's, and
10 to 13% of the chords' and the bass's. Variety comes from cycles drawn afresh, which draw their values afresh too, and
from the bar and section layers; should repeated figures sound mechanical, a small fresh loudness draw could be added
over the replayed accent.

### Left for later

- **Long cycles:** a bar pattern is one bar long, so a slower cycle, such as the kick's slowed to two bars, plays its
  first half and starts again at every bar line. Patterns as long as their cycle would let slow figures run whole, such
  as a crash every two bars or a kick figure answered in the second bar. Worth it if slow figures sound wrong.
- **Segments in any pattern:** bars that mix feels, such as three straight beats and a quintuplet beat; fills get them
  first.
- Choosing whole patterns by measured features (syncopation, evenness) as a family, should a target prove out of reach
  of the dyadic engine; euclidean patterns that fit no cycle; a library of idioms such as clave and bossa; and drums
  generated together, the snare avoiding the kick and the hi-hat filling the gaps.

A cycle that does not fit the bar and is cut off at the bar line, such as 3+3+2, stays as it is: an off-kilter feel,
not a fault.
