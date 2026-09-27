# Roadmap

Planned work that has been decided but not built yet.

## Next

In this order, each measured before it is planned:

1. **Fills from the groove** (see *Fills*): the runs are rebuilt on the section's groove, as measured and decided there.
2. **Listen and tune.** The new fills (their level against the groove, the length of runs, whether twists sound
   interesting or broken), intros (whether the band's entry after a chords-first or build-up intro feels earned) and
   endings (whether the ritardando plays in the browser, whether a final chord taken from a weak note is too quiet) have
   been tuned by measurement only.
3. **Section dynamics.** One energy value per section, drawn around the song's, that drives what is drawn apart today:
   the section's velocity, the drums' fullness and which groups play (ride and crash when loud, cross-stick when
   quiet), the melody's busyness and the chords' rhythm, and fills that build into a loud section and break before a
   quiet one. Verse, pre-chorus and chorus then differ by design. Measure first how far sections differ now.
4. **Sections changing mode,** such as a chorus in the relative major or a darker bridge: the section's scale becomes
   its own state; scales, homes and cadences already work per scale.
5. Smaller: melody motifs remembered per cycle (see *Rhythm engine*), fade-out endings (see *Form*), and moving the
   melody's final note and a stop's cuts from render flags to edits of the notes after `Realizer`, one at a time.

## Architecture

Left from the review, each small and best done when the code is next touched:

- **Track roles:** a role on a track's definition in place of checking track numbers (`SongTracks.MelodyTrack`,
  `>= DrumGroups.FirstTrackNumber`) in the generators, and the melody's pattern code (phrase ends, steps, motifs) out
  of `PatternGenerator` into a class of its own.
- **Split `FillGenerator`** into the decisions and a player of fill specs, once fills grow again.
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

### From the groove

The runs sound detached from the groove they end. Measured over 300 corpus songs, against the drums but the kick in
the bar before each fill:

- **Grid:** the runs play on the fixed grid (16ths, 8ths above 150 BPM) while the groove's hands play 8ths or coarser
  in 70% of bars, so three runs in four play finer than the groove, and one in three at least four times finer.
- **Feel:** one run in five falls off the groove's grid, straight over a triplet, dotted or quintuplet groove (one in
  three around the kit), since a fill takes a tuplet only where it fills a quarter of the last bar's drum notes.
- **Loudness:** tom runs and runs around the kit average 0.37 against the groove's 0.21, louder in about 80% of fills;
  snare rolls average 0.14, quieter than the groove.

Decided:

- **One run** in place of the tom run, the snare roll, the run around the kit and the pickup; the fills are then the
  run, the break, stop time and the lift.
- **The rhythm is the groove's:** the snare track's state at the line plus a fill layer, which raises the max rank and
  the fullness and sets the variation to 0, so the run keeps the section's cycle, phase and tuplet, and its accents
  fall by rank where the groove's do. A pickup is a small layer; a speed-up adds a rank for the second half; the
  150 BPM limit on 16ths becomes a limit on the max rank. Only the snare's state is read, not the hi-hat's.
- **Each instrument's limits** are layers of its own over that, such as the cymbals kept to coarse notes; the
  section's conventionality loosens them. The drummer's busyness moves the fullness.
- **The sounds:** a weighted set of the song's drums, mostly the snare and the toms, in a random order that a
  conventional section pulls toward the toms' pitch order, walked one way, turning, looping, or at random, mostly to a
  neighbour. Each note plays the sounds in a window of the walk, whose width is drawn, mostly 1, with no limit.
  The snare's single sound makes a set of the snare alone a roll.
- **Twists:** upward, zigzag, odd voice and gappy go, as walks, sets, windows and layers; tuplet (where the groove has
  none), slow-down (the max rank down), odd span, fading, early landing and no landing stay.

This is when to split `FillGenerator` (see *Architecture*).

### Snare and cross-stick

A section whose snare group plays the cross-stick gives the run the cross-stick's state, which works as a rhythm, but
the run then plays a cross-stick or a clap as its snare. Better handling later: the song's main snare's sound over the
section's rhythm, or both where the song has both.

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
