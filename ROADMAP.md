# Roadmap

What is to be done, what listening is to settle, the ideas parked and the decisions that keep what is built so. What
is built is described where it is built, in the code, and its numbers are in the commits that made it; an entry here
is removed once it is built.

## Priorities

**In order** (P1, the plan of 9 October 2026, from the full review of that day, every one of its 525 findings in one
of sixteen packages, each through the implementation loop, its low findings checked against the code before they are
fixed; the review and the plan are kept outside the repository):

1. Done: the four high findings: a shuffle genre's 8ths swung at every tempo (blues, jazz, country); the events of a
   day and the new genres of a day capped for everyone together; a version accepted only as this server's or older.
2. Done: loudness on the heard scale, a share of a velocity taken through the render's curve (`Render.ScaleLoudness`):
   hi-hat rolls growing from 60% of the hat as heard, drum marks' rolls, swells and strengths, gated chords' restrikes;
   a drum solo's time keeping keeping its accents.
3. Done: genre data and validation. A preset names every option of a chances choice the songs play (209 written,
   16 given back: half and double time in Rock, Country, Disco, Fusion, Ska and Soul, Soul's legato and key changes,
   Country's key changes, Fusion's trades); a value read at the song's facet it follows, its plain and wild ends
   heard; a pool weighing nothing refused; Funk's stale chugs out and a kit in; Latin's claves on the clave, one
   direction a song; the blues looping one 12-bar chorus; Drum and bass's breakbeat; Djent's kick with its riff;
   the band devices out of Orchestral, Medieval and Dungeon; Chiptune straight and drier; Ambient's drums rarer;
   louder forms and a raised cadence no longer leaking into Disco, Soul, Ska, Ambient, Hip hop, House, Reggae and
   Synthwave. Left: a dance track's ending stripping back to the drums, an ending of its own (*Form and sections*).
4. Done: stage interactions. Figures played in turn by what they follow, the kick with the riff as its figure plays
   it, the bass around the kick as its figure plays it, and none in a section running a polymeter, nor a polymeter in
   a breakdown; the riff's twin palm-muting, slurring and taking its louder or quieter form with its riff, and resting
   where the riff chugs; hat rolls never under a fill nor in stop time; drum marks before the drum stages, which push
   only drums keeping time; louder forms before the instruments, so a part sounding as another takes its form; stop
   time skipped steering no stage; the bass's walk out of stop time kept; a solo note taking no double stop of the
   melody's; a riser taking no articulation.
5. Done: musicality in the code. The blues' turnaround, V–IV–I–V in its last four bars (bar 10 on IV 8% → 71%, bar 11
   on I 2% → 77%); the meters other than 4/4: a phrase's end resting in a short bar, the line cliché on the meter's
   groups, a mutating groove keeping its backbeat in any meter, a steady figure (a double kick, a blast, tremolo,
   16ths) in any meter, Black metal's pinches 13 → 4 in 64 songs as its 3/4 riffs tremolo; stabs on the rhythm part, an
   octave bass alternating, strums down and up in turn, the bass one note at a time; ghosts on the snare's head; a
   vamp without passing changes; reharmonisation keeping chords in a scale of their own; a genre's drums kept by their
   weight once, the kicks pushing together; a forced landing never on the calls; the harmonica bending; Metal's snare
   tight on its kick; the ritardando over the last bar in any meter; a line's approach in its own scale; an open hat
   leaning to the offbeat by rank; a dominant pedal under a section's last phrase; soloists trading whole phrases, an
   accompanist comping between its turns; a drum solo's time keeping under the hands again. Left: the walking bass,
   measured first (*Harmony and lines*).
6. Done: generator correctness. A swing's pairs counted from each section, one in a meter that does not pair them
   straight; a line's approach in its own section's range; a mutating groove's swaps and trades keeping its anchors;
   a push only by the parts striking the change; a solo run going on from its motif; the drums' sends from their first
   note sent; a strong effect no part may carry not played; a slide from a note heard; an early landing where the fill
   cleared the groove; a varied repeat by the form facet, none in the plainest and every one in the wildest; a free
   form reusing its sections by the form facet; an odd play of a pattern placing its line; a genre's alone parts kept
   to; a percussion section counting the percussion it draws from; a doubling drum never silent; a solo's register a
   step inside its range; a third-less seventh named 7(no3); a filter's resonance and a sweep's pan restored after.
7. Done: stream robustness. A chance drawing at an end too; every line's fill, a section's every decision and every
   pitched track, a pinch and a dive by their note, key changes, octaves and dips by their place, and every appearance
   by its section and how often it played, each from a stream of its own; a soloist's draws whatever its instrument; a
   key named apart from a seed (`Seeds.Key`). The fingerprint over songs with values supplied, the deploy's over the
   page's genres, and a report over every preset's songs (`CorpusFingerprintTest.ReportGenres`). The new songs showed,
   and the step fixed: a soloing bass's run octaved, the kick's doubles left behind by its push and its polymeter, a
   ghost counted a downbeat, a line cliché through a chord a later note voices otherwise, marking drums soloing into
   silence, and a strum's turn rounded the wrong way.
8. Done: the server's robustness. A failing song logged with what it was given, the client told which song only; a
   request with a null inside answered 400; a request's body a megabyte at most; ratings and verdicts under a cap of
   their own; a listen without its seconds no longer breaking the summary; the analytics file shrunk after pruning;
   the visitors' secret written private and whole; the admin area gated however its path is written, its password
   compared by hash; a link's genre, a preset's or a kept one's, described with the song; a listening server refusing
   to run without analytics; a question's id kept short enough for its verdicts. Left: analytics written off the
   request (*Open work*).
9. Done: the page. The genre editor never losing a genre: one opened is one's own only where it is, a file imported
   saved beside it, Save as refusing a name it cannot take, the latest graphs drawn; the fine-tune window overriding
   nothing a click does not change, restoring the songs' own, a drag let go of when the browser breaks it off; Any
   genre's draw put back by a Stop; a mix changed while a song is on its way heard on it; the listening mode's song
   without its device leaving its stages out in its remakes, its download and its MP3, a verdict filed under the
   version heard; a download named as its link; the address rewritten once a slider rests; the dashboard showing the
   latest range asked for, its minutes rounded whole; the admin page clearing an old description; CSS on touch screens
   and the narrow layout.
10. Done: tests that cannot fail made to fail. Every collection compared in its order, the five sets among them said
    to be; every rule's loop counted; tests crashing on nothing guarded; each section's bars in its own meter;
    calibrations printed and bounded widely, their targets in the reports; rules checking what their names say. Left:
    a few tests that check less than their names (*Open work*).
11. Done: duplication removed, the fingerprint kept: Mod and Normalize shared, tracks found by their role, one program
    decoder, a meter's phrase length, the fills' spans on the draft, dead branches gone, silent defaults made explicit.
    The octave and the alternating bass kept apart, as a bass may play both and the presets name both. The small merges
    left then done after it: beats a second, a line's devices, the parts' states, a section's trace, the parts' labels,
    a line note's chord and pitch, a stream's place, a section's first rests, the drums' one group, the spreads and the
    server's nearest step; the MIDI tempo event rounded to the microsecond on the way. Kept apart: a triad's quality,
    as the generator's major, minor or other and the chord names' fuller quality cost as much to join as to keep, and
    the empty tracks and a span's notes replaced, one-liners a helper would not shorten. Left: the drum solo's
    parameters, with the song's decisions passed whole.
12. Done: performance, behind a profile (dotTrace, 32 songs): the decisions 45% of a song's time, the stages 50%,
    realizing 17%, no single hot spot. A chord's movement measured in loops, a meter's cycle templates and pulses
    kept, a position's section found by halving: 7.4 s to 6.4 s for the 32, the fingerprint kept. Left: the state
    maps every note's state is read from (*Open work*).
