# Roadmap

Planned work that has been decided but not built yet, and the decisions that keep it so.

## Next

In this order, each measured before it is planned:

1. **Listen and tune.** The sections' energy has been tuned by measurement only (see *Section dynamics*). Heard and
   kept: the endings, the section modes, the chords' level, the drums, the fills and their runs, the fades, and the
   intros of entries.
2. **The melody** (see *Melody*): listen to the question and its answer and to the improvised appearances; then a bar
   of a later letter within the phrase.

Then, by what is heard for its cost: the song's shape before the drums' details, which are paused. Every section is
a 4-bar pattern played twice, a chord a bar, so the songs are alike in their form more than in anything the drums do.

3. **The energy in the pitched tracks, and by appearance** (see *Section dynamics*).
4. **Harmonic rhythm** (see *Chords*).
5. **Panning** (see *Instruments*).
6. **A section's length** (see *Form*).
7. **Pitched roles and the arrangement** (see *Instruments*).
8. **Harmonised doubling** (see *Instruments*).
9. **Occasional chords** (see *Instruments*).
10. **Swing** (see *Groove*).
11. **Articulations** (see *Instruments*).
12. **Sections with a role** (see *Form*).
13. **Solos** (see *Instruments*).
14. **A short bar at a section change** (see *Meter*).
15. **Bars of any length and polyrhythms** (see *Meter*), needed someday, low priority.

Alongside, whenever the web page is touched: **ratings** (see *Listening data*), so that listening is measured as the
corpus is.

## Section dynamics

A section's energy (`SectionEnergy`) leans its loudness, its drums' fullness and density, which drums play, and the
fills into it. Over 200 corpus songs, in plain sections and wild ones, it correlates with loudness 0.51 and 0.27, with
how many drums play 0.46 and 0.32, but with the drums' notes only 0.22 and 0.19. Left:

- **The drums' notes** follow the energy weakly: the section layers' density and fullness move little, so even with
  energy near deciding (odds of 100,000) the drums' notes correlate with it only 0.48. The section's shared rhythm
  layer, or the drums' speed, would move them more, the first also moving the pitched tracks.
- **The melody's busyness and the chords' rhythm,** by the same pull.
- **Energy by appearance,** so that the last chorus plays louder than the first: a section already appears afresh
  every time it plays (`GeneratedSection.Appear`), which may be a simpler place for it than an edit after assembly.

## Section modes

A section may play in a parallel mode (`Scales.PickSection`). Left:

- **Listen** whether the cadence into a change prepares it; the parallel major or minor and the harmonic minor sound
  clearly (corpus seeds 196, 10, 36 and 171), the one-note modes more faintly.
- **Key changes,** such as a last chorus a step up: a section's key as its own state, as its scale is now.

## Melody

- **Varied repeats (A′) and conventionality** (pending listening): a repeated bar is varied by a flat chance of 0.25
  and a fixed strength (`PhraseSchemes.VariedRepeatChance`, `VariedRepeatVariation`). Leaning A′ was not built: which
  way convention runs is unclear (a varied repeat is as conventional in songwriting as a literal loop is in electronic
  music), and the scheme's choice already leans to more distinct bars in wild sections, so leaning A′ the same way
  would push wild sections to no repetition and plain ones to loops. Should listening find plain sections too varied
  or wild ones too literal, lean its chance alone, gently. The answer's lean (more mutation the wilder) awaits the same
  listening.
- **The contour shapes the melody weakly:** a bar's mean pitch follows the register it aims at about 0.2
  (`MelodyContourTest`). The echoes mask it (0.60 without them), as they replay a note's step whatever the aim: an
  echo run's octave or transposition chosen towards the aim would let a repeated bar follow the arch as a sequence.
  The wave (`MelodyLayers.Periods`) and the lean towards the aim (`MelodyLayers.AimOdds`) wait for this, then retune.
- **Gaps before chord changes:** half the changes have no melody note in their last beat, a rest or a held note, where
  a pitched fill at a phrase line, or a counter-melody, would move, should the gaps sound empty.
