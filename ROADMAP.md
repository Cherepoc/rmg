# Roadmap

Planned work that has been decided but not built yet.

## Next

In this order, each measured before it is planned:

1. **Listen and tune.** The sections' energy has been tuned by measurement only (see *Section dynamics*). Heard and
   kept: the endings, the section modes, the chords' level, the drums, the fills and their runs, the fades, and the
   intros of entries.
2. **The melody** (see *Melody at generation*): listen to the question and its answer and to the improvised
   appearances; then a bar of a later letter within the phrase, and a last chorus varied more than the second.

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

The melody is placed before `Realizer`, where the other tracks' notes are decided: once the song is put together (it
was once a section's bars were made, see below), one line places its notes in order, by the rules of `Line`, over the chords `Realizer` would work out
(`LinePattern.Place`), and every note keeps its scale step above its chord's root (`StateKinds.ScaleStep`), which
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

- **Varied repeats (A′) and conventionality** (open, pending listening): a repeated bar is varied by a flat chance of
  0.25 and a fixed strength (`PhraseSchemes.VariedRepeatChance`, `VariedRepeatVariation`), where the answer's amount
  leans. Leaning A′ too was not built: which way convention runs is unclear (a varied repeat is as conventional in
  songwriting as a literal loop is in electronic music, and what is unconventional here is rhythmic strangeness, not
  development); the scheme's choice already leans to more distinct bars in wild sections, so a lean of A′ the same way
  would push wild sections to no repetition and plain ones to loops; and its strength is what makes a varied repeat
  start as the first did, not a choice to lean. Should listening find plain sections too varied or wild ones too
  literal, lean its chance alone, gently. The answer's lean (more mutation the wilder) awaits the same listening. A′ needs no
  mutation of its melody: its cycles drawn afresh bring fresh beats, placed by the rules, so its second half keeps 30%
  of its first play's steps against 47% for a plain repeat (65% and 61% in the first half), over 100 corpus songs
  measured by the scale step above the chord's root, as a repeat over another chord plays a sequence; plain repeats
  are recognisable as they are. Gathering the answer's and A′'s values in one class was dropped: they are two
  mechanisms of two domains, the phrase scheme's and the melody's, that share only their lean.