13. Done: test speed, 183 s to 165 s for the suite: the intro's genre songs made once for both its rules, a class's
    genre songs once, the corpus's renderings and trace read once, the songs a test makes twice made side by side, a
    duplicate test gone, four tests made to check what they say, a free form's kept parts tested. A cache of the
    songs with values supplied left out, as their thousands would not fit the tests' memory.
14. Done: page tests reliable, five runs in a row passing: the listening questions fetched beside the page, the
    tests serving their own (one patching a genre, one leaving out a stage, a question of its own for each test that
    gives verdicts), a song's end stamped as it ends, negative checks waiting past the remake, a failed CDN fetch tried
    again, the generator's tuned numbers read by their form.
15. Done: test structure: the tests ask a track's role, never its number, through one helper; their copies of
    production replaced by the production they copied (a note's program, a state's chord, a note's merged state), the
    layers' names as constants; tests checking what they say, loops counted, calibrations widened and printed; the
    listening measure, the media session and the pulse tested. Kept apart on purpose: the fill lines the tests state
    in their own words, as calling the fill generator would make its tests agree with it by construction.
16. Done: dead code and stale docs. Unused members gone, among them a reharmonisation's shift layer that only wrote
    zeros and a doubling's share that was always the same; every doc on the member it describes and saying what the
    code does; redundant tests and test helpers removed; the server's documented scales and parts as they are. One
    fix in passing: a pushed note held at most its own bar. All sixteen packages done.

**Done before it** (the plan of 8 October 2026 from the review of P3 and P4, each step through the implementation loop,
each device a genre's data on a mechanism there already where it can be, strong in its genres rather than a few
percent everywhere, and a listening question once deployed):

1. Done: a breakdown, a bridge or a free form's section in half time, the kick with the chugging riff, the lines and the
   pad out; Djent's 55% of songs, Metal's 42%, Nu metal's 41% (`breakdown` heard better, 8 October 2026).
2. Done: the riff as the song's signature, a song opening on its riff alone, the band coming in after it; Rock's 33%,
   Metal's 42% (`riff-intro` heard better, 8 October 2026). Between its sections, the part alone already (`GenreChoices.Alone`).
3. Done: a snare's ghost notes, soft 16ths around its backbeat, the same in a section's bars; Funk's 1.2 a bar,
   Soul's 0.9, Rock's 0.5 (`?listen=ghosts`), heard too loud at first and now 40% of the snare's velocity as rendered,
   about 40 against 85. Its drags left (*What the genres cannot play yet*). The hi-hat rolls ramp their loudness by
   scaling a velocity sum, as the ghosts first did, which the render's curve flattens: to check (`HatRolls.From`).
4. Done: the solo harmonised, two parts together a third apart, the second on the first's notes of an 8th and longer,
   as every solo played together now; Metal's 39% of its solos, Rock's 16% (`twin-solo` heard better, 8 October 2026).
5. Done: passing chords of a genre's kinds, the tritone substitute, a half-diminished chord a semitone below the next
   between roots a whole step apart, the next chord a semitone below, beside its dominant and a semitone above; Jazz's,
   Soul's, Bossa nova's and Orchestral's pools, kept though hard to tell apart by listening (3 better, 3 worse). A ii–V into a chord but the home left
   (*Approach chords*).
6. Done: the bass alternating its chord's root and fifth on its strong beats, Country's 73% of its sections, Folk's
   38% (`root-fifth` heard better, 8 October 2026); Jazz's walking quarters there already, its bass's rhythm in quarters leading into its chords.
7. Done: the kick and the crash pushing with the band, an eighth early; the drums playing a stop-time phrase's last
   half bar, a fill there as heavy as a section's, 1% to 21% of the phrases filled out of.
8. Done: a riff's weak notes on an open string's drone, the chugging riff's pedal without its chugs, heard worse and
   played by no preset now (*A riff's drone, reworked*); a driven guitar's quieter form, acoustic or clean in the quieter
   appearances, Black metal's (`?listen=quieter-form`; *Acoustic sections, fitted*). The montuno left, a pattern of its own (*What the genres cannot play yet*).
9. Done already: the instruments by listening are in the presets, the timpani, the organ bass, the church organ, the
   music box and the kalimba, Chiptune's squares; their weights by listening (*Instruments by listening*).
10. Done: a strum's up stroke missing a full chord's lowest string. Left: legato slides, by listening; a fill's toms by
    genre; a band leaning into a big entrance, whose tempo would know the dive's section; the riser over a filtered drop,
    put off with the riser as a lift cue, both wanting a channel of their own.
11. Let go of after its plan's review: a phrase extended before its cadence, 4 + 2, as an edit after assembly, would
    move every later place of the song's map, its lines, fills and stop time, its bars counting as its pattern's first:
    the 4-bar grid rewritten in all but name (*Phrases of other than four bars*).

Done in the plan of 7 October 2026: pinch harmonics; an ending in the song's own harmony; any General MIDI instrument
in any role; a sparse song's silent start; a groove that mutates; drum solos' feet and stickings; a filtered drop and a
whammy dive; Djent's gate on its pad; Techno's silent drop; stop time carried. A line's tensions beyond Djent let go of
by listening; a dive's ritardando deferred (*A band leaning into a big entrance*).

Done in the plan before it (written 7 October 2026): the echo as the synthesizer's delay; the strong effects stacked; the gate holding
whole chords; the part alone a genre's; metal's arpeggios and a part's colour; a line's tensions, Djent's synth over
its guitars; the brightness measured, the filter's floor raised; a part's tone; a tape's wow; the mix filtered; Trance
as it is produced; Djent's dips and stutter. After it: a Medieval intro silent on drums that only mark the form; the
mood of Black metal and IDM, a section's home a genre's; every genre's kit and articulations checked. Waiting on
listening: the rest of rock's and metal's, long cycles, more figures, rolls in 32nds, a riff's drone.

Done in the plan of 8 October 2026: the genres sorted by name; rock and metal measured and their presets tuned; a
section left to drums that only mark the form; the listening tool, *Listening* cut to its questions; a progression's
degrees leaned by a genre; the render reading the song's map, stop time decided once; the song's sound decided before
its notes; figures as a genre's data, the kick with the riff, the double kick, the gallop and the clave.

**Next** (P1, alongside): listening to what is deployed and tuning by what it says (see *Listening*, the page's
listening mode for the questions it asks), done for a version when every question is answered or let go of; and the
ratings by conventionality once enough are in, continuously and never as a gate.

**Later** (P2): the ratings report (see *Listening data*) once the dashboard shows songs rated enough.

**When the need shows** (P3), each by what listening says: the items left in the sections below, each marked P3, any
genre first (see *Genres*).

**Put off** until its uses ask for it: a tag (see *Form and sections*).

**From the review of 8 October 2026** (P2, to be ordered into the next plan): the architecture's items (see
*Architecture*: plainer prose); and the music's: the
songs' own appoggiaturas (*Fills and lines*); and targets from outside (*Listening data*).

**Parked** (P4): see *Parked*, kept for their reasons, not planned; and, parked where they stand: scales of other
sizes and a melody in block harmony.

## Listening

What listening is to settle: each a question, its measure, what it decides, and where the page's listening mode asks
it, `?listen=` its id on the test instance (`ListeningQuestionsTest` writes them, the dashboard tables the verdicts).
Done for a version when every question is answered or let go of. A question's songs are those of the corpus's first 24 that play its
device where it is heard the most, by how much its song with the device and without differ as heard
(`HeardDifference`, the notes' loudness that differs and the moments its effects do), from where they differ the most;
its song without lays its patch over the genre's options, the others kept (`withoutDevice`). Asked again since:
`tape`, whose song without had lost every other effect of its genre's, and `stop-time`; asked anew, `inversion`, the
melody's figures inverted (Pop), and `trade`. The least heard by their nature, short devices in a long song:
`lift-cues`, under 0.1.

Answered on 8 October 2026, late, and let go of: the clave, stop time and the ghost notes, heard as great; the tape and
the tone, barely heard, so a tape's wow now drifts by the sound facet, 5 cents at the plain end to 26 at the wild, and a
part's tone closes 24 steps at its deepest rather than 16; the rolling bass, whose sections were heard without a kick,
so a figure around the kick plays only in a bar the kick plays in; Black metal's endings, kept as they are; and a wah
heard in Black metal, its mix filtered by the songs' chances, which it names none of now.

Answered on 8 October 2026, the evening, and let go of, each heard better in 4 to 6 of its 6 songs: pushes, the drums'
marks, louder forms, ornaments, the pocket, the solo's answers, the lift's cues (as often the same), doubling, the kick
with the riff, the double kick, the gallop, the tremolo, the blast, the echo, metal's and Djent's arpeggios, the mix
filter, the trade, the breakdown, the riff intro, the twin solo and the bass's root and fifth. Asked again: the rolling
bass, heard worse in 5 of 6, over six other songs; kept: the clave, the tone, Black metal's ending and Jazz's passing
chords, split.

Answered on 8 October 2026, with the songs picked by how much they differ, and let go of: `pinch`, a highlight in 6 of
6; `pinch-sound`, General MIDI's harmonics over the squeal in 6 of 6, 8 of 12 in all, so the harmonics are the songs'
sound and the squeal a genre's alone; `dive`, a launch in 5 of 6, 8 of 12 in all, its ritardando still to be considered; `drum-solo`, the hi-hat's pedal
under the stickings in 6 of 6; `mutation`, an evolving break in 4 of 6; `breakbeat`, a dusty break in 9 of 12; and
`djent-tension`, 3 better, 4 worse and 5 not told apart of 12, its tension kept to Djent. Verdicts are counted by
version and the test instance keeps the live one, so a question asked again counts its earlier verdicts too until a
live deploy.

### The song and its sections

- **How plain by default:** drawn songs centred at 0.5 (`Unconventionality.BaseCentre`), 5% plain in every convention
  (`ConventionalityLevelsReportTest`): too adventurous for songs of no genre? Decides a centre of 0.375, or not.
- **The forms:** verses and choruses heard as such, the chorus lifting; sections of 80 bars at the median; an intro or
  outro of its own material (a third and a seventh of middling songs) the same song; a last chorus twice (17%). Decides
  the forms' weights and the last repeat's chance.
- **The sections' energy:** quieter sections heard as sections, louder ones lifting; a last appearance 0.10 louder
  than the first; fills into a quieter section 54% of the time. Decides the energy's contrast and the fills' landings.
- **Rests, entries and textures:** breakdowns and melody-less sections as arrangement or parts gone missing; parts held
  back at the start (15-18%); one part alone or parts dropping out, leaned to the quieter sections (`TextureTest`).
  Decides the textures' weights and their lean.
- **The lift** (`lift-cues` heard better, 8 October 2026): a crescendo of 8 of 127 into a louder section, and the drums' cues, 2.6 a song.
  Decides whether the cues stay, and how often.
- **Key changes:** two a song in the fourth fifth; prepared ones, pivots, pedals, sequences heard. Decides their
  chances and devices.
- **Tempo:** a section's own tempo eased in or taken at once; 6/8 counted in quarters; 13/16 too busy? Decides the
  easing and the odd meters' weights.
- **Half and double time:** double time's 22 drum notes a bar a lift or a scramble? Decides its weight.
- **The fade-outs:** a ritardando into the fade, the drums fading first, a tag after it? Decides whether to build one.
- **The settings panel:** a song made again with changed settings sounding moved rather than jumped; the pad in 80% of
  songs, the counter-melody in 60%, the drums in 95%. Decides the parts' chances.

### Harmony and lines

- **The section modes:** a mode change prepared by its cadence; the parallel and harmonic minor heard (seeds 196, 10,
  36, 171). Decides the modes' weights.
- **Harmonic rhythm:** a chord every half bar busy at 175 bpm, one every two bars static at 90? Decides leaning the
  span by its length in seconds.
- **Phrases:** question and answer, varied repeats (A′ at 0.25 in a middling form, none in the plainest and every one in the wildest, `PhraseSchemes.VariedRepeatChance`), the melody's
  leaps at a phrase's start (a third), the sequences rising with the phrase. Decides their chances.
- **The gaps before chord changes:** half the changes with no melody in their last beat, the solo part answering now
  (`solo-answers` heard better, 8 October 2026). Decides filling them.
- **The walks into a section:** the melody at 15% of changes, the bass at half of the runs. Decides their chances.
- **Double stops and the riff's chords:** the melody's on a chord instrument's downbeats in a quarter of sections.
  Decides their chance.
- **Borrowed and applied chords:** 7-12% of chords leaving the scale (`ChordScalesReportTest`); a held melody note
  clashing, an applied dominant's resolution spoiled? Decides their chances.
- **The bass's pickup** always on the beat: square? Decides an 8th before the bar line, leaned by convention.
- **A walking bass** (from the review of 9 October 2026): its weak quarters keep to the chord's notes, so a ii–V may
  walk arpeggios rather than a line. A report of the share of Jazz's bass notes on beats 2 and 4 outside the chord
  (about 30-50% in a walk) decides letting them take scale steps and chromatic passing notes where a genre leans its
  leading high.
- **Octaves and twins** (`doubling` heard better, 8 October 2026): a line in octaves in 10-20% of appearances, the riff's twin in 15% of riff
  songs, twin leads a third apart. Decides their chances and intervals.

### Solos

- **Runs and phrases:** a player's or a scale exercise; cells of five or seven; the intensity's arc; pauses in 17-20%
  of segments; 2.1 notes a beat at the middle. Decides the runs' cells and the solo's density.
- **Expression:** 59% of a middling solo's longer notes with a vibrato, 16% bent, 3% wide; 85% of guitar and synth
  solos bending. Decides the soloists' leans.
- **Loudness:** a soloist 0.06 of velocity over the band's loudest part, a flute too? Decides the margin.
- **Chords in solos:** 41% of a chord soloist's held notes stacked, planing 17% at the middle and 59% wild. Decides
  their rates.
- **Drum solos:** stickings round the kit; a crash with the kick in 23% of segments. Decides the drum solo's gestures.
- **Trading with the drums** (`trade` heard better, 8 October 2026): Jazz's solos trading with the drums by turns, the band laying out. Decides
  the trade's chance by genre, and whether the band keeps time under the drums' turns instead.

### Drums and groove

- **The drums:** the percussion songs; the wild end's kick or snare in a role not theirs; bound drums at 0.15.
  Decides the roles' wild weights and the binding chance.
- **The fills:** wild songs' 1.8 times the tuplets; three runs in ten at quarters or slower. Decides aiming a run's
  finest rank at the tempo's finest.
- **Repeated cycles** replaying their accents: mechanical? Decides a fresh loudness draw over them.
- **Swing and panning:** a triplet's swing at 16ths; wild tuplets swung; the chords' pan. Decides the swing's amounts
  and the pans.
- **Drum pieces given:** both snares doubling the backbeat; a piece put in by its roles alone. Decides giving the
  snare group one snare.
- **Every drum heard** (`DrumCoverage`): a drum landing once, placed or tacked on? Decides its landing.
- **The drums' marks** (`drum-marks` heard better, 8 October 2026): a strike every three bars and a swell into a section. Decides the marks'
  rate and the swell's length.
- **The ornaments** (`ornaments` heard better, 8 October 2026): one in eight to ten long notes, 2.5% of Folk's and Medieval's lines; a trill
  up to 1.5 s. Decides their rate and the trill's length.
- **The polymeters:** the snare on every cell's start; the chords joining a cycle. Decides who joins a cycle.
- **The pocket** (`pocket` heard better, 8 October 2026): the backbeat a little late, the bass a touch ahead, the lead laid back. Decides
  the offsets.
- **The figures** (`kick-lock`, `double-kick` and `gallop` heard better, 8 October 2026; `?listen=clave`): Metal's sections 27% with the riff, 14% a
  double kick, 15% galloping, Nu metal's 42% with the riff (`RockAndMetalReportTest`), Latin's claves on the clave.
  Decides their weights and leans.
- **Pushes** (`pushes` heard better, 8 October 2026): the band's changes an eighth early. Decides their chances by genre.
- **Stop time carried** (`?listen=stop-time`): the band hitting once a bar under the lines, the drums keeping time
  going on where the lines rest (Blues and Rock, none of 700 stopped bars silent past its hit, from 16%). Decides its
  chance, and whether the drums keeping time is the band's device or the band should stop with them.

### Sound

- **The velocity:** the bass's and the chords' evenness; bar layers adding 3 to 5. Decides the layers' spread.
- **The echo as the synthesizer's delay** (`echo` heard better, 8 October 2026): the Roland GS delay's tone in the player, its feedback
  (100, about half), its darkness (2 of 7), the send (100), and a dub snare's quiet strokes echoing with its backbeat,
  the delay's send being a drum's, not a stroke's. Decides those settings, or the echo's notes back.
- **Metal's arpeggios** (`metal-arpeggio` and `djent-arpeggio` heard better, 8 October 2026, `MetalArpeggiosReportTest`): a third
  of Metal's chord parts' sections broken, the pedal point the commonest, 1.7 notes a beat, its colours dissonant 17%;
  Djent's 79%. Ringing over the riff or muddying it; the rhythm part resting less where the heavy guitars rest, should
  the arpeggio be lost under them. Decides the colours' and the patterns' weights.
- **The strong effects stacked** (`ExpressionTest.ReportStacked`): Trance 56% of its songs with two or more, a pump
  and a gate or an opening; the drawn songs 5%. A wall of effects, or a produced track? Decides the shares beside the
  first, and a genre's weights.
- **The effects:** 1.9 gentle kinds a song at the middle, a strong one in 25% (`ExpressionTest.Report`). Decides the
  song's weights.
- **Bends, vibratos and slides:** slides 44% of the pitch shapes. Decides their rates.
- **The instruments added in October 2026** (about 4% of parts): any too loud or strange in either soundfont? Decides
  their weights and traits.
- **The riser into a drop:** a soundfont's sample shorter or longer than the bar. Decides starting it later.
- **The filter:** wobble, squelch and opening, Techno 11%, Trance 28%, Dnb 26% (`FilterTest.Report`); a General MIDI
  synth bass wobbling thin? Decides its depth and resonance.
- **Double-tracking:** Metal 2.8 parts a song at the sides (`DoubleTrackingTest.Report`): a wall, or mud? Decides the
  delay and which parts stay single.
- **The dub echo, the insertion effect and the rooms** (`DubEchoTest`, `InsertionTest.Report`): echoes as notes thin,
  the wah's and phaser's settings, the rooms wet enough? Decides the GS delay and the effects' settings.
- **Louder forms** (`louder-forms` heard better, 8 October 2026): a clean guitar turned driven when loud. Decides its chance by genre.
- **Rock's rhythm guitar** clean (53%) or muted (20%) beside driven chords and riff: a contrast, or wanted driven?
  Decides the exclusion's lean.
- **The bass under E1** (9% of its notes, `RegisterReportTest`). Decides the bass's range.
- **Instrument switches** within the family, 58% of a middling song's. Decides their families.

### Genres

- **The presets against their sentences** (`GenrePresetsReportTest`, `GenreDevicesReportTest`,
  `GenreIdiomsReportTest`): Pop's and Metal's beat without groove hold; Jazz's ride's time figure; Latin's and
  Metal's solos (66%, 91%); Blues' twelve bars loose enough. Decides each preset's choices.
- **The presets made in October 2026:** Orchestral's and Pop's crescendos; Funk's, Techno's, House's and Djent's vamps
  (30-66% of sections); Djent 70% polymetric; Nu metal's loud sections driven (97%); Medieval's and Dungeon synth's
  drums marking the form. Decides each preset's choices.
- **The dark presets' mood** (`MoodReportTest`): still happy? Decides their degrees leaned (`rootDegree`).
- **Rock's and Metal's degrees** (`RockAndMetalReportTest`): Rock's ♭VII–I in 45% of sections, Metal's ♭VI in 19% of its
  chords; a rock song's I–♭VII–IV loop, which the progression's home, away, preparation and cadence come near only
  through the cadence? Decides a loop's template of its own.
- **Rock and metal against their bands** (`RockAndMetalReportTest`): the organ's solos, the shuffle, Metal's clean
  quiet sections. Decides the presets' weights.
- **The wild ends** (`EveryPreset_ChoicesByEnd`): the wildest Rock 34% in 4/4, the wildest Jazz 56% swung; a wild
  song's homes plainer than its chords. Decides raising the experimental shapes' amounts.
- **How the wild end swings:** as often as a middling song (`Groove.Generate`). Decides a heavier swing at the wild end.

## Listening data

A song's version (`VERSION`, `SongsVersion`) is stored with every analytics event about a song, with its settings
(`SongSettings`) and its rating; the dashboard shows the listening and the ratings by version and by fifths of how
plain or wild the songs are.

- **The ratings report** (P2, once the dashboard shows a few hundred songs rated in one version): an export of a
  version's ratings and time listened (seed, rating, seconds), behind the dashboard's token, and an explicit report
  test that reads it, keeps the seeds of the build's own version, generates them again with the trace and holds the
  ratings against its values (`StateTraceEntry.Value`): which endings, intros, drum setups, modes, tempos, energies or
  improvisations are liked or skipped, with their counts, so that a small sample shows itself. Measured by: ratings
  per version, enough to tell two versions apart.
- **Targets from outside** (P2, raised by the review of October 2026, as listening is one listener's and slow): a few
  measures (the notes a bar by role, the intervals, the syncopation, the chord changes a bar) of a corpus of real MIDI
  songs to tune towards.

## Open work

Each item with its plan, and what it waits for.

### Conventionality

- **A song's length, the rest:** a short length runs long, 16% of the songs given a minute and 19% of those given two
  past 15% over (`SongLengthReportTest`), the intro's bars taken in (`SongFormGenerator.IntroDuration`) but an intro
  section, an outro or the ending's bars adding what the length leans no draw of; their chances leaned by the length
  as the form's are, should it matter. Long songs, ten minutes, are free forms seven times in ten, as no form reaches
  them: heard as rambling? Solos filling a long length rather than more parts, if so.
- **Listening to the drum solos' players:** a soloist is drawn evenly among the kit's drummer and the percussion
  drums of two sounds or more, so that in a song with percussion a percussion drum solos in 68% of the drum solos and
  without the kit in 64% (`DrumSoloTest.Report`); the kit weighed more, should listening miss the drummer. A player's
  turn traded away rests, its feet aside. A hi-hat's foot on the backbeats under the kit's solo only if listening asks
  for it, a pattern of its own.
- **More modes for the wild end** and **a song's own coherence,** how far its facets stray from its base, by the same
  rule (`ByConvention`), should the wild end sound samey or a song incoherent.
- **The plain end's rigidity,** to tune by listening: no change of feel, triplet fills included, every bar of a plain
  song at the same density.

### Form and sections

- **Breakdowns beyond Djent** (P2, suggested 8 October 2026 after listening: Djent's bridge in half time, the kick
  following the chugs, heard as great): Metal's and Nu metal's already break down (42% and 41% of their songs); more of
  Nu metal's, and the other heavy genres', Punk's hardcore end and Black metal's slower passages, each a genre's
  `breakdown` chance, and a breakdown in a verse or before the last chorus beside the bridge, should more be wanted.
- **The bass with the riff, by convention** (P2, suggested 8 October 2026): the bass doubling the riff in 35% of
  sections whatever the song (`LineDoubling.RiffChance`, alike at every end, as neither plainer nor stranger); a plain
  band's bass plays the riff with it always and a wild one's goes its own way, so the chance from 1 at the plain end,
  through about 0.5, to near 0 at the wild, by the melody or the groove facet, a genre's `doubling` "riff" still above
  it where named; measured by the sections with a riff whose bass doubles it, by fifths of conventionality.
- **Quiet drums in calm genres** (P2, heard 8 October 2026 on `drum-marks`): the drums marking the form strike
  too loud for a calm piece, and any drum in Ambient or Dungeon should stay soft. The marks' strikes and swells well
  under the parts they colour (`DrumMarks`), and a genre's drums' loudness as its data, a level of the drum kit's
  against the band's, Ambient's and Dungeon's low, set through the render's velocity curve (`Render.FromMidiVelocity`)
  rather than by scaling a velocity's sum; measured by the drums' velocities as rendered against the pitched parts'.
  And the kit that marks the form (suggested the same day): never the standard, room or power kit, a band's, but the
  orchestral kit, its timpani and its bass drum, or the percussion, perhaps the brush or the electronic kit too; the
  kit a drum job's choice (`DrumMarks.Jobs`, `GenreChoices` "kit"), Ambient's and Dungeon's marks on the orchestra.
- **A double kick with a backbeat** (P2, heard 8 October 2026 on `double-kick`): a double kick under no snare
  is heard as a drum machine where it goes on long; a section's double kick without a backbeat for a phrase at most,
  the snare joining for its second half, on the backbeat or the riff's accents, as a thrash drummer brings it in; and
  the snare's own sound there, never the cross-stick, as in a breakdown (*A breakdown's snare*): a heavy figure's
  backbeat the snare's head (`DrumSounds`, `Figures`). Measured by the bars a double kick plays without a snare struck.
- **Ghost notes in more genres** (P2, suggested 8 October 2026, the ghosts heard as great): Funk, Soul, Fusion, Blues,
  Hip hop, Rock and Disco ghost now (`GenreChoices.Ghosts`); Drum and bass's breaks, Reggae's one drop, Ska, R&B, a
  jazz drummer's comping on the snare and Latin's timbales would too, each its chance by listening to the preset.
- **A riff's drone, reworked** (P2, heard 8 October 2026: a good idea for metal, but the songs sounded better without
  it, so no preset plays it now; `GenreChoices.Riff` "drone" kept): the riff's weak notes all on its chord's root at the
  bottom of its range, 69% of them, is a pedal, not a drone riff; a black metal or a folk metal riff alternates its
  melody with an open string, the string the key's own (its tonic or fifth, not every chord's root), in a pattern of
  the riff's own (every other note, or the 16ths between its melody's 8ths), the melody on the strong notes over it.