- **Where it leaps:** a phrase or a section starts afresh at its aim, and a pattern's end wraps to its start, so the
  melody leaps there about a third of the time, mostly after a rest; whether that sounds like a new phrase or a break
  is to be heard. Two rules tried to close a phrase onto its start and were dropped: aiming the last bar back moved
  little, and landing the last note near the start only moved the leap one note earlier.
- **An endless song** (later, low priority): a mode in which a song goes on with sections generated afresh as it
  plays, with no ending. The sections generated per appearance are a step towards it; the form's plan, its intro and
  ending, and the song's single pass are not.

## Velocity

- **Listen** to how even the bass and the chords play; the dynamics and chord softening are tuned by measurement only.
- **The bar layers** (a bar's and a bar pattern's loudness) add little, 3 to 5 from bar to bar; they could go, should
  bars sound to jump.

## Architecture

- **A section's streams:** `SectionGenerator.Generate` derives thirteen numbered streams by hand, each as
  `Seeds.Derive(Seeds.Derive(_seed, sectionId), …)`; an enum of them, as `SongStream` is, and one method to derive one.
- **Note keys** are hashed seeds where a plain key of the bar pattern, the cycle and the place would do.
- **Memory:** every `RealizedNote` keeps the state it was decided from, which a song holds on to (about 2 MB a song,
  8 MB with a trace). Recompute it on demand instead, should memory matter.

Decided, and kept:

- **The tables** that are how often musicians do a thing (the fills' spans and treatments, the landings, the intros
  and endings, the progressions' homes and root motions, the cadences, the scales, the bass's approaches and arrivals,
  the drums' weights, the phrase ends) are convention the draws are pulled towards, and stay tables.
- **The drums' articulation offset stays a list:** every layer's fraction of a drum's sounds is rounded to whole
  sounds and added, so that a layer moves every note under it alike, which an additive number rounded once would not.
- **The scale and a chord's shape are single values that are lists,** set by one layer each (`SingleValuedListsTest`);
  a kind of their own would add a mechanism for a mistake not made, and forbid a section's scale overriding a song's.
- **The chords and the bass are not placed at generation** as the melody is: they already repeat their pitch classes
  where a section plays again, and `Realizer` only chooses their register.

## Drums

- **Listen** to the percussion songs, and to the wild end, where the kick or the snare in a role not theirs may sound
  broken rather than bold (the snares keep time in 2 to 4% of their sections, a train beat).
- **The clap's ghost notes,** soft hits around the backbeat, which a figure on the snare's feel may come near, only
  should listening miss them; and the doubling chance (0.15), should bound drums be too rare to hear.
- **The vibraslap's pickup,** the last beat before a landing, the one other place it would fit.
- **Drums by appearance:** a section's later appearance changing its drums (see *Energy by appearance*).
- **Strokes before a lift:** the snare going from its cross-stick to its head in the last phrase before a louder
  section, an edit after assembly.

Decided: the kit's leads among themselves, the snare and the hi-hat, keep their own feels, over the drums' shared
layers; only the drums that do not lead follow a lead's (`FeelLeads`).

## Instruments

Three pitched tracks play, the chords, the melody and the bass, each on one instrument for the whole song and every
section, all of them in every section.

- **Panning:** no track is panned; the file sends no pan (CC 10), which the web player's synth plays. A place per
  track drawn by its role, the bass and the kick in the middle, the chords and a pad spread, the drums as a kit
  stands.
- **Pitched roles and the arrangement:** more pitched roles, such as a pad, a second chord instrument or a
  counter-melody (a third `LineProfile`), and a section's parts chosen as its drum kit is (`DrumKitGenerator.SelectKit`):
  roles, the ones that lead and the ones that colour, which play leaned by the section's energy, so that a section
  builds by what plays, not only by how loud. The one mechanism for the drums and the pitched tracks, where it can be.
- **Harmonised doubling:** a line doubled a diatonic third or sixth above, as twin guitars play a riff, on a track of
  its own panned against it: a line's notes are scale steps above the chord's root (`LinePlacement`), so the double is
  two steps up. Per section, leaned by energy. Plain doubling, the same part twice, waits for timing by role (see
  *Groove*): two copies with the same notes and timing only play louder.
- **Occasional chords:** how many notes a track sounds at once is its role's (`Realizer`). As a note's state, a note,
  two or the chord, leaned by the beat's accent, the energy and the landings, the melody would play a double stop on
  an accent, a guitar a power chord where a section lands, the bass a chord now and then.
- **Articulations:** a finger, pick or slap bass, a muted guitar or its harmonics are General MIDI programs of their
  own. An instrument's articulations as data, picked as a drum's stroke is (`DrumStrokes`: the song's, changed by a
  section and by a bar of a later letter, the lowest layer winning), a program change where it changes; the playing
  should follow, such as octaves popped in slap and short notes muted, or it is only another sound.
- **Solos:** an appearance of a section in which the melody rests and a solo line improvises over its chords, more
  (`LinePattern.Mutate`), busier and over a wider range, the other parts thinned; a bass solo by the bass's profile
  loosened. Special solo instruments by a pool of their own. Needs the arrangement and sections with a role: the form
  decides which appearance is a solo.

## Groove

- **Swing:** every note lands on the grid, tuplets aside. A song's swing, drawn and leaned conventional towards none,
  would delay the notes off the 8ths (or the 16ths, by the song's) by a fraction of the way to the next, a state that
  generation decides and `Render` plays, every track alike, the drums perhaps more than the chords.
- **Timing by role,** later: the backbeat a little late, the hi-hat on top, should swing alone sound stiff.

## Listening data

The corpus is measured against targets the plan set itself, and listening is one seed at a time. The web page already
records how long each seed is listened to (`Analytics`, `SeedListening`); a rating on the page, stored with the seed
and the version, would give a mark to hold the trace's values against (`StateTraceEntry.Value`), such as which
endings, modes or drum setups are skipped. Later, should targets from outside be wanted: a few measures (the notes a
bar by role, the intervals, the syncopation) of a corpus of real MIDI songs to tune towards.

## Bass

- **The pickup** always falls on the beat; the 8th before the bar line, leaned by convention, waits for listening.

## Chords

- **Harmonic rhythm:** a chord a bar, always (`Progressions`). A section's harmonic rhythm as its state, a chord for
  half a bar, a bar or two bars, drawn and leaned by convention and energy, over the same 4-bar pattern, would change
  how every section moves at no cost to the rhythm engine; the cadence's place and the lines' leading follow the
  changes, not the bars.
- **Jitter on in-between heights:** heights between two qualities, such as the third, could move a little from chord
  to chord, so that a scale with more notes than seven picks sometimes one quality and sometimes the other. Worth it
  once scales of other sizes than seven exist; in a 7-note scale it changes nothing.

## Fills

- **Tuplets of the other drums:** a run plays the snare's feel only, so where the hi-hat or the percussion plays a
  tuplet and the snare does not, the run stays straight: wild songs play about 1.8 times the tuplet notes of plain
  ones, down from 2.1. Reading the tuplet the drums play most would bring it back, should wild songs sound too
  straight.
- **How fast runs are:** a run is two ranks finer than the snare's groove, and the snare's backbeat is sparse, so
  about three runs in ten play quarters or slower. Should slow runs sound weak, a run's finest rank could aim at the
  finest the tempo allows, a step or two less, keeping only the groove's cycle and phase.
- **Loudness:** a run's swell, its accents and a landing's hit are constants, where the groove's loudness is layers; a
  fill's velocity layer would make them cumulative with the section's.
- **Speed changes** are a rank more for one half of the span, a case of ranks changing along it (see *Segments in any
  pattern*).
- **Walks:** one way, turn, loop and random could be one walk with a few values, should they grow.
- **Idioms the draws no longer tie together:** a lift is a half-beat run on any sounds, rarely the open hi-hat, and a
  landing takes any cymbal sound.
- **Sounds by convention:** runs and landings weigh a drum's sounds the same in a plain section as in a wild one; the
  weights could lean by the section's conventionality, so plain sections crash and wild ones reach for the china and
  the splash.
- **The drummer's walks and busyness as state:** the favourite walk, a choice among four, would need a pool like the
  chord pool's, and busyness, which weighs the spans and moves the fullness, would become a fullness and a span length
  that the section's energy adds to.
- **A drum's sounds together:** a drum is one track, whose note plays one sound, so a run never plays two toms
  together. Notes of several sounds on one track would allow it.

## Form

- **A section's length:** every section plays its 4-bar pattern twice. Played once, twice or four times, drawn per
  section and leaned by convention towards twice, with now and then a tag of 2 bars before a section changes, it would
  vary the form with no change to the pattern.
- **Sections with a role** (later, once the above are in): the form draws a section's role, a verse, a chorus, a
  bridge, which leans its energy and its harmony, where now the energy is inferred from how often and where a section
  plays (`SectionEnergy`).
- **Fade-outs,** should listening ask: a ritardando into the fade, the drums fading first, or a tag after it.
- **Intros of their own material,** such as a riff the song does not play otherwise.

## Meter

Every bar is four beats (`Meter.BarDuration`, read in about 50 places), and the rhythm engine halves from the bar.

- **A short bar at a section change:** a bar of two beats before a section, now and then, as an edit after assembly.
- **Bars of any length** (needed someday, low priority): a meter as state, a song's, a section's or a bar's, of any
  number of beats or 16ths, odd ones too, such as 7/8 or 13/16, the bar grouped as a grouped cycle is (3+3+2), in
  place of the fixed four beats: the engine halving from a bar's groups, the progression's chords, the phrase
  scheme's bars and the fills' spans taking the bar's length.
- **Overlapping polyrhythms** (with bars of any length): a figure of a length of its own, such as a riff of 23 16ths,
  running on against the bar, as the kick and the guitar do against the hi-hat and the snare in Meshuggah, meeting the
  bar again at a phrase's end. Needs patterns longer than a bar (see *Long cycles*) and the lengths no power of two;
  it covers *Cross-rhythms across the phrase*.

## Rhythm engine

- **Mechanical repeats:** a repeated cycle replays its beats' values, accents included; should repeated figures sound
  mechanical, a small fresh loudness draw could be added over the replayed accent.
- **Long cycles:** a bar pattern is one bar long, so a slower cycle, such as the kick's slowed to two bars, plays its
  first half and starts again at every bar line. Patterns as long as their cycle would let slow figures run whole,
  such as a crash every two bars or a kick figure answered in the second bar. Worth it if slow figures sound wrong.
- **Segments in any pattern:** bars that mix feels, such as three straight beats and a quintuplet beat; fills get them
  first.
- **Splitting a grouped cycle by its number** (a dotted 8th into three 16ths, one strong and two weak), where the
  engine only halves: the 3-against-4 cross-rhythm as a full 16th stream, should the tresillo alone sound thin.
- **Cross-rhythms across the phrase,** a grouped cycle running on across the bars, need *Long cycles*.
- Choosing whole patterns by measured features (syncopation, evenness) as a family, should a target prove out of reach
  of the dyadic engine; euclidean patterns that fit no cycle; a library of idioms such as clave and bossa; and drums
  generated together, the snare avoiding the kick and the hi-hat filling the gaps.

Decided: a drum keeping time plays grouped cycles (a tresillo) unleaned, 7% of the plainest sections' bar patterns to
23% of the wildest, as the other drums do; the tresillo keeping time is conventional in reggaeton, dancehall or
afrobeats, so its convention is the genres' to say. Should it be wanted, a role's lean on its drum's own layers, per
section, tied to genre.
