# Roadmap

Planned work that has been decided but not built yet.

## Chords

Chord shapes are pitch fractions snapped to the scale, picked from a table ordered by unconventionality and laid out by a
voicing step (see `ChordShapes`, `HarmonicUnconventionality`, `ChordVoicing` and `Render.SnapChordToScale`). Still to do:

### Voice leading

Each chord takes the voicing closest to the chord before it on the same track, so common notes stay and the others
move as little as possible: C-E-G to F becomes C-F-A instead of F-A-C. It belongs in `Render`, after snapping, since
it needs the actual pitches of the previous chord:

- the candidates are the inversions and octave placements of the chord that fit the track's range;
- the one with the least total movement from the previous chord wins, with a small pull towards the middle of the
  range so that a progression does not drift to one edge;
- the first chord of a section keeps the voicing it was generated with, a repeated chord keeps its voicing, and
  shapes with a fixed voicing are left alone.

It only concerns tracks that play whole chords; melody and bass pick single chord notes. Rendering then keeps the
previous chord of a track, where it now renders every note on its own.

### Jitter on in-between heights

Heights between two qualities, such as the third, could move a little from chord to chord, so that a scale with more
notes than seven picks sometimes one quality and sometimes the other. Worth it once scales of other sizes than seven
exist; in a 7-note scale it changes nothing.