- **Acoustic sections, fitted** (P2, heard 8 October 2026: acoustic sections are great for metal and rock, but not every
  quieter section fits one): a driven guitar turned acoustic only where no other part plays a heavy riff, the riff, its
  twin and the rhythm all turning together or the section left as it is, rather than one part at a time; and the
  acoustic guitar breaking its chords there (`Arpeggios`), as a band's quiet passage is picked; for Metal and Rock
  beside Black metal, whose quieter form plays 36% of its songs now (`?listen=quieter-form`).
- **A breakdown's snare** (P2, suggested 8 October 2026 after listening): Djent's breakdown plays its backbeat on the
  cross-stick, where a breakdown wants the snare itself, struck on the riff's accents with the kick or on the song's
  backbeat in half time, its 3 or its 2 and 4; the snare's sound in a breakdown the full one (`DrumSounds`), and its
  hits a figure of the section's, either on the accents or the backbeat.
- **A dance track's ending stripped back** (P3, from the review of 9 October 2026): House, Techno and Trance end
  by fading, opening or stopping; a DJ's ending drops the parts out one by one to the drums alone, an ending of its own
  beside the fade, the reverse of an intro of entries.
- **A riser over a filtered drop** (P3, by listening, October 2026): a reverse cymbal swelling over the muffled bar
  before a filtered drop, as the silent one has, which wants a channel of its own as the band plays on there (*A riser as
  a lift cue*, under *Genres*).
