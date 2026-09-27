# Roadmap

Planned work that has been decided but not built yet.

## Next

In this order, each measured before it is planned:

1. **Listen and tune.** The runs, rebuilt on the groove (whether they sound part of it now, whether the drums they
   draw, their walks and windows sound interesting or broken, how often they play drums other than the snare and the
   toms, about one run in five), intros (whether the band's entry after a chords-first or build-up intro feels earned)
   and endings (whether the ritardando plays in the browser, whether a final chord taken from a weak note is too quiet)
   have been tuned by measurement only, as have the sections' energy and modes (see *Section dynamics* and *Section
   modes*) and the melody's echoes (see *Rhythm engine*), which play a bar that comes back, and so a section's second
   phrase and a section that recurs, much as it was.
2. **The melody at generation,** as planned in *Melody at generation*.
3. Smaller: fade-out endings (see *Form*), and moving the melody's final note and a stop's hold from render flags to
   edits of the notes after `Realizer`, one at a time, as a stop's cut of the notes that would sound into it already is
   (`TimelineEdits.CutNotes`).

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

- **Listen** to the changes: whether a section in the parallel mode sounds like a new colour or a mistake, and whether
  the cadence into it prepares it.
- **Key changes,** such as a last chorus a step up: a section's key as its own state, as its scale is now.

## Melody at generation

The melody is the one track whose notes are decided in `Realizer`, one after another over the song, from the note
before and render flags (`BeatRank`, `MelodyStep`, `MelodyRegister`, `Echo`, `MelodyFinal`, read only there); the
echoes patch the repetition that this loses with a memory of their own. Decided: the melody is placed at generation,
where the other tracks' notes are decided.

- The key reaches the section generator (drawn in its own stream, so nothing else changes).
- `MelodyLine` moves into `MelodyPattern`: one line per section places its notes in order over the section's bars, as
  part of each note's values, with the note's chord worked out as `Realizer` does (`Realizer.GetChord`); a note keeps
  its scale step above its chord's root as its state. A bar pattern that comes back in the section plays its notes
  again, by its seed; a varied repeat is placed afresh; a repeated cycle plays its steps again by the rhythm engine; a
  section that recurs is the same by construction.
- `Realizer` plays the given step; its melody memory, the echo key and the render flags go.
- The final note becomes an edit as the song is put together (`CreateEnding`): the chord's root in the octave nearest
  the note it is made from.
- Measured against the echoes: leaps, the mean move, chord notes on the beat, the same note over the same root, and
  leaps where a section starts, whose line starts afresh on the chord note nearest its phrase's aim; the other tracks
  unchanged.

## Architecture

Left from the review, each small and best done when the code is next touched:

- **Track roles:** a role on a track's definition in place of checking track numbers (`SongTracks.MelodyTrack`,
  `>= DrumGroups.FirstTrackNumber`) in the generators.
- **Visibility:** the tables (`FillLayers`, `FormLayers`, `Drummer`, `MelodyBusyness` and more) are public though
  nothing outside needs them.
- `TrackEventStateTimelineMap.MergeStateTimelineMap` in place of merging a map of no tracks to add common state; one
  `Pick` over weights in `Generators` in place of the copies; `StateMap.With(kind, value)` for setting finished state.
- **Chance scales as tilts:** the choices the chance scale multiplies (stopping, the random walk, a group's run chance
  to the power of its unconventionality, the drummer's spans) are each a weight times a scale to the power of how the
  option leans, which `Tilt.Weigh` is; they could move to it when next touched.
- **Memory:** every `RealizedNote` keeps the state it was decided from, which a song now holds on to (about 2 MB a
  song, 8 MB with a trace). Recompute it on demand instead, should memory matter.

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
echoes: a melody note of a bar that comes back, or of a cycle that repeats the one before, plays the note it had again,
as the scale step from its chord's root (`MelodyPattern.GetEcho`, `MelodyLine`). Over 100 corpus songs, 5% of the
melody's notes are in a repeated cycle and 84% in a bar that comes back; over the same root as before, 77% play the
same note, up from 40%, the others moving to the chord on a strong beat.
A beat of a repeated cycle also plays the values its beat had in the cycle it repeats, for every track: its accent,
its walks (such as the hi-hat's open or closed sound and an arpeggio's chord note), its lengths and the melody's step;
only what depends on its position, the chord there, is its own (`DyadicRankItemPattern`). Over 100 corpus songs about
half the hi-hat's notes are in a repeated cycle, 35 to 40% of the snares', a quarter of the kick's and the ride's, and
10 to 13% of the chords' and the bass's. Variety comes from cycles drawn afresh, which draw their values afresh too, and
from the bar and section layers; should repeated figures sound mechanical, a small fresh loudness draw could be added
over the replayed accent.

### Left for later

- **Echoes in the bass:** the bass could play its notes again by the same key, where its approaches into the next
  chord allow.

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