- **A section as it recurs** (built): a song improvises its melody as its sections recur, by an amount of its own
  (`MelodyLayers.Improvisation`, drawn per song from `SongStream.MelodyImprovisation`, none in about half the songs,
  which play their sections' melodies as first heard, as riffs do, and up to 0.6 in the others, the more the less
  conventional the song's rhythm): a section's melody is kept unplaced (`SectionLine`) and placed afresh every time
  the section plays (`GeneratedSection.Appear`), its notes mutated from the first appearance, not from the one before,
  so that a chorus keeps its tune as a singer varies it, by the answer's mechanism (`LinePattern.Mutate`), keyed by
  the note key (`CompositionStateKinds.NoteKey`), so that a figure that comes back varies alike. A mutated note draws
  where it means to go afresh, its step and its turn, as it was drawn; a turn alone changed a recurring section's
  notes 7% of the time, where the rules placed it where it was. Over 100 corpus songs a recurring section plays its
  first appearance's notes 100% of the time in songs that do not improvise and 82% in songs that do; leaps, chord notes
  on the beat and the contour as before. Its rhythm improvises too, where a phrase varies (`MelodyLayers.AnswerBars`,
  its second half): the bars of a letter draw their rhythm afresh, alike, by the amount times
  `MelodyLayers.ImprovisedRhythm` (2), a draw per letter from the appearance's own sequence, as the answer's do by a key
  of their own (`PatternGenerator.BuildBars` from the seeds `DrawSeeds` drew, not drawn again; the trace paused while
  it is built again, `StateTrace.Pause`). A rhythm drawn afresh of the same settings keeps most of its onsets, so the
  improvised rhythm stays gentle: a later appearance keeps 100% of the onsets of a phrase's first half and 92% of its
  second (95% at a factor of 1), the same note on 86% and 72% of them, and 80% of its notes as a whole. A last chorus
  varied more than the second (an amount that grows by appearance) waits for listening.
- **An endless song** (later, low priority): a mode in which a song goes on with sections generated afresh as it
  plays, with no ending, a streaming form rather than a planned one. The sections generated per appearance are a step
  towards it; the form's plan, its intro and ending, and the song's single pass are not.
- **Question and answer** (built): a section plays its 4-bar pattern twice, and its melody as a question and its
  answer (`LinePattern.Answer`): placed as one line over the 8 bars, so that the answer goes on from the question,
  its first half the question's and its second half mutated a decision at a time, a note there drawing afresh where
  it means to go and playing no note heard before, by a chance of the section's (`MelodyLayers.AnswerAmount`,
  0.5, the likelier the less conventional, times 0.8 in the answer's changing bars, `MelodyLayers.AnswerBars`, since
  a mutated note draws its step afresh as well as its turn), from a sequence keyed by the note it echoes, so that notes that echo the
  same one mutate alike and no other draw moves; the other tracks play their pattern twice as before. An echo plays as
  it was heard over the same root, and as a sequence nearest the note before over another, where it moved to the note
  before over any root and, the melody placed on, fell out of its range to be clamped. Over 100 corpus songs the
  answer's first half plays the question's notes 98 to 100% of the time and its second half 68 to 72% (66% now); a phrase starts
  again with a leap 36% of the time (38% when it played the same), and a note leaps 6.3% of the time (4.7%), as what
  plays again over the same root now plays in its octave; chord notes on the beat 87%, and a bar follows the aim of
  its contour 0.38. Its rhythm answers too, each a decision of the answer's own sequence: a changing bar draws its
  rhythm afresh by the same chance times `MelodyLayers.AnswerRhythm`, the same settings on another rhythm, and the
  answer's phrase ends afresh by the same chance, other than the question's; the answer's third bar keeps 90% of the
  question's onsets and its last 80%, the same notes on 61% and 51% of them, where drawing the cycles afresh, as a
  varied repeat does, changed a melody's bar of mostly one cycle hardly at all.
- **The contour shapes the melody weakly:** within a 4-bar pattern a bar's mean pitch follows the register it aims at
  0.30 (`MelodyContourTest`, over 100 corpus songs), as the line turns towards the aim only past
  `LineProfile.RegisterPull` and otherwise goes on or turns back by its draw alone. A phrase's shape may start again
  every half phrase, a wave (`MelodyLayers.Periods`, 19% of the patterns, the more the less conventional the section's
  rhythm), and a note's draw of going on or turning back (`CompositionStateKinds.LineTurn`) can lean towards the aim
  (`MelodyLayers.AimOdds`), off at 1; with the octave still chosen per bar, odds of 32 moved the following only from
  0.14 to 0.20, so retune it now. The echoes mask the rest (without them 0.60), as they replay a note's step whatever
  the aim: an echo run's octave or transposition chosen towards the aim would let a repeated bar follow the arch as a
  sequence. The wave and the lean wait for this work.
