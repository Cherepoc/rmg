# Roadmap

Planned work that has been decided but not built yet.

## Next

In this order, each measured before it is planned:

1. **Listen and tune.** The runs, rebuilt on the groove (whether they sound part of it now, whether the drums they
   draw, their walks and windows sound interesting or broken, how often they play drums other than the snare and the
   toms, about one run in five), intros (whether the band's entry after a chords-first or build-up intro feels earned)
   and endings (whether the ritardando plays in the browser, whether a final chord taken from a weak note is too quiet)
   have been tuned by measurement only, as have the sections' energy and modes (see *Section dynamics* and *Section
   modes*) and the melody placed at generation (see *Melody at generation*), which plays a section's second phrase and
   a section that recurs as it was, and leaps where a phrase starts again.
2. Smaller: fade-out endings (see *Form*), and moving a stop's hold from a render flag to an edit of the notes after
   `Realizer`, as a stop's cut of the notes that would sound into it already is (`TimelineEdits.CutNotes`), and the
   melody's final note an edit as the song is put together (`SongFormGenerator.LandOnRoot`).

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

The melody is placed where the other tracks' notes are decided, not in `Realizer`: once a section's bars are made, one
line places its notes in order over them, by the rules of `MelodyLine`, over the chords `Realizer` would work out
(`MelodyPattern.Place`), and every note keeps its scale step above its chord's root (`StateKinds.ScaleStep`), which
`Realizer` plays. The section then plays it the same wherever it plays: its second phrase and a section that recurs are
the same notes, where before, placed as the song played, a bar that came back played the same note over the same root
40% of the time. The melody's render flags are generation state now, and the final note lands on the root as an edit of
its step (`SongFormGenerator.LandOnRoot`).

As the chords' voicing and the bass's octave are, the melody's register is chosen in `Realizer` from what came before:
a phrase, the notes placed for a section's 4-bar pattern, is moved by whole octaves to start nearest the note before,
within the track's range (`PhraseRegister`, `StateKinds.PhraseStart`), so what plays again plays the same notes, in
the octave its phrase starts in.

Measured over 100 corpus songs, against the melody placed as the song played: chord notes on the beat 87.5%, up from
81%; within phrases the melody moves about as before (2.9% leaps against 2.2%), but where a phrase starts again, at a
section's start or its second phrase, 27% of the moves are leaps, against 3% before, 83% of them after a rest, as a new
phrase starts after a breath; 38% before the phrase's octave was chosen, which a range of an octave or two often has
no room for. Left:

- **Listen** to where phrases start again. Two rules tried to close a phrase onto its start and were dropped: aiming
  the last bar back moved little, and landing the last note near the start only moved the leap one note earlier.
- **The bass** could be placed at generation the same way, and its notes played again in a repeated cycle.

## Architecture

From the review of September 2026, in this order; the two small errors it found, the melody read only where a note
has a chord note offset and wiring left to silent defaults, are fixed, and a trace entry carries what was decided as a
value (`StateTraceEntry.Value`, such as a `FillDecision`), at a point named in `TracePoints`, which the tests read in
place of its words. The item that placed the chords and the bass at generation, as the melody is, was wrong: they
already repeat all their pitch classes where a section plays again, and `Realizer` only chooses their register from
what came before, as it now does the melody's (see *Melody at generation*). The rhythm's unconventionality leans its
choices as a `Tilt` (`RhythmicUnconventionality.Tilt`), every option by how unconventional it is, as the energy leans
by how loud: a weight times the odds, a chance by its odds, so that none is capped at certain; the toms' order of pitch
leans conventional twice as much (`FillLayers.PitchOrderLean`), which keeps it in 97% of the plainest sections' runs.
A section's scale leans by its energy as far as its harmony follows it (`HarmonicUnconventionality.Coupling`).

1. **A line's weight:** the fills' section and phrase tables (spans, landings, fullness) could be one value that
   scales the chance of no fill, the span, the fullness and the landing, of which the energy it leads into is a part;
   a phrase line in the middle of a section, whose energy does not change, has no lean now.
2. **Track roles:** a role on a track's definition in place of checking track numbers (`SongTracks.MelodyTrack`,
   `ChordsTrack`, `BassTrack`, `DrumGroups.FirstTrackNumber`, 13 checks in `PatternGenerator`, `SectionGenerator` and
   `SongFormGenerator`); and `SectionGenerator.Generate`, which draws the section's energy, scale, harmony, drums, bar
   state and tracks, places its melody and keeps its notes' render state, split, with its flags (`hasTonicHome`,
   `keepsSongScale`) a plan of the section in their place.
3. **Tables:** 139 tuning constants and 30 weighted tables in `Composition`; some are convention (a drum's weight),
   some shapes chosen by hand (the melody's four contours, its phrase ends, the fills' spans, treatments, speeds and
   widths, the leans of drums, spans and treatments) that a rule could derive or a draw could make.
4. **A stop's cut in one place:** its last note held to the stop before `Realizer` (`TimelineEdits.CutBefore`) and the
   notes that would sound into it cut after (`CutNotes`) could both be edits after it.

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
