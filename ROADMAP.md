# Roadmap

Planned work that has been decided but not built yet.

## Chords

Chord shapes are pitch fractions snapped to the scale, picked from a table ordered by unconventionality, laid out by a
voicing step and led from chord to chord (see `ChordShapes`, `HarmonicUnconventionality`, `ChordVoicing`,
`Render.SnapChordToScale` and `VoiceLeader`). Still to do:

### Jitter on in-between heights

Heights between two qualities, such as the third, could move a little from chord to chord, so that a scale with more
notes than seven picks sometimes one quality and sometimes the other. Worth it once scales of other sizes than seven
exist; in a 7-note scale it changes nothing.

## Rhythm engine

A bar pattern's rhythm comes from the dyadic engine (`DyadicRankThresholdPattern`): a cycle (the period) is halved
again and again down to a top rank, and each position is kept by chance by its rank. Most of what fills, busier
melodies and repetition need is a small extension of it rather than a new engine, which keeps its settings and all
the tuning built on them, such as the snare's backbeat and the shares of speed and tuplets. Each step is off by
default, so it can first be shown to leave the recorded songs unchanged, and then tuned by measurement. Built so far:
fullness and repeated cycles (rolls, riffs and pulses), phrase schemes, rhythmic unconventionality, and the melody's
rhythm (its busyness, riffs, and phrase ends with a held note and a rest).

### 4. Fills

A new stage after the sections are put one after another, the only one that knows the boundaries. A fill is an
archetype with parameters, and its voices are dyadic patterns like any other, not exact figures: a tom run is a fast,
full cycle whose articulation walks down the toms, a snare roll a fast, full cycle with a velocity ramp, and a pickup a
sparse one. A note that chance leaves out is welcome, as a drummer would leave it out; nothing needs to be strict.
A voice can be two short patterns one after the other, such as 8ths then 16ths, for a fill that speeds up.

The archetypes: none, pickup (a few sparse hits), tom run, snare roll or build, around the kit, break (the drums, or
rarely the band, silent), stop-time (a unison hit, then silence), and a cymbal or open hi-hat swell. Each has a span
before the line, a treatment of the groove there (kept, thinned to kick and snare, dropped, or stopped), and a landing
on the downbeat (crash and kick, kick only, a choke, or nothing). Section changes get fills more often than repeats,
and phrase ends now and then a pickup. A song-level drummer sets how busy and in what vocabulary; the tempo caps the
finest grid. Taking events out in a span is a new timeline operation, kept to this stage.

### Left for later

- **The melody's direction:** about 58% of its moves turn back, where a sung line turns less often. It did not change
  when the melody got busier, so it comes from `MelodyLine`'s rules, not from sparse notes; worth tuning there.
- **Motifs within a bar:** a melody's repeated cycle repeats its rhythm, but its pitches follow the rules afresh; a
  motif could be remembered per cycle as well as per bar.

- **Long cycles:** a bar pattern is one bar long, so a slower cycle, such as the kick's slowed to two bars, plays its
  first half and starts again at every bar line. Patterns as long as their cycle would let slow figures run whole, such
  as a crash every two bars or a kick figure answered in the second bar. Worth it if slow figures sound wrong.
- **Segments in any pattern:** bars that mix feels, such as three straight beats and a quintuplet beat; fills get them
  first.
- Choosing whole patterns by measured features (syncopation, evenness) as a family, should a target prove out of
  reach of the dyadic engine; euclidean patterns that fit no cycle; a library of idioms such as clave and bossa; fills
  in the section's tuplet feel; energy-aware fills that build into loud sections and break before quiet ones; and drums
  generated together, the snare avoiding the kick and the hi-hat filling the gaps.

A cycle that does not fit the bar and is cut off at the bar line, such as 3+3+2, stays as it is: an off-kilter feel,
not a fault.
