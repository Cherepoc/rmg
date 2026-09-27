# Roadmap

Planned work that has been decided but not built yet.

## Next

In this order, each measured before it is planned:

1. **Listen and tune.** The runs, rebuilt on the groove (whether they sound part of it now, whether the drums they
   draw, their walks and windows sound interesting or broken, how often they play drums other than the snare and the
   toms, about one run in five), intros (whether the band's entry after a chords-first or build-up intro feels earned)
   and endings (whether the ritardando plays in the browser, whether a final chord taken from a weak note is too quiet)
   have been tuned by measurement only.
2. **Section dynamics.** One energy value per section, drawn around the song's, that drives what is drawn apart today:
   the section's velocity, the drums' fullness and which groups play (ride and crash when loud, cross-stick when
   quiet), the melody's busyness and the chords' rhythm, and fills that build into a loud section and break before a
   quiet one. Verse, pre-chorus and chorus then differ by design. Measure first how far sections differ now.
3. **Sections changing mode,** such as a chorus in the relative major or a darker bridge: the section's scale becomes
   its own state; scales, homes and cadences already work per scale.
4. Smaller: melody motifs remembered per cycle (see *Rhythm engine*), fade-out endings (see *Form*), and moving the
   melody's final note and a stop's cuts from render flags to edits of the notes after `Realizer`, one at a time.

## Architecture

Left from the review, each small and best done when the code is next touched:

- **Track roles:** a role on a track's definition in place of checking track numbers (`SongTracks.MelodyTrack`,
  `>= DrumGroups.FirstTrackNumber`) in the generators, and the melody's pattern code (phrase ends, steps, motifs) out
  of `PatternGenerator` into a class of its own.
- **Visibility:** the tables (`FillLayers`, `FormLayers`, `Drummer`, `MelodyBusyness` and more) are public though
  nothing outside needs them.
- `TrackEventStateTimelineMap.MergeStateTimelineMap` in place of merging a map of no tracks to add common state; one
  `Pick` over weights in `Generators` in place of the copies; `StateMap.With(kind, value)` for setting finished state.
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

- **A line's weight:** the section's and the phrase's tables of spans and landings could be one value that scales the
  chance of no fill, the fullness, the span and the landing: the energy of *Section dynamics*.
- **Loudness:** a run's swell, its accents and a landing's hit are constants, where the groove's loudness is layers; a
  fill's velocity layer would make them cumulative with the section's, which section dynamics will want.
- **Speed changes** are a rank more for one half of the span, a case of ranks changing along it (see *Segments in any
  pattern*).
- **Walks:** one way, turn, loop and random could be one walk with a few values, should they grow.
- **Idioms the draws no longer tie together:** a lift is now a half-beat run on any sounds, rarely the open hi-hat,
  and a landing takes any cymbal sound, the china and the splash as often as the crashes. The drummer's signature
  twist went with the twists.
- **Sounds by convention:** a run or a landing draws among a drum's sounds evenly, so half the cymbal landings are the
  china or the splash, accents that mark a downbeat less than a crash, where a plain song would crash. Each sound could
  carry how conventional it is, as data on its drum like the toms' order of pitch: the crashes 1, the china and the
  splash less, their weights multiplied by the section's chance scale, so plain sections crash and wild ones reach for
  the others.
- **Fill values as state,** built with section dynamics, which needs the same plumbing for its energy: the chances of
  starting off the beat, fading and landing early as multiplicative state kinds (a base, the section's chance scale, a
  signature's ×5, capped at 1 when read), layered in `FillGenerator` from the fills' own random stream, so that songs
  stay the same outside the lines: a base layer, the drummer's song layer, which brings the signature back as a
  raised value, and the section's. A run's chance of a role's drums would follow as each group's own state, per role
  so that a song of four percussion drums plays them no more than one, with how unconventional a group is, 0 for the
  snare and the toms, as the power of the chance scale that multiplies it, in place of `RoleChances` and
  `ConventionalRoles`.

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
rhythm (its busyness, riffs, and phrase ends with a held note and a rest), and fills with landings and a drummer.

### Left for later

- **Motifs within a bar:** a melody's repeated cycle repeats its rhythm, but its pitches follow the rules afresh; a
  motif could be remembered per cycle as well as per bar.

- **Long cycles:** a bar pattern is one bar long, so a slower cycle, such as the kick's slowed to two bars, plays its
  first half and starts again at every bar line. Patterns as long as their cycle would let slow figures run whole, such
  as a crash every two bars or a kick figure answered in the second bar. Worth it if slow figures sound wrong.
- **Segments in any pattern:** bars that mix feels, such as three straight beats and a quintuplet beat; fills get them
  first.
- Choosing whole patterns by measured features (syncopation, evenness) as a family, should a target prove out of reach
  of the dyadic engine; euclidean patterns that fit no cycle; a library of idioms such as clave and bossa; and drums
  generated together, the snare avoiding the kick and the hi-hat filling the gaps. Energy-aware fills come with section
  dynamics (see *Next*).

A cycle that does not fit the bar and is cut off at the bar line, such as 3+3+2, stays as it is: an off-kilter feel,
not a fault.