- **Leading into chord changes** (built): the melody's last note before a chord change within a phrase bends, by a
  third at most and so never a leap, to a step from the note the next bar starts on, a note of its scale on a weak
  beat and of its chord on a strong one, where the section leads: a chance of its own (`MelodyLayers.Leading`, 0.5,
  spread 0.5 either way, so that some sections hardly lead, as a riff, and some lead most changes), drawn per bar of
  the 4-bar pattern, so that the answer's first half leads as the question does, and not at a phrase's end, whose
  held note is its cadence (`Line.Approach`, `LinePattern.Place`). An echo keeps its figure but for that note,
  as a riff turns into its next chord. Over 200 corpus songs the melody crosses a chord change by a step onto the new
  chord 42% of the time, against 31%, by a skip or leap 44% against 50%; within a phrase 50%, and 61% in the sections
  that lead most; leaps, chord notes on the beat and the contour as before. The bass now leads too (see *Bass*). Left:
  half the changes have no melody note in their last beat, a rest or a held note, where a pitched fill at a phrase line, or a counter-melody, would move, should the gaps
  sound empty.
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
listening. The rhythm layers' chances and the fills' rarer ones, which were multiplied by the chance scale and capped at
1, lean by their odds as every other chance does (`RhythmLayer.Lean`, `FillGenerator.GetChance`), the layers' by a
lean of their own (`RhythmLayers.ChanceLean`, 1.1), tuned so that a section is as busy as before at every
unconventionality: over 300 corpus songs (`RhythmBusynessReportTest`, the sections told apart by their melody's answer
amount), the drums play the same notes a bar, off the 8ths and off the 16ths as they did, within a point, from the
plainest sections (13.4% and 3.0% against 13.5% and 3.1%) to the wildest (31.4% and 18.1% against 30.3% and 18.0%),
where a lean of 1 left the wildest's tuplets at 16.4% and one of 1.25 made the plain ones plainer (10.8% off the 8ths). A
chord's root is whole scale steps (`StateKinds.ChordRoot`), the section's home and the progression's, where it was a
list of sevenths of the scale rounded back, and the chord root's and the chord note's walks, which no line reads since
the bass became one, are gone, their draws with them, which reshuffled the corpus: over 300 corpus songs its bands
moved either way, no more than their spread between runs (the wild sections' notes off the 16ths 13.5% against 10.1%,
the wildest's 15.7% against 18.1%). The drums' articulation offset stays a list: every layer's fraction of a drum's
sounds rounded to whole sounds each and added, so that a layer moves every note under it alike, which an additive
number rounded once would not. How a track plays its chord is its role's (`Realizer`), where an empty chord note
offset used to play the whole chord and two render states flagged the melody and the bass. The scale and a chord's shape are single values that are
lists, set by one layer each, whose kind would join two layers' into one; a test holds them to one
(`SingleValuedListsTest`), where a kind of its own for a value one layer sets would add a mechanism for a mistake not
made, and forbid the override a kind the lowest layer sets allows, should a section's scale ever override a song's.

Smaller, when the code is next touched:

- **Lines** (built): the melody and the bass are one kind of line (`Line`, `LinePattern`, `SectionLine`), what sets
  them apart held as data (`LineProfile`: `MelodyLayers.Line`, `BassLeadingLayers.Line`), placed last, once the song is
  put together and its fills made, over its whole chords (`LinePattern.Place` in `SongGenerator`), so that a line goes on
  from section to section and leads into the next section's chord; `Realizer` plays them as placed (`LinePlacement`: a
  scale step above the chord's root, and an `Alteration` a semitone off it). A section brings every appearance's bars
  unplaced (`SectionLine.Appear`), each note carrying its draws (`LineStep`, `LineTurn`, `NoteKey`, kept by the song as
  render state), how its bar leads out and lands (`LineApproach`: a scale step, a half step below or above the next
  root, its fifth or the root early, each aimed at where the next note was placed, so that no approach leaps an octave;
  `LineLanding`: the root, third or fifth nearest the note before), and a phrase's first note whether it starts afresh
  at its aim (`LineReset`, drawn by the line's freedom to change register, spread by the song and the section). A
  line's last note lands on the root (`SongFormGenerator.LandOnRoot`). The melody: its phrases start afresh by a
  freedom of 0.25, and it leans towards its aim (`AimOdds` 16); over 100 corpus songs, against placing it per section,
  it leaps into a section 21% of the time against 33%, into its last note 11% against 31%, and follows its phrase's
  contour 0.23 against 0.35, which a freedom of 1 brings back to 0.31 at 31% leaps into a section; the echoes hold it
  down. The bass: every note on a strong beat a note of the chord, a weak one moving along the scale, its range all of
  its track's, no contour, a freedom of 0, so that it never starts afresh and replays an echo nearest the note before,
  half the song's improvisation, its leading and landing as its bars draw them (`ChordApproach`, `ChordArrival`);
  `BassLine` and `BassPattern` retired. Against the bass before, over 100
  corpus songs (`BassLineReportTest`, `BassLeadingReportTest`, `BassArrivalReportTest`): it moves 2.7 semitones on
  average against 2.9, a fifth or more 6.4% of the time against 6.2%, an octave or more 0.3% against 0.1%, spans 21.9
  against 21.4; leads 14, 42 and 65% of its changes by its instrument, as before, a tension resolving by step 8, 17 and
  23% against 6, 17 and 26%; lands on the root 77 to 92% of the time, as before; a recurring section plays its first
  appearance's pitches 82% of the time against 90%. Its octave nearest the note before is its line's rule: placing
  every note in its chord's register instead leapt a fifth or more 35% of the time and an octave 18%. The bass's pickup
  is added where a bar asks to lead, as the bars are made, over the chord at its place, and kept only where the song
  put together has a new chord after it, the next section's too (`LinePickup`, `LinePattern.Place`). A phrase's
  start decides its echoes too (`PhraseStart`, `LinePhraseStart`): a phrase that starts afresh replays an echo over the
  root it was heard over where it was heard, and one that goes on replays it so too, unless that would leap from the
  note before, where it takes the octave nearest it; an echo's octave nearest the note before is within the range, so
  that a replayed tune moves by octaves rather than bending at the range's edge. Over 100 corpus songs, against echoes
  always where they were heard: the melody leaps where a phrase starts again 16% of the time against 31%, into a
  section 12% against 18%; a recurring section plays its first appearance's notes in some octave 95% of the time, the
  same notes 91% against 96%, and the answer's first half the question's in some octave 94%, the same notes 85%
  against 99%; chord notes on the beat 89% against 88%; its contour 0.18 against 0.21. Nearest the note before
  whatever the range, the same notes fell to 71%, a replayed figure bent at the range's edge into other notes. The melody
  leads into its phrase's first bar as into any other, the next section's too, where the phrase runs on into it: a held
  note, a phrase's cadence, never bends (`LinePattern.Place`), where the melody once never led into a phrase's first bar
  at all; few notes change, too few to move the measures above.
- **Note keys** are hashed seeds where a plain key of the bar pattern, the cycle and the place would do.
- **Memory:** every `RealizedNote` keeps the state it was decided from, which a song now holds on to (about 2 MB a
  song, 8 MB with a trace). Recompute it on demand instead, should memory matter.

## Drums

A drum plays a role in the groove (`DrumRole`): it grounds it (as the kick does), plays the backbeat (the snare), keeps
time (the hi-hat) or colours it (the toms, a crash ride, more percussion); the fills keep their own roles
(`FillDrumRole`), finer, by the sounds a run plays. A drum's affinity for every role is data on it, its main role the
heaviest: the kick and the snare steep, the percussion spread over several roles, the shakers keeping time. The song
draws a drum's role and a section may draw it again (`DrumRoles`), the lowest layer's winning, both leaning to the roles
not its main one the less conventional the rhythm; the role sets the drum's fixed rhythm (grounding repeats its figure,
the backbeat is a half-bar cycle shifted by half, time plays faster, full and steady), a section's other role as the
difference from the song's. Over 200 corpus songs the kick always grounds, the snares keep time in 2 to 4% of their
sections (a train beat, to listen to), the clap colours in 11%, the tambourine plays the backbeat in 28%, the conga
grounds in 53% and keeps time in 35%, the claves play the backbeat in 56%.

A section's kit is chosen by the roles (`DrumKitGenerator.SelectKit`): a lead for the ground and one for the backbeat,
and one to keep time 90% of the time, the more energy the likelier, each among the drums whose main role it is, by
weight, the loud ones likelier the more energy, one lead a role in place of the groups' rule that their drums do not
play together; what a lead plays is its role in the section, so that a wild section's snare may keep time in the
backbeat's place. Then the colour, none to two groups of it (the toms, the accents, the percussion), more the more
energy; and now and then, 19% of the sections, 8% of the quieter half and 29% of the louder, a drum doubles a lead of
its role (`PercussionInstrumentDefinition.Doubling`): the clap or the tambourine on the backbeat, a shaker or the
tambourine over the hi-hat or the ride, playing the lead's rhythm and its bar patterns on the lead's strong beats. A song
has an acoustic or an electric snare, and in 30% of the songs a clap, which mostly doubles it and now and then leads the
backbeat. The groups keep only the song's drums, the colour and the fills' roles. The triangle, the cuica and the
whistle are left out of the song's percussion, as they grate in a groove, until genres call for them. The vibraslap is
an accent, as a crash is: a fifth of the songs have it beside the cymbal, and it lands in about one cymbal landing in
eleven where the song has it and all but never grooves.

A drum's sounds are data (`DrumSound`): a weight, which runs and landings pick them by, so that the cymbal lands on a
crash four times in five where it landed on the china and the splash half the time, and a sound joins a run by its
weight against the heaviest; a loudness, which adds to its velocity (`VelocityLayers.SoundLevel`) and leans it by
energy; a stroke weight; and an accent. A drum walks its sounds from note to note, as the toms do their pitches, or
strikes one steadily, its stroke (`DrumStrokes`), as the hand drums do too, accenting the strong beats with
their other tone where they walked from tone to tone, a state the lowest layer that sets it decides
(`StateKinds.CreateLowestLayerWins`, by the depth every value carries): the song picks one, a section may change it and
a bar of a later letter may change the section's, a change the likelier the heavier the new stroke, the less
conventional the rhythm and the more its loudness goes the energy's way. The cross-stick is a stroke of the snare, no
longer a drum: 14% of the snare's sections play it, 20% of the quieter half and 7% of the louder, and 86 of 194 songs
switch to it and back. A note may play an accent over its stroke, at a note's depth (`DrumAccents`), leaned to the
beats it favours and by energy, and replayed in a repeated cycle: the hi-hat plays 85% closed, 6% its pedal and 9% open,
two thirds of those off the beat, where the three were about even; the ride's bell is 6% of its notes, nine in ten on
the beat, where it was 28%.

Drums by bar (`DrumPresence`): the section's first bar pattern, the phrase scheme's A, plays its drums as the section
does, and the bars of another letter vary them: a lead never sits out, as the hi-hat sitting out sounded as a dropout,
but may change its stroke there; a drum that colours the groove or doubles a lead sits out 30% of the time, from about
10% in the plainest sections to 60% in the wildest, less the more energy the section has, each drawn from a stream of
its own. Bars of the same letter play the same drums.

A song's drum setup (`DrumSetups`), drawn once and leaned to percussion the less conventional the song: most play the
drum kit alone (119 of 200 corpus songs); some the kit with one to three percussion drums (74), which colour it and may
switch to sections of percussion only and back (`PercussionSections`, by a chance of the song's lean around none and
the section's, leaned to unconventional and quiet sections, where the song has two or more); and a few percussion alone
(7), a drum for every role by its main role and one to three more, the hand percussion among them (a drum's family,
`DrumFamily`, the kit, the percussion or both), every section percussion only with up to four drums, and figures that
repeat more. Into a section of percussion only the drums land on the percussion, a run there plays the percussion
alone, and a run in a drum kit section does now and then (`FillLayers.PercussionRunChance`). The count-in clicks on the
first of a few dry sounds the song has (`FormLayers.CountInSounds`), the hi-hat's pedal first, or on its first drum.

Measured over 200 corpus songs (`DrumUseReportTest`): the timekeepers groove in 83% of the sections, the percussion in
18% (41% when every song had some), the toms in 13%, the accents in 6%; the drums change from the bar before in 28% of
the bars. 4.9% of the sections play percussion only, in 21 songs, the 7 percussion songs among them. A percussion
song's drums play as a kit song's: 13.6 notes a bar against 10.7, 30% of the bars as the one before against 35%, the
downbeat in 99% against 96%, both 2 and 4 in 60% against 63%. Listen to the percussion songs, and to the wild end,
where the kick or the snare in a role not theirs may sound broken rather than bold.

Later:

- **The clap's ghost notes,** the weak ranks of the backbeat, only after listening, as a clap there may sound busy.
- **Shakers over the hi-hat with figures of their own,** where doubling plays the lead's rhythm.
- **Drums by appearance:** a section's later appearance changing its drums, as an edit after assembly (see *Energy by
  appearance*).
- **Strokes before a lift:** the snare going from its cross-stick to its head in the last phrase before a louder
  section, an edit after assembly.

## Bass

- **Leading into chord changes** (built): a bar that leads (`StateKinds.ChordApproach`, drawn per bar of the 4-bar
  pattern by the bass's leading, from a stream of its own with as many draws whatever the chance) plays a note in its
  last beat where it has none before a change of chord: the note sounding there again, over the chord at its place and
  as lightly as the rhythm's weakest beat (`PatternGenerator.LeadIn`), for the bass's line to play the approach on. Before,
  a bar that led had a note to lead on a third of the time. Over 100 corpus songs (`BassLeadingReportTest`) the bass
  leads 64% of its changes with a walking bass (was 27%), 42% with an electric one (was 13%), and synth basses, at
  `Plain` 0.15 (was 0.3), 14% (was 10%), so that they sit on the roots; a tension resolving by step 27%, 16% and 6%
  (was 12%, 6% and 4%). The pickup always falls on the beat; the 8th before the bar line, leaned by convention, waits
  for listening.
- **Landing on the new chord** (built): what the bass plays on a new chord's first note is drawn from one list for
  every bass (`BassLeadingLayers.Arrivals`: the root 0.8, the third and the fifth 0.05 each, the figure's own note
  0.1), all but the root leaning unconventional by the section's rhythm, in place of a root chance that rose with the
  leading amount, so that synth basses, which lead least, landed on the root least. Over 100 corpus songs
  (`BassArrivalReportTest`, plain and wild sections told apart by the melody's answer amount) the bass plays the new
  root on 85 to 87% of a synth bass's changes (was 72 to 75%), 91% of an electric one's in plain sections and 84% in
  wild ones (was 83 and 88%), and 90 and 81% of a walking one's (was 89 and 90%).

## Chords

Chord shapes are pitch fractions snapped to the scale, picked from a table ordered by unconventionality, laid out by a
voicing step and led from chord to chord (see `ChordShapes`, `HarmonicUnconventionality`, `ChordVoicing`,
`Realizer.SnapChordToScale` and `VoiceLeader`). Still to do:

### Jitter on in-between heights

Heights between two qualities, such as the third, could move a little from chord to chord, so that a scale with more
notes than seven picks sometimes one quality and sometimes the other. Worth it once scales of other sizes than seven
exist; in a 7-note scale it changes nothing.

## Fills

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
- **Sounds by convention:** runs and landings weigh a drum's sounds by their weights (see *Drums*), the same in a plain
  section as in a wild one; the weights could lean by the section's conventionality, so plain sections crash and wild
  ones reach for the china and the splash.
- **The drummer's walks and busyness as state:** the fills' rarer choices and each drum
  group's chance of a run are layered state now, and a signature a song layer over them; the favourite walk, a choice
  among four, would need a pool like the chord pool's, and busyness, which weighs the spans and moves the fullness,
  would become a fullness and a span length that the section's energy adds to.

### A drum's sounds together

A drum is one track, whose note plays one sound, so a run's window plays one sound of a drum at a time: never two
toms together. Notes of several sounds on one track would allow it.

## Form

- **Fade-outs** (built): an ending (`EndingKind.Fade`, weight 0.15, leaning conventional, 9.5% of 200 corpus songs)
  that plays the last section once more, another appearance of it, improvised as far as the song improvises, with no
  final chord, and fades the band out over it: the form puts a song-wide render state on it (`StateKinds.Fade`, from
  1 down a step every `FormLayers.FadeStep`, a quarter beat), which `Render` carries as the song's fade and `Midi`
  writes as every channel's expression (CC 11), under the channel volume the web player locks for its mixer. Velocities
  would not do: they change how hard a note is struck, not how loud the band is, and leave held notes as they are. A
  fade of one section's pass is 12 to 16 seconds. Later, should listening ask: a ritardando into it, the drums fading
  first, or a tag after it.
- **Intros of entries** (built): in place of the drums first, the chords first and the build-up, each with its order
  set by hand, the band comes in part by part in an order drawn (`IntroKind.Entries`, `SongFormGenerator.DrawEntries`):
  the chords, the bass and the melody, and the drums by their role in the first section, so that the hi-hat and the
  kick can come in before the snare, each next part drawn by its weight, leaned by the song's rhythm
  (`FormLayers.IntroParts`: the time and the chords most often, the backbeat, the colour and the melody, 0.05, leaning
  unconventional), over a window (`FormLayers.IntroWindows`: 1, 2 or 4 bars of the first section's before it, or its
  first phrase), a drawn number of them, at least one and one fewer than all, spread evenly from its start, and the
  rest together at its end with a fill and a landing, so that the window never plays the whole band, which made a
  4-bar intro of the drums alone sound like a section of 12 bars. Drawn from a stream of its own (`SongStream.Intro`).
  Over 200 corpus songs, 120 intros of entries: the first part in is the chords 38 times, the time 27, the ground 25,
  the bass 18, the backbeat 11, the colour once; 53% of the parts come in inside the window; the melody 7 times (6%),
  which in bars of the intro's own plays the first section's opening before it starts again.
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
plays the note it had again, as the scale step from its chord's root (`LinePattern.GetNoteKey`, `Line`).
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

A grouped cycle, a number of 16ths that is no power of two, such as the dotted 8th's three (`ResolvedRhythm.IsGrouped`),
starts again every smallest power-of-two span that holds two of it (`RestartOf`), so that a dotted 8th plays 3+3+2 every
half bar, as a tresillo, and subdivides only as far as its halves stay on the 16ths (`GridRankLimit`), a dotted 8th's not
at all. Halved as any cycle is and cut off at the bar, it streamed dotted 32nds that crowded their last note onto the next
bar's first, a 64th apart, heard as a rush at every bar line (seed 162711917 at 1:15). Over 200 corpus songs the groove's
notes off the 16th and triplet grids fell from 18,910 to 6,147 (the quintuplets and septuplets), its gaps under a 32nd
from 286 to 3, and 666 bars play a tresillo twice; a grouped cycle plays fewer notes, 3% of the groove's in all. Left:

- **Timekeepers in grouped cycles, not leaned:** a drum keeping time plays a grouped cycle in 6.9% of the plainest
  sections' bar patterns, 9% of the middle and 23% of the wildest, as the other drums do, leaned already by the
  chances every rhythm layer scales; heard as a clean tresillo since the fix. Straightening it after the layers' sum
  would undo a decision rather than lean one, per bar pattern where a feel is the section's, and the tresillo keeping
  time is conventional in reggaeton, dancehall or afrobeats, so its convention is the genres' to say. Should it be
  wanted, first the rhythm layers' chances as leans (see *Chances multiplied*), then a role's lean on its drum's own
  layers, per section, tied to genre.
- **Splitting a grouped cycle by its number** (a dotted 8th into three 16ths, one strong and two weak), where the
  engine only halves: the 3-against-4 cross-rhythm as a full 16th stream, should the tresillo alone sound thin.
- **Cross-rhythms across the phrase,** a grouped cycle running on across the bars, need patterns longer than a bar
  (see *Long cycles*), as the unconventional reading of what the tresillo is the plain one.