- **A tag** (put off): half a pattern before a section changes, which breaks the pattern's grid for the fills' lines and
  the phrases; with the meter as state, an engine's change for a rare idiom.
- **The energy's recurrence** counts a section's appearances, not its plays: should long sections sound too quiet.
- **A bar of two beats before a section,** which would make the bars of a section unlike each other; and swing by a
  section's own meter, which the song's meter decides now.
- **The chords and the bass,** which follow the energy weakly, should choruses not sound fuller; the melody sings a
  section's energy over the song's in its range's place (`MelodyLayers.EnergyRegister`), the chorus 2.6 semitones over
  the verse, higher in 61% of the songs.
- **The band's devices, the rest** (P3, each an edit after assembly among the stages, as stop time and pushes are):
  stop time's hits added where a part rests on the downbeat; a backing figure that recurs through the song. Call and response between
  parts (postponed October 2026): an edit after the notes keeping the counter-melody to the melody's rests kept 12% to
  15% of its notes, the melody leaving no room; the counter-melody's rhythm placed against the melody's gaps when it is
  drawn, as the solo part's answers are (`SoloAnswers`), is the way, should listening ask.
- **Harmony's devices, the rest** (P3): a ii–V into a chord but the home, which a chord's own scale makes part of; the
  line cliché rising (the fifth, its sharp, the sixth) beside its falling one (`InnerLines`), should
  listening ask.
- **A section's parts chosen as its drum kit is** (`DrumKitGenerator.SelectKit`), leading and colouring roles, where
  now each part rests on its own (`Arrangement`).
- **The drums' changes by an appearance's energy** (their colour, a drum's stroke), which are the section's as it was
  made.

### Fills and lines

- **The melody's motif developed, the rest** (P3; the inversion kept, heard 8 October 2026 as hard to notice, worse in 5
  of 6 Pop songs, a device of development rather than one to be heard at once): a run of echoes plays its figure
  inverted now and then (`LinePattern.Inversions`, 15% in the middle, 40% at the wild end); a figure displaced by an 8th or its rhythm
  stretched, should the inverted ones be heard as development rather than wrong notes.
- **The songs' own appoggiaturas** (P2): a line's note on a strong beat a tension
  that resolves by step to the chord, the songs' chance 0 at the plain end and low in the middle (`LinePattern.Tensions`
  is a genre's alone now, 0 for the songs); a genre's tension (`GenreChoices.Tension`) stays as it is.
- **Filling the melody's gaps before a chord change within a phrase,** should they sound empty now that the solo part
  answers.
- **The lift beyond the cues:** the chords' rhythm doubling, a part of the next section coming in early, should the
  crescendo and the drums' cues not lift.

### Instruments and expression

- **A held riff restruck outside a gate** (P3, by listening): a decaying chord is struck again on its bar lines only
  where the gate holds it (`GatedChords.Hold`); a rock band's riff held over a bar restruck the same way, should a held
  guitar chord dying out be heard elsewhere.
- **A band leaning into a big entrance** (P3, deferred from the dive, October 2026; the dive itself heard as a launch):
  the tempo easing a little over the last bar before a louder section now and then, a ritardando back to tempo on its
  downbeat, where a riff dives into it. The tempo is decided before the notes (`SectionTempo`) and the dive after them
  (`Expression`), so the dive's section would be decided with the tempo.
- **The playing following the articulation** (P3; palm-muted chugs done): octaves popped in slap, short notes muted,
  without which an articulation is only another sound.
- **Plain doubling,** a part doubling another in unison, now that the parts play by their pocket (`Pocket`).
- **A motif stretched,** its rhythm doubled, beside moved and inverted (`Soloist.MotifInversion`), should listening ask.
- **A solo's runs in fives and sevens,** a fusion player's grouping, which the feels give a whole part but perhaps not
  a run; if not, a case of segments in any pattern (see *Parked*), not a mechanism of its own.
- **Legato slides** (P3, a case of legato): a slide strikes its note anew, where a player's note before goes on
  sounding into the next; holding the note before into the slide and leaving the next unstruck edits two notes at
  render. Built if the struck slide sounds like a stutter.
- **A filter drawn over time** (P3, if a wobble's restart on every note is heard): a part's brightness drawn through
  its rests rather than while its notes sound, and the filter's shapes a vocabulary as data anchored where they mean
  something; the part's one curve now adds its tone and a filter's closing note by note (`ExpressionRender`).
- **Drum solos rethought, the rest** (P2, planned October 2026; `drum-solo` heard better): a solo now plays the pitched solos' gestures with the kit as a
  scale, a run round the toms, a lick, a crash held, a pause, a quote of the groove, the kick keeping time under it.
  A drummer thinks otherwise; in order of what it would add:
  1. Done October 2026: its feet a genre's ostinato, the groove's kick, the hi-hat's pedal on 2 and 4, a double kick, a
     bossa's kick (`DrumSolos.Feet`); a samba's or a baião's kick and a timbales player's cascara left, by listening.
  2. Done October 2026: its runs and licks in stickings, singles, doubles, paradiddles, six-stroke rolls, hands and foot
     in triplets, the right hand round the toms, the left on the snare (`DrumSolos.Stickings`); flams and drags left.
  3. Done October 2026: a phrase's first run or lick restating the phrase before's, its sticking and speed, a drum
     round the kit, displaced by a 16th now and then, 38% of the kit's phrases (`DrumSolos.MotifChance`); augmented or
     diminished left, by listening.
  4. Done October 2026, measured: its runs' and licks' hits a beat and loudness follow the intensity, 3.6, 6.2 and 8.1
     hits a beat at 0.37, 0.44 and 0.55 by its thirds, and the intensity grows over the solo, 0.53, 0.61, 0.64.
  5. Done October 2026: a pitched solo trading with the drums turn by turn, the band laying out for the drums' turns
     (`Solos.Trade`), Jazz's 30% to 60% of its solos, 112 trades in 64 Jazz songs.
  6. Next, the hi-hat's pedal under Jazz's stickings heard as a drummer in 8 of 12 songs and never worse: a genre's
     idioms as data, metal's double-kick runs and tom cascades, a timbales' abanico, funk's ghosted groove solo, an
     IDM's drill of ratchets, each a vocabulary of its own over the feet's patterns and the stickings, which are a
     genre's already.
  Measured by the solos' notes a beat over their arc, the limbs' share, the stickings drawn; `?listen=` questions.
- **A drum's pocket by its section's role** (P3, if a section's change of role is heard): the timing takes a drum's
  offset by its role in the song's groove (`Pocket.OffsetOf`), not a section's, which may move a ride to the time.

### Scales and chords

- **Free chords, the rest** (P3): a chord moved by semitones (`StateKinds.ChordShift`), planing (`Planing`) and a
  chord's own scale on its root (`ChordScaleKind.Own`) are built. Left: planing choosing the next chord by the voice
  leader's least move among free shapes, should it sound mechanical; the diminished scale, of eight notes, which the
  seven-note scales cannot hold. Its uses past these: a fusion genre, a long-lined guitar player's, its harmony and
  lines as a genre's data (free chords, melodic minor, octatonic and whole-tone line scales, leaping, stretched
  voicings, swells, odd meters); not its tone, which General MIDI has not, its long legato soloing, which the solo's
  gestures (licks, runs, held notes) are not built for, its drumming and its melodic bass, gaps of their own.
- **A section holding its spacing** rather than the voice leader drawing it anew a chord, should a spread chord's
  (`ChordVoicing`, "Spread") neighbours sound close.
- **Approach chords, the rest** (P3): turnarounds, passing chords, a chord solo's planed approaches, chromatic passing
  chords from above and a later appearance reharmonised are built. A plain song is never reharmonised, by any of them,
  the songs' chance 0 at the plain end; a preset's may be above it, as Soul's and Bossa nova's turnarounds. Passing chords of a genre's kinds are built too (`PassingChords.Kinds`): the
  tritone substitute, a half-diminished chord a semitone below, the next a semitone above or below. Left: a ii–V into a
  chord but the home, two chords passing where one does now; a chord solo's approaches on the strong beats at the wild
  end, and the dominant as its approach; the tritone substitute for a progression's own V7 in a reharmonised
  appearance. Today a chord solo stacks the band's chord or planes its shape, never another chord.
- **A melody in block harmony** (P4, parked): the counter-melody or the pad voicing its long notes, approaches on its
  passing ones, as a horn section's soli.
- **Scales of other sizes** (P4, parked again after a trial, 8 October 2026): a minor pentatonic forced on 64 songs
  fails at once (`Progressions.GetCadenceWeights`); seven steps are assumed in 13 files, 28 places, and the
  progressions' motions, degrees and cadences, which genres name, are tables of seven steps. The blues' harmony it was
  reopened for is there already: Blues plays every chord its dominant (`chordScales`), in sevenths and ninths, under
  blues and pentatonic lines. What it would add is harmony inside a pentatonic or a whole-tone scale, quartal chords at
  the wild end, for a redesign of the progressions in semitones. The plan when taken up: the steps counted by the
  scale's length, a fifth the step nearest seven semitones, the motions and degrees by their semitones.
- **Long cycles** (P4 since October 2026: a section's bars repeating, AABA to ABCD, already give every part, the drums
  too, figures of two and four bars, a varied repeat (A′) starting as its bar did and then changing, near an answer;
  the clave and the montuno are figures, *Figures as a genre's data*; built only should listening find the second bar
  no answer, or a figure want a note across the bar line): a bar
  pattern is one bar long, so a slower cycle, such as the kick's slowed to two bars, plays its first half and starts
  again at every bar line. Patterns as long as their cycle would let slow
  figures run whole, a crash every two bars, a kick figure answered in the second bar, a chord of two bars, a clave's
  3-2. Put off as an engine's change: the bar is the unit of the scheme's letters, the state's layers, the melody's
  question and answer, the drums' presence, the fills' lines and the feels. A crash every two bars, should it be
  missed, an edit after assembly as the lift's cues are.
- **Phrases of other than four bars** (P3, postponed October 2026: a section is whole 4-bar patterns, and 89 places in
  26 files count its bars by them, the fills, the phrase ends, stop time, the energy, the polymeters; an extension's bars
  would count as its pattern's first, not its last): a phrase extended before its cadence (4 + 2, the cadence's bars
  played again), a 2-bar tag closing a section (*A tag*), a phrase of three or six bars, once the pattern is no longer
  the unit (*Long cycles*). Leaned by the form facet, the plain end keeping four.
- **Decided in the review of October 2026, not built:** a genre's one rule, which it keeps already, its amounts in
  place of the songs' but a choice drawn in roles, written down on `ByConvention`; a genre resolved once, which the
  request does already (`GenerateSongRequest` checks every choice and option); streams by name (they would move every seed and so lose the
  fingerprint as a refactor's check, for one line saved a stream); a stage's module taking the song's decisions whole
  (it moves the unpacking from `SongStages` into the module, and a module's parameters say what it reads); a stage
  declaring what it reads and writes (`SongDraft`'s fields say who makes them, and CLAUDE.md the two phases). A new
  stage takes `(SongDecisions, SongDraft)`, as fifteen do.
- **Plainer prose:** comments that say in a plain sentence what the code cannot, and commits' lines of about a hundred
  characters; CLAUDE.md's paragraph on conventionality as rules a test can check.
- **The ratings in the history** (P4): the list of songs so far does not show which were liked or disliked.
- **A mix written by the page** (P4): a change of the mix asks the server for the file again only for the download;
  writing the mix into the file in the page would leave the server out of it, at the cost of MIDI writing in
  JavaScript.
- **The server's figures read in full** (P4): the dashboard reads every song made in its window, nothing at hundreds a
  day and slow at the 200,000 a day the table keeps at most; then the spreads would be worked out a day at a time.
- **The note states read once** (P4, from the review of 9 October 2026): `Realizer.GetNoteStates` merges a track's
  state with the whole song's and builds a state map at every position, again for every pass of `RealizeStruck`, for
  every solo's loudness and every answered gap; reading it once a track, and a note's tempo without the whole map,
  would take a share of the 17% realizing costs and the stages' few percent each. Measure with the profile first.
- **Analytics written off the request** (P4, from the review of 9 October 2026): every tally and song measurement opens
  SQLite and writes under one lock on a pool thread, so a burst of tallies holds threads that songs wait for; a queue
  one writer drains, in batches, would answer at once. And the admin password tried without a limit: a pause after a
  few wrong ones, should the area ever face the open internet without the proxy's.
- **Tests that check less than their names** (P4, from the review of 9 October 2026): the louder and quieter forms
  per appearance, both ways; a breakdown's riff chugging and its kick on its onsets; a solo's approach off the beat
  into its change; each passing kind's shape (the tritone substitute's dominant, the half-diminished's whole step);
  the melody's joins at every pattern; the busyness reports' facets read from the trace; the MIDI file parsed for its
  program changes; the stages' hand-offs, each read after it is made.
- **The suite on a second corpus** (P3, from the seeds made 64-bit, 9 October 2026): redrawing every song with no other
  change failed about 30 tests, rules meeting songs they never had (a stop leaving a soloist ringing, a hook intro
  without its chorus's inner line) and calibrations over too few songs. A setting that shifts `TestCorpus`'s seeds
  (`RMG_CORPUS_OFFSET`) would let the suite run now and then on other songs, so that such bugs show before a change
  that redraws the songs trips over them; each failure then fixed as a bug or its calibration given a wider sample.
- **The page's tests of sound** (P4): the page tests drive asking for songs, not hearing them; playing, seeking, the
  MP3 export and the lock screen's controls are tried by hand, as are Safari and phones.
- **A report of thousands of songs** would make, measure and let go of its seeds past the shared corpus rather than
  cache them; a corpus song holds about 5 MB.

## Genres

A genre is the page's data (`Genre`, `GenreChoices`, `Client/genres.json`), every option three amounts, at the plain
end, the middle and the wild end; what each choice does is in the code and the numbers of each in its commit. What is
left:

- **Any genre** (P3, a plan in steps): a song asked for with no genre drawing a preset, or the songs' own as one of its
  outcomes, evenly, so that the plain band stays among them; the seed alone making the song again, the CLI's and the
  API's too, and the ratings telling which genres are liked. Steps:
  1. The presets the server's data rather than the page's alone (`Client/genres.json`), served to the page, which the
     tests and the CLI read too (`--genre`).
  2. The server drawing one when asked for any, from a stream of its own, so that the rest of the song stays as the
     seed made it; the preset drawn in the response, the song's link and every analytics event about it, as its version
     and settings are.
  3. The page's Any genre, its preset drawn by the page for every new song, drawn by the server instead.
  4. The default (the songs' own, as now, or any genre) decided by the ratings by genre once a version has enough, and
     not before the retuned presets, Pop and Metal most, are heard.
  Not weighed by the presets nor leaned by conventionality: each preset spans the plainest songs to the wildest.
- **Rock and metal as their bands play them** (P3, from 8 October 2026): what each lacks against the sound of a riff-driven
  hard rock band, a blues-rock band's shuffle, a twin-guitar heavy metal band, thrash and groove metal
  (`RockAndMetalReportTest`). Their solos (73% and 78% of songs) and their riffs of two and four bars (AAAB and ABAB a
  third of their sections) were there already; the organ's solo, Rock's shuffle at its plain end and Metal's clean
  quiet sections are presets now. Left:
  - The riff between the sections (P3): one part alone is a texture already (`TextureKind.Alone`), the riff a genre's
    weight of it, but not placed as an interlude of its own.
  - Tremolo picking (P3, measured first): fast repeated notes unmuted, the riff's speed perhaps enough already.
  - A melody sung rather than played (P3, a listening question first): no singer in General MIDI; the melody's
    rhythms word-like, its breaths and held ends, should it sound like a lead guitar playing a tune.
- **Black metal and IDM, the rest** (October 2026, `NewGenresReportTest`): Black metal tremolo-picks 51% of its
  sections, blasts 20%, double-kicks 20%; IDM, warm and swung at its plainest, an acid line, odd meters (47% of its
  wildest songs), polymeters and rolls at its wildest, breaks 24% of its sections. Left:
  - Rolls splintering into 32nds, a breakbeat chopped and drilled, figures finer than a 16th (P3, by listening): the
    figures' grid in 32nds, should IDM's wildest want more than its hi-hat rolls and fills give.
  - Field recordings, a prepared piano, glitch edits (*Parked*, beyond General MIDI).
- **More genres** (P3). What the genres made in October 2026 still want: Fusion's
  roots off the scale (the progressions move by a 7-note scale's steps, chromatic and constant-structure motion
  coming only of planing and a chord's own scale) and a lead's wide-interval legato lines (*A melody's leaping by
  genre*); and Bossa nova's two-bar clave (below and *Long cycles*).
- **The hook and the phrase scheme as one** (P3): a hooked section a scheme sung literally, should a trial keep the
  report's repeats (50% of a hooked section's notes at their first bar's places); otherwise kept apart, with the reason.
- **Harmony for Jazz and Soul** (P3): a ii–V elsewhere (*Approach chords*, *Harmony's devices*); call and response
  (*The band's devices*); a part resting by its section's role.
- **The choices grouped into families** (P4): bass, harmony, form, drum devices, one choice of several chances rather
  than fifteen of one, which would read better in the editor; should the editor grow hard to use.
- **What the genres cannot play yet** (P3, each by what listening to the presets says; `GenreDevicesReportTest`,
  `GenreIdiomsReportTest` the measures):
  - The loop's figures shared, the bass's and the riff's, beside its harmony (`GenreChoices.Loop`).
  - A riser as a lift cue, into a louder section where the band plays on, which the riser into a drop
    (`Risers`) plays on a channel its silence leaves free: a program change there would cut the part's
    chord, so a channel of its own, should a lift want one.
  - The pump on the kick wherever it falls, should a pumping genre's kick leave the beat; it ducks on the beat now.
  - The pinned drums (`GenreChoices.Pins`) in the bars a drop or an entry clears, and the wilder songs that leave a
    role free.
  - A snare's drag, two quick strokes before a backbeat, should the ghost notes want it.
  - The piano's montuno, a broken chord off the beat, an arpeggio's pattern of its own (`Arpeggios`) rather than a
    figure, the figures striking a player's chord or note; a second time keeper playing straight under the tresillo.
  - More figures (P3, by listening): Bossa nova's clave, a figure for the hi-hat or the snare, a figure of more than
    two bars; the double kick following the riff's accents alone, should every 16th sound a machine.
  - A hook of one or two bars sung through a whole section, its phrase ends let go of; a hooked melody's bars repeat
    about half at most now, a phrase's last bar ending on its held note.
  - A fill's drums leaning to the toms by genre, which the drum groups' state draws, not a genre's choice.
  - A melody's leaping by genre, a lean of its register's freedom and its leap's size: leaning its stepwiseness moved
    the mean move by a tenth of a semitone and the leaps not at all, a melody's leaps coming of a phrase starting
    afresh in another register, its echoes and its landings.
  - A part resting by the section's role (a dub's breakdown), with *A genre's sections differing*.
  - A genre's lean of the phrases' place rules, should it press them, and the songs' own sections of eight bars now
    and then.
  - Articulations played by a plugin's output as a keyswitch or a controller, the one place in `Render` the seam, once
    an output differs from General MIDI.
  - Beyond General MIDI, parked: the gated reverb (one reverb send for the whole drum channel, no gate); an acid
    line's slides and accents past the filter's squelch (`StrongEffect.Squelch`), which has no 303.
- **The rhythm's values against the part's own:** a value moves a setting by steps from where the part rests, so that
  one part's "faster" is another's "as fast as can be"; a value set outright, where it lands, would read the same for
  every part, should listening ask for it.
- **A link replacing the settings kept** (a decision): opening a shared song makes its settings this browser's own,
  so that the next song goes on in them; keeping one's own and offering a way back to them is the other way, should
  listening to shared songs ask for it.
- **Instruments by listening:** the timpani as Dungeon's and Orchestral's bass, an organ's bass under Jazz and Soul,
  the church organ on the dark genres' pads, a music box and a kalimba on IDM's riff, Chiptune's square and saw on
  every part, General MIDI's triangle missing (its bass a bass-and-lead or a square).
## Parked

Ideas with a reason, not planned: none has shown a need. Taken up only when listening or another item asks for one.

- **A song's mood** (P4): a value drawn per song that a preset's options lean dark or bright by, so that one preset
  spans liquid and dark drum and bass coherently; a preset of each until a third genre asks for a span.
- **Segments in any pattern:** bars that mix feels, such as three straight beats and a quintuplet beat; a fill's speed
  change, a rank more for one half of its span, would be a case of it.
- **A drum's sounds together:** a drum is one track, whose note plays one sound, so a run never plays two toms
  together; notes of several sounds on one track would allow it.
- **Patterns by measured features** (syncopation, evenness) as a family, should a target prove out of reach of the
  dyadic engine; euclidean patterns that fit no cycle; a library of idioms such as clave and bossa, which genres may
  take up; and drums generated together, the snare avoiding the kick and the hi-hat filling the gaps.
- **An endless song:** sections generated afresh as it plays, with no ending. The sections generated per appearance
  are a step towards it; the form's plan, its intro and ending, and the song's single pass are not. The time is no
  obstacle: an hour's song asked of the generator is made in one to two seconds, but plays 33 to 45 minutes at 120
  bpm, its form's parts running out (`SongLength`'s free form of up to 32), and the page's length stops at fifteen
  minutes (`LengthScale`); an hour and more is the endless song's, not the length's.
- **A parallel CLI batch** (P4): `--count` making its songs in parallel; a song takes about a second.
- **The rare devices pruned** (P4, the review of October 2026): a device that plays in under about 1% of songs, with
  and without genres, and was never heard as good, taken out or folded into another, the options at
  `ByConvention(0, 0, 0)` that genres alone reach first; should the stages grow too many to keep.
- **The wild end's one device** (P4, the review of October 2026): a wild song stacks key and tempo changes,
  polymeters, every gentle effect and a strong one; one or two devices a song as its signature, leaned by its facets,
  should the ratings fall at the wild end (the ratings by conventionality).

## Decided, and kept

- **Black metal ends both ways** (heard 8 October 2026, 2 better, 3 worse, 1 not told apart): a raw band's modal ending,
  ♭II, ♭VI or ♭VII into i, the most often, and a symphonic band's classical V–i now and then, its last chord V 22%, its
  leading tone raised 5% (`cadenceDegree`, `cadenceRaise`); the songs' chances of the raise and the chord into home as
  they were.
- **Decided against** (8 October 2026, the review of P3 and P4), each for little heard at its cost or a sound General
  MIDI has not: the drums one by one (GS drum parameters), a flanger, the percussive family's other sounds, sound effects,
  sound design beyond General MIDI, ping-pong echo, modulation (no soundfont maps it), a sustain pedal (notes are held to
  the next already), long fall-offs, the vibraslap's pickup and the drum groups' pulses fading by their sounds; the hook
  at the end (the last section is the chorus most often), a head with solos (AABA and a genre's solos come near it), a
  genre's sections differing (the dynamics' contrast and the phrases' AAB give most of it), the tempo leaning by the
  energy, a key change's common tone and the fills' loudness as layers; and as refactors with no sound, note keys read
  plainly, the drummer as one player or as state, a stage report of notes moved and the notes' memory.
- **The brightness's range** (measured October 2026, spessasynth rendering one note of each family in both soundfonts
  served): closing the brightness (CC74) below the middle darkens every family, the distorted guitar too, to half its
  brightness or less by 48 for a few dB; below 40 most lose 10 to 40 dB, the pads first, so a filter's floor is 40.
  Above the middle only the synths brighten (two to ten times), the acoustic instruments open already, so a tone
  darkens from the sound's own and brightens back to it, never past it but on a synth. The resonance (CC71) adds up to
  8 dB at 84.
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
- **A drum keeping time plays grouped cycles (a tresillo) unleaned,** as the other drums do: its convention is a
  genre's to say (see *Genres*).
- **A polymeter's cells are not accented by themselves:** a cell's first note is the motif's, on the frame's downbeat,
  and carries its accent already (0.52 of velocity against the rest's 0.15).
- **The state is realized per map:** of the songs' own time a quarter is realizing notes, each map's state timeline
  built once (`StateTimelineMap`), which is how the state is designed rather than a hot spot to fix.
- **An applied dominant keeps the shape drawn for it,** which its Mixolydian makes a major triad, a dominant seventh
  or a suspended dominant; forcing a seventh would override the shape's draw for the stranger shapes alone.
- **Groove hold stays beside pins** (measured October 2026): a pin decides a part's figure, groove hold leans every
  part steadier, which the principles ask for over pinning more. With it taken out of every preset the pinned measures
  moved 2 points or less, but the unpinned backbeat fell: House's snare on 2 and 4 0.67 -> 0.45, Techno's 0.61 -> 0.47,
  Funk's 0.77 -> 0.68, Blues' 0.71 -> 0.63, where a snare pinned 70% of the time is held by it the rest.
- **Seeds are not stable across versions;** the version labels what was heard (see *Listening data*).
