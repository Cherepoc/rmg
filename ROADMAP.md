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
   16 given back: half and double time in Rock, Country, Disco, Fusion, Ska and Soul (since the review of 11 October as
   the songs draw them, never at the plain end), Soul's legato and key changes,
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
   page's genres, and over every preset's songs (`--fingerprint-songs`). The new songs showed,
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
    kept, a position's section found by halving: 7.4 s to 6.4 s for the 32, the fingerprint kept. After it (9 October
    2026), allocations: a song 64 to 47 MB allocated and 127 to 94 ms, the suite 204 to 150 s, a timeline's states
    made once, a note's state read at its place, a track's state merged once over `RealizeStruck`'s passes. Its few
    passes and the notes' kept states leave realizing no more to win; what a song allocates is spread over its
    decisions (21 MB), realizing (9 MB), the lines (5 MB) and the rest, no call above a tenth (`AllocationReportTest`).
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
   played by no preset then, since reworked with leaps for Djent; a driven guitar's quieter form, since a section's calm form
   (*Planned: held tones, let ring, a section's form, and two genres*, step 3). The montuno left, a pattern of its own (*What the genres cannot play yet*).
9. Done already: the instruments by listening are in the presets, the timpani, the organ bass, the church organ, the
   music box and the kalimba, Chiptune's squares; their weights by listening (*Instruments by listening*).
10. Done: a strum's up stroke missing a full chord's lowest string. Left: legato slides, by listening; a fill's toms by
    genre; a band leaning into a big entrance, whose tempo would know the dive's section; the riser over a filtered drop,
    put off with the riser as a lift cue, both wanting a channel of their own.
11. Let go of after its plan's review: a phrase extended before its cadence, 4 + 2, as an edit after assembly, would
    move every later place of the song's map, its lines, fills and stop time, its bars counting as its pattern's first:
    the 4-bar grid rewritten in all but name (built since, *Planned: the engine*, steps 4 and 5).

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

**Planned: the engine** (9 October 2026, reviewed the same day, each step through the implementation loop; output
beyond General MIDI left out). Of the six limits named, three are smaller than they looked: a drum note may hold
several pitches and an ornament already, as the render plays both for any track; a fill already changes its feel
within a bar; and swing already pairs by each section's meter. In order of what each gives for its cost:

1. Done: a riser's own channel, the render's, its part's track carrying it and its mix over it, the page's too; a
   riser into every drop, a filtered one's as well, its part playing on under it (`riser` heard and kept, 10 October 2026).
2. Put off after its review in the loop: the bar patterns' finest note by the tempo. A shortest note in seconds that
   keeps 16ths at the fastest tempos lets 32nds through at the slow ones, and every pattern whose rank bounces off the
   limit now (`ResolvedRhythm.Of`) would change, so it cannot be one rule with the fills' without a genre's lever; the
   one genre leaning fineness up, Metal's kick at 100, means its finest, 16ths, which a lifted cap would make 32nds; and
   IDM's alone would want a state of its own and the tempo in the bar's rhythm for a sound no listening has asked for,
   its hat rolls and ratchets in 32nds already. Taken up with *Rolls splintering into 32nds* (*Black metal and IDM*).
3. Done: flams and drags, the snare's ornaments, its grace strokes before its note on its sound (`DrumOrnaments`), a
   flam on a fill's 8ths and a drag into a backbeat, the song's by its fills and groove facets and a genre's
   (`rudiments`), none in the machines' genres (`rudiments` heard and kept, 10 October 2026). Two toms at once put off (*Fills and
   lines*, *Two toms at once*).
4. Done: a phrase's length the meter's (`Meter.PhraseBars`), every phrase line through the meter, the fingerprint kept.
5. Done: phrases of three bars and of four and two (`PhraseLengths`), by the form facet and a genre's `phraseBars`, for a
   pattern of one phrase: a scheme of four fitted to them, three leaving out its second bar and four and two playing its
   last two again, its cadence's chords with them, which go back to their preparation before they arrive; a span that
   fits no phrase not drawn, a chord a bar where none of a genre's does; the loops' genres in fours, Orchestral's four
   and two 14% of its sections (`phrase-length` heard and kept, 10 October 2026). Left: the genres' section lengths named in bars of four
   (`bars16`) play their patterns as often whatever their phrase, so a section of "16 bars" in threes has 12; and an
   intro's window of four bars, which a first section in threes does not end a phrase with.
6. Done: a short bar before a section (`ShortBars`), a section's last bar cut to its first half of groups wherever it
   plays but at the song's end and under an intro's entries, by the form facet and a genre's `sectionChanges`
   "shortBar", Djent's the most, 6% of the drawn songs' sections at the middle and 19% at the wild end; the map's meter
   at a position the short bar's (`SongMap.MeterAt`), so that a fill, a drop's silence, a riser and a tempo's easing
   take the bar before a section as it is, a held note cut to it, the file signing it (`short-bar` heard and kept, 10 October 2026, off in the loops' genres). With it, a
   trading turn half a pattern in whole bars, a pattern of three trading bar by bar, and a solo's phrase kept within its
   turn.
7. **Feels mixed within a bar,** let go of after review: a bar's change of feel taking effect from one of its groups
   rather than its start, generated as two spans spliced. A fill does it already where a drummer does, and a soloist's
   tuplets give a run its fives and sevens; a groove changing feel mid-bar outside a fill is the wild end's alone and
   would be heard as one. Kept in *Parked* (*Segments in any pattern*) unless listening asks.

The tag stays put off: the extension (step 5) covers a phrase's last bars played again within its section, and a tag
at a section's end alone would make its last pattern unlike the others, which nothing in steps 4 to 6 needs.

**Next plan** (10 October 2026): call and response (*Form and sections*), then acoustic sections fitted, quiet drums
in calm genres, a breakdown's snare with a double kick's backbeat, and the tag. Done first: the suite on a second corpus
(`RMG_CORPUS_OFFSET`), its 28 failures at an offset of a million fixed, 13 of them bugs (an intro's entries over a
first phrase of three, a mutation past a short bar, a section without its harmony, a line cliché off its section's
groups and out of its chord's scale, a polymeter's copies in the wrong chord shape and pushed, a doubled line silenced
where a texture rests its source, a drop the band does not come back on, a stop's silence struck again, a quieter form
recorded unplayed), the rest tests assuming what new songs break and calibrations given 128 songs.

**Done: the plan of 11 October 2026** (each step through the implementation loop, deployed to test): the clean-up;
held tones, let ring and a section's form; a heavy band's backbeat, Punk's breakdown, Djent's drone and polymeter frame,
the bass with the riff by convention; call and response, the songs' own appoggiaturas, ghost notes in three more genres,
quiet marks on a colouring kit, the drum solo's tom cascade; 12/8, the Power ballad and Classical; the CLI's presets and
the ratings in the history; the montuno and a dance track's stripped ending. Left of it: Any genre's server draw (put
off) and its default by ratings, the ratings report, two toms at once, and Tooling's items, each when its need shows.

**Next** (P1, alongside): listening to what is deployed and tuning by what it says (see *Listening*, the page's
listening mode for the questions it asks), done for a version when every question is answered or let go of; and the
ratings by conventionality once enough are in, continuously and never as a gate.

**Later** (P2): the ratings report (see *Listening data*) once the dashboard shows songs rated enough.

**When the need shows** (P3), each by what listening says: the items left in the sections below, each marked P3, any
genre first (see *Genres*).

**Put off** until its uses ask for it: a tag (see *Parked*).

**From the review of 8 October 2026** (P2, to be ordered into the next plan): the architecture's items (see
*Architecture*: plainer prose);.

**Done: the parked reviewed** (9 October 2026, each step through the implementation loop): the seven steps one rule, a
scale of other than seven notes refused; Bossa nova's clave on the cross-stick (`?listen=bossa-clave`); the melody in
block harmony, the counter-melody stacking its chord (`?listen=block`); roots off the scale, a planed section's chords
each in a mode of its own (`?listen=own-modes`). Long cycles for the generated patterns tried, and since
decided against, as are scales of other sizes (*Decided, and kept*).

**Parked** (P4): see *Parked*, kept for their reasons, not planned.

## Listening

What listening is to settle: each a question, its measure, what it decides, and where the page's listening mode asks
it, `?listen=` its id on the test instance (`ListeningQuestionsTest` writes them, the dashboard tables the verdicts).
Done for a version when every question is answered or let go of. A question's songs are those of the corpus's first 24 that play its
device where it is heard the most, by how much its song with the device and without differ as heard
(`HeardDifference`, the notes' loudness that differs and the moments its effects do), from where they differ the most;
its song without lays its patch over the genre's options, the others kept (`withoutDevice`). Asked again since:
`tape`, whose song without had lost every other effect of its genre's, and `stop-time`; asked anew, `inversion`, the
melody's figures inverted (Pop), and `trade`. The least heard by their nature, short devices in a long song:
`lift-cues`, under 0.1. Answered on 10 October 2026, and kept: `riser`, Techno's reverse cymbal into every drop, the filtered ones too, on a
channel of its own; `rudiments`, Jazz's flams and drags; `phrase-length`, Orchestral's phrases of four and two and of
three; and `short-bar`, Djent's sections ending on a bar cut short, which the loops' genres play none of now (Trance,
Drum and bass, Trap, Disco, Funk, Ambient and Chiptune beside the five that named none), their sections square.

Answered on 11 October 2026, late, the round of the plan of 11 October, and kept: `calm`, Black metal's calm sections
(6 of 6); `techno-strip`, the dance tracks' stripped ending (6 of 6); `montuno`, Latin's (6 of 6); `djent-open`,
Djent's drone with leaps (5 of 6); `appoggiatura`, the songs' own (5 of 6); `ballad`, its heavy chorus (5 of 6);
`call-response`, Blues' (5 better, 1 worse, 1 the same); `punk-breakdown` (4 of 6); `held`, Pop's held tones and let
ring, tied 3 to 3 and kept as they are; and `blues-12-8`, heard once and kept. Then the three the round of ten had left
out: `classical`, its cello on a chord's third, kept (4 to 2); `djent-frame`, the polymeter's frames of 8 and 16 bars,
not told apart (5 unsure of 5) and let go of, the frames as they are, the choice of them cut in the review of 11 October; and `dnb-ghosts` (3 better, 1 worse, 2 unsure),
heard good where a song ghosts little, about 1.2 to 1.6 a ghosting bar, and not told apart where it ghosts 3 to 5: a
genre's ghosts now as many as it says (`ghostNotes`, `Ghosts.GenreOdds`), Drum and bass's 20, 1.09 ghosts a bar of
its songs to 0.66, heard better again (`dnb-few-ghosts`, 5 of 6) and kept. Every question built so far answered.

Answered on 11 October 2026, the night, ten of the review of 10 October's proposed questions (its findings queued for
listening). As the presets are, and kept: `idm-key` (IDM's key changes, the songs' own, #472, 6 of 6), `fusion-lift`
(Fusion's last section lifted, #455, 5 of 5), `techno-comp` (Techno's comping rhythm part, #397, 6 of 6),
`djent-colour` (Djent's colours on 84% of its broken chords, #432, 6 of 6) and `metal-violin` (Metal's violin melody,
#422, 6 of 6). Added to try: kept, `chiptune-octave` (Chiptune's octave bass, 30-40-40%, #476, 6 of 6),
`punk-riff-intro` (Punk opening on its riff, 20-30-30%, #453, 6 of 6) and `orchestral-switch` (Orchestral's theme handed
on, 10-20-40%, #464, 4 to 2); `techno-bass` (#398) tied 3 to 3, its offbeat 8ths heard good and its rolling 16ths
mostly not, so Techno's bass plays the offbeat figure alone; and `bossa-root-fifth` (#463), split 2, 2 and 2 unsure and
the least heard, taken out, Bossa nova's bass walking as before.

Answered on 11 October 2026, the review's next ten. As the presets are: kept, `orchestral-sus` (Orchestral's sus and
add9 chords as often as each other, #465, 4 of 4 sure); `ska-brass-solo` (Ska's solos on its brass section, #448) split
3 to 2 and kept as it is; and `folk-swing` (Folk's swung 16ths below 107 bpm, #408) heard worse in 4 of 6 and taken
out, Folk's swing on 8ths alone. Added to try: kept, `punk-double` (Punk's double time, 10-15-20%, #450, 6 of 6),
`metal-tremolo` (Metal's tremolo riff, 5-10-20%, #420, 6 of 6), `soul-fade` (Soul fading more often than ringing out,
#460, 6 of 6), `house-planing` (House's planed stabs, 10-25-40%, #392, 4 to 2), `dungeon-tape` (Dungeon synth's worn
tape, 30-50-70%, #430, 5 of 5 sure) and `disco-open-hat` (Disco's off-beat hat open at its most, #439, 5 of 5 sure);
`fusion-head` (Fusion in AABA, #454) heard worse 3 to 2 and taken out, Fusion's forms the songs' own. House's gliding
bass (#391) left out: its offbeat bass leaves no note to glide from. Every question built so far answered.

Answered on 12 October 2026, the review's third ten, each added to the preset and kept: `blackmetal-blast` (Black
metal's blast leaning plain, 100-80-60, #471, 6 of 6), `dnb-break` (Drum and bass's breakbeat in about half its
sections, #445, 6 of 6), `blues-form` (Blues without a pre-chorus, #410, 6 of 6), `ambient-sus` (Ambient's plain end
with fewer triads than sus and add9 chords, #414, 6 of 6), `folk-room-kit` (Folk's room kit at every end, #409, 5 to 1),
`house-tempo` (House near 120 to 128 at its wild end too, #393, 5 of 5 sure), `latin-half-bar` (Latin's chords by the
half bar, #404, 5 of 5 sure), `punk-rhythm-8ths` (Punk's second guitar in 8ths, #452, 4 to 2), `idm-space` (IDM's
deeper reverb, #474, 4 to 2) and `orchestral-ternary` (Orchestral in ABA and AABA, #469, 4 to 2). Every question built
so far answered. Left for later: Medieval's plain ornaments (#425) and Djent's rudiments (#431), the song without them
barely different or no ornament at all; Dungeon synth's arpeggios on its sustained voices (#429), its harpsichord's
ostinato played by the same arpeggio.

Answered on 12 October 2026, the plan's own questions, each as the presets are. Kept: `orchestral-alone`
(Orchestral's part alone, any part, 6 of 6), `folk-key-sections` (a section's or a pattern's own key, 6 of 6),
`rock-section-tempo` (a section's own tempo, 4 of 4 sure), `rock-rhythm-clean` (Rock's clean or muted rhythm guitar,
3 to 2), `pop-section-meter` (a section's own meter, 3 to 2) and `pop-off-scale` (applied and borrowed chords, 2 better
and 4 not told apart); `pop-switch` (instrument switches within the family) tied 3 to 3 and allowed, as they are.
Lowered: `pop-double-time`, heard a scramble in 4 of 6, the songs' double time at 2% of a middling section from 4%
(`Groove.DoubleTimeChance`), 3.3% of the corpus's sections in four from 6.5%, and the presets that copied the songs' 4
with it (Rock, Latin, Ambient, Disco, Ska, Fusion, Soul, Country and Chiptune); half and double time's own amounts now
declared as the songs draw them, never at the plain end (the review of 11 October's #56), the presets' flat amounts
still to follow (#378).

Answered on 13 October 2026, the metal family's round: kept, `numetal-vamp` and `metal-low-riff` (6 of 6 each),
`thrash-vamp` (5 to 1), `numetal-bass-riff` and `doom-half-time` (4 to 2 each), `symphonic-choir` (4 of 6, 2 unsure:
a wall, not mud) and `symphonic-raise` (4 to 1); against, `doom-blues` (1 to 4, Doom's riff on the blues scale heard as
a blues band slowed down; its blues scale to go, the flat fifth then the chromatic scale's and Locrian's alone, as the
riff stabs of the metal family's step 3 must weigh). `metal-calm` tied 3 to 3 again and was explained: the calm
sections liked were slow, the others played a tremolo or a chugging riff under the broken chords, a clean riff or
rhythm guitar the calm section neither turned nor rested, the riff playing on alone where no driven guitar broke the
chords, the first section after an intro of entries keeping its parts, and a solo over the calm section. Now every
guitar but the one breaking the chords rests, clean or driven, a section is calm only where a guitar plays the chords,
the first section too, and no solo plays over it (`CalmTest`: Heavy metal's calm sections with a rhythm guitar under
them 14 of 22 → 2, with the riff 6 → 0); asked again over other songs.

Asked from 13 October 2026, the band devices' round: `numetal-narrow`, `numetal-chromatic`, `trap-bass-kick`
(without, the figures' stage), `funk-hits` (faint, 0.07: one bar of a window) and `rock-big-ending`.

Answered on 13 October 2026, `metal-calm` asked again over six other songs after the calm sections' fix: 6 of 6 for
the calm sections, 9 to 3 in all; Heavy metal's calm kept at 5-10-20.

Asked from 13 October 2026, the metal family's first, Metal now Heavy metal and three new genres beside it, as the
presets are: `numetal-vamp` (Nu metal's sections on one chord), `numetal-bass-riff` (its bass doubling the riff),
`metal-low-riff` (Heavy metal's riff an octave down), `metal-calm` (Heavy metal's calm sections, asked again over other
songs), `thrash-vamp`, `doom-blues` (Doom's flat fifth), `doom-half-time`, `symphonic-choir` (the choir and strings
over distorted guitars, a wall or mud) and `symphonic-raise`. Nu metal's kick with its riff left unasked, a figure's
weight being past what a question's patch reaches.

Answered on 13 October 2026, the round of 12 October, every one kept as the presets are: `rock-reharm`, `pop-ornaments`
(6 of 6 each), `synthwave-octave` (5 to 1, its raised octave bass kept), `reggae-half-double` (5 to 1),
`blues-call-bass`, `soul-sixteenths` (4 to 2 each) and `orchestral-metric` (3 to 2, its cut put off); `pop-melody-chords`
not told apart (4 of 6 unsure, 1 to 1), kept, the least heard; tied 3 to 3, `trance-raise`, kept as the songs have it
(asked again it would differ by 0.08 against 0.22, too faint to hear), and `metal-calm`, asked again in Heavy metal.

Asked from 12 October 2026, ten of the review of 11 October's proposed questions. As the presets are:
`orchestral-metric` (Orchestral's metric modulation, a cut candidate, #113), `pop-melody-chords` (the melody's double
stops on every downbeat, #72, the least heard, 0.12; its song without loses the riff's landing chords too), `rock-reharm`
(Rock's reharmonised last chorus, a cut candidate, #99), `blues-call-bass` (the bass doubling the riff resting with it
under every call, #14), `pop-ornaments` (Pop's trills and turns, #367), `soul-sixteenths` (Soul's swung 16ths, #383),
`trance-raise` (Trance's raised cadence, #381) and `reggae-half-double` (Reggae's one drop in half or double time, #359).
Added to try: `synthwave-octave` (Synthwave's octave bass, 35-45-45% from 15-20-25%, #356) and `metal-calm` (Metal's
calm sections, 5-10-20%, #371). Left out: the pump where the kick drops out (#29), whose song without loses the pump
everywhere, so that it asks `disco-pump` again until the pump can keep out of a kickless stretch; and the questions that
need the code changed first (#11, #124, #44, #71, #65, #23, #67), or that earlier rounds settled (#112, #363, #96, #27,
#80, #79).

Answered on 11 October 2026, the review of 10 October's findings of taste: taken out, `disco-pump` (0 of 6 kept it),
Disco's sidechain pump; `metal-turnaround` (1 of 6), Metal's turnaround; and `trap-reharm` (2 to 2), a trap loop's
harmony static on purpose, its last hook's chords as they were. Kept, `funk-key`, `funk-pushes`, `numetal-half-time`,
`techno-feel`, `djent-polymeter` and `medieval-raise` (a cadence's leading tone is medieval practice, musica ficta; the
full major triad on the fifth is later). Halved on a tie, `rock-key-lift`: Rock's last section lifted at the songs' 8%
in the middle, not 20%. Asked again over six other songs, `quieter-form`, tied 3 to 3. The review's other fifty proposed
questions (its report's table) are asked a few at a time from here.

Answered on 10 October 2026: `ambient-swing`, Ambient's swing heard on its hi-hats alone, the pitched parts' notes on a swung 16th
one in six or fewer, too few to swing; taken out of the preset, its half and double time the songs' own.

Answered on 9 October 2026, late, and kept: `own-modes`, Fusion's planed chords in modes of their own; and `mediant`,
Orchestral's iii or vi turned major.

Answered on 9 October 2026, and kept: `bossa-clave`, Bossa nova's cross-stick playing the clave in 74% of its sections,
0.07 as a rim's stroke is quiet, its velocity the kit's highest (88 against the snare's 81) and its sample 6 to 13 dB
under the kick's at the same velocity in the soundfonts measured, the sound set's; and `block`, Soul's melody
harmonised in three voices, 0.15.

Answered on 8 October 2026, late, and let go of: the clave, stop time and the ghost notes, heard as great; the tape and
the tone, barely heard, so a tape's wow now drifts by the sound facet, 5 cents at the plain end to 26 at the wild, and a
part's tone closed 24 steps at its deepest rather than 16, set back to 16 on 11 October to stay short of the filter's floor at
40, below which instruments fall nearly silent, and asked again as a cut (the review of 11 October's #28); the rolling bass, whose sections were heard without a kick,
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
sound, and the squeal, which no genre named, cut in the review of 11 October; `dive`, a launch in 5 of 6, 8 of 12 in all, its ritardando still to be considered; `drum-solo`, the hi-hat's pedal
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
  back at the start (15-18%); one part alone (`orchestral-alone` heard better, 12 October 2026) or parts dropping out, leaned to the quieter sections (`TextureTest`).
  Decides the textures' weights and their lean.
- **The lift** (`lift-cues` heard better, 8 October 2026): a crescendo of 8 of 127 into a louder section, and the drums' cues, 2.6 a song.
  Decides whether the cues stay, and how often.
- **Key changes:** two a song in the fourth fifth; prepared ones, pivots, pedals, sequences heard. Decides their
  chances and devices.
- **Tempo:** 6/8 counted in quarters; 13/16 too busy? Decides the odd meters' weights.
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
- **The polymeters** (`?listen=polymeter` to be asked first, Djent's sections with and without): the snare on every
  cell's start; the chords joining a cycle. Decides who joins a cycle, and the frame below.
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
- **The bass under E1** (9% of its notes, `RegisterReportTest`). Decides the bass's range.

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

## Open work

Each item with its plan, and what it waits for.

### From the review of 10 October

What it left, all in `reviews/2026-10-10.md`'s *Open*:

- **The findings left open, the rest** (A3 of the plan of 11 October 2026 worked the 67 but these): the chance shapes'
  per-section shares decided, the outro's and a kept tempo's shaped (110); a preset's copy of the songs' amounts written
  as "own" rather than listed (467), should the copies drift.

### From the review of 11 October

Its 402 findings in 21 packages, worked in order through the implementation loop (the plan kept outside the
repository), its three architecture proposals among them (#168 bars from the map, #138 keep-outs as data, #126 the
intro and the ending as their source, packages 7a, 8a, 8b and 9a).

Its idiom gaps accepted, each a plan to build after the packages, one mechanism over a case each:

- **The big ending and the false ending** (#348, #372, #374), one entry: an `ending` option holding the final chord
  while the drums roll a stretched fill over the toms and cymbals, cut off together on a button's hit (Rock, Metal,
  Punk, Blues, Soul, Funk, Country, Ska, Power ballad, Nu metal, Black metal); and a `dropKind` option before the last
  repeat playing that ending's chord, a beat or two of silence, the drums counting back in (Rock, Metal, Punk, Blues,
  Soul, Funk, Ska). The songs' own 0; a genre's alone.
- **Bass on the kick** (#379): the kick's `withRiff` following generalised to a player following another, the bass's
  figure `withKick` striking the kick's onsets (Trap and Hip-hop leaning plain, Rock, Punk, Metal, Nu metal, Funk,
  Country and Drum and bass lower).
- **The kick-out** (#353): a fill's rest, the kick (and a bass on the kick) out for a line's last beat to bar while the
  hats and clap play on, landing on the downbeat; a fills option of the club genres (House, Techno, Trance, Drum and
  bass, Trap, Hip-hop, Disco, Synthwave, Pop), a held span of `Holds`.
- **Ensemble hits** (#361): stop time's one hit a bar generalised to a figure of hits from the figures' onsets, the
  band striking it together in a section's last bar before a louder one (Funk, Disco, Latin, Ska, Soul, Fusion, Jazz,
  Rock, Metal, Punk).
- **The ostinato** (#354): a riff option beside the drone, the riff held at its pitches over the chords, a clash on a
  strong beat moved to a chord tone at the plain end, kept at the wild (Techno, Trance, House; Synthwave, IDM,
  Chiptune, Ambient, Dungeon synth, Orchestral lower); the scale rule checked against it first.
- **Latin bass figures** (#360): the tumbao and bossa's bass as bass figures, data.
- **The boogie** (#366): the alternating bass generalised to a cycle of the chord's degrees, root and fifth or
  1–3–5–6–♭7–6–5–3 (Blues, Rock, Country), after the pedal's keep-out.
- **The alternating thumb** (#362): an arpeggio pattern, the thumb alternating root and fifth on the beats (Folk,
  Country, Power ballad, Blues, Bossa nova, Medieval).
- **The jazz ride** (#349): a time drum's figure, the swing ride's skip note after 2 and 4, measured first against
  what Jazz's time already plays.
- **The breakdown chorus** (#369): a `lastRepeat` option, the first pass at the thinnest texture, the second the full
  band (Pop, Power ballad, Soul, Country, Rock, Disco, Folk).
- Found while fixing it: **12/8's backbeat** is the meter's half-cycle-late cycle (`Meter.Backbeats`), dotted quarters
  2, 3 and 4, where a 12/8 backbeat is 2 and 4; the snare's own in 12/8 to be measured before the cycle's node changes.
  **Latin's clave** plays in 78 of its 299 sections (64 songs), the claves resting in the rest, though 187 draw it.
  **Two pitched parts on different chord shapes at once** (#296): at corpus offset 816000000, seed 5, beat 173.5,
  the chords play a triad's shape and the riff a five-note one, each on its own note as the section made it, in a
  section taking an inner line; neither the inner line, the polymeter, the mutations nor the figures write a shape,
  so the riff's bar as an appearance redraws it is the next place to look.
  **A long lift moving notes outside its swell** (#224): a genre's long lift moves about a hundred notes of 64 songs
  outside the swell's bars by more than 0.01 of the velocity, through a stage that sets a part by the song as heard;
  `LiftTest` counts them.
  **The central rules over 64 songs** (#287), postponed: the scale rule (`ScalesTest`) and the parts sharing a chord's
  shape (`SongGeneratorChordProgressionTest`) run on 3 and 8 songs; over 64 the scale rule fails on seed 19, a section's
  tonic pedal at beat 276.25 sounding D in F Phrygian, its A♭ right in the state at the pedal's start, and on seed 25's
  bass at 142.5, and the shape rule on seed 46 at 126 (as #296). Seeds 4 and 14 were the test's own (a planed stack, a
  line's approach read from the bar), fixed.
  **A squelch at a million** (offset 1000000): `ASquelch_OpensOnEveryNote_AndAWobble_ClosesAndOpensWithinACycle` fails
  there at the committed code of 13 October 2026, before ensemble hits; to bisect.
  **The held-out corpus** (offset 716000000) after the packages: 12 of 1581 tests fail, against 13 at the review's
  commit; four of those fixed by the packages, a dive's (the twin diving with its riff) the test's own. Two more were
  older behaviour the packages' tests now see: a djent breakdown at seed 48, section 7, in 15/16 (4+4+3+4), whose kick
  pattern (period 2 beats, rank offset 2 of 2) plays no note, so the kick with the riff has nothing to lay on (seen
  once the figures were traced as laid); and a polymeter of 6-16th cells at seed 26, beat 248, whose chords play 1.5,
  1.75, 2 and 2.25 beats apart rather than the first cell's motif (seen once the rule ran on songs that always play one).
  Nine fail at the review's commit too: a chromatic passing chord (seed 4 at 2), a section's own key (seed 50), a
  pushed change (seed 58 at 192), a guitar's strum (seed 15 at 148), the inner line, a chord-playing soloist's stacks,
  the family switch's share, every chord note in its scale, and a song's notes as render decides them.
- Later: **the tape stop** (#358), every pitched part bending down an octave over a slowing tempo into a drop or as
  the ending, a change of the tempo map and every channel's bends together; and **imitation** (#388), the
  counter-melody taking the melody's phrase a bar later, its clashes across chord changes new work.

### Planned: held tones, let ring, a section's form, and two genres

Planned 11 October 2026 and revised the same day after two reviews; each step through the implementation loop, with a
listening question and a deploy to test. In order:

1. Done (11 October 2026): **held tones**, a colour of a broken chord's (`ChordColour.Held`, `HeldDark`), the key's second
   and third or its fifth and sixth held over every chord on a clean instrument that dies away; Pop's sweet ones heard
   and kept (`held`), Metal's dark ones (rare on its clean guitar) asked with step 3's calm sections.
2. Done (11 October 2026): **let ring**, a part's articulation (`Articulations.LetRingChance`, "letRing"), a broken chord's
   notes sounding to its change, a bar at most, never over their own pitch struck again, on a clean instrument that dies
   away and a figure of 8ths or slower; Pop's with its held tones, heard with them and kept.
3. Done (11 October 2026): **a section's form** (`SectionForm`), decided with the section in place of the instrument swap
   after assembly: a quieter section calm, its driven guitars acoustic or clean, one breaking the chords in 8ths or
   slower, let ring with the genre's held tones, the other guitars resting, a late entry waiting for the next appearance,
   no figure, the drums in half time or resting; a louder one heavy, its clean guitars driven, its chords struck. Black
   metal's calm in 38% of its songs, with its dark held tones, heard and kept (`calm`, `quieter-form` retired). Left,
   by listening: a calm section's bass held on its roots; a fill or a fade into it; Metal's and Rock's calm.
4. Done (11 October 2026): **a riff's drone, with leaps**: its weak notes on an open string, the key's own at the bottom
   of the guitar, its melody an octave or more over it as far as the guitar reaches, 9.5 to 10 semitones on the average,
   single notes, no chugs; Djent's chugs drawn down to 50% of its sections and its drone 14%, heard and
   kept (`djent-open`). Black metal's tremolo figure repeats a note, so its drone would not alternate; left to listening.
5. Done (11 October 2026): **12/8**, a meter of four groups of three 8ths (`Meter.TwelveEight`), the songs' own never
   at 0, 2% at 0.5 and 15% at 1; Blues' 6/8 moved mostly to 12/8 and Soul's halved into it, heard and kept (`blues-12-8`).
6. Done (11 October 2026): **Power ballad** (`ballad`), data: a clean guitar's broken chords let ring with sweet held
   tones, an overdriven lead, strings, 60 to 85 in 4/4 and 12/8 (28% of its songs), 3.5 to 5 minutes, the chorus
   heavy (57 of 263 sections), the last chorus up a step; its heavy chorus heard and kept (`ballad`).
7. Done (11 October 2026): **Classical** (`classical`), data: a melody on violin, flute or clarinet over a piano's
   Alberti figure and a cello, no drums, raised cadences and ornaments, AABA, ABA and a rondo (`SongForms`, forms of
   their own the songs never take), terraced dynamics, 80 to 140 in 4/4, 3/4 and 6/8, two to four and a half minutes;
   the cello on a chord's third in 32% of its notes, heard and kept (`classical`). Its key change to the dominant, a
   genre's pool of key steps, left until the songs' own steps are heard not to serve.

Review: **overengineering**: held tones a colour and let ring a note's length, not new systems; a section's form one
mechanism for both directions rather than a calm texture beside the louder form; the presets data, with 12/8 and
perhaps the key steps their only code; a tempo of a calm section's own left out, half time giving the calm. **Musicality**:
held tones sweet or menacing only on clean, decaying sounds and a few notes, and dark ones dark over the tonic alone,
lush over the 6th, so heard before kept; let ring blurring on fast figures, so 8ths or slower; drums blasting under a
calm guitar the absurdity listening split on, so the drums are measured; the jump into a calm section left as it is, as
black metal cuts, a fill or a fade there only if listening asks; Classical judged by its cadences, its bass and its
dynamics, as it has no drums.

### Planned: the metal family, tuned down, locked and split

Planned 12 October 2026 from listening (Nu metal heard as not its sound): its riff a fifth too high, a melody rather
than a groove, chords changing every bar or two, the band not in unison. Metal heard as too diverse: its facets drawn
apart, a choir and a violin over a thrash tempo, a gallop under organ chords, which a preset of its own for each style
keeps together. Each step through the implementation loop, with a listening question. In order:

1. Done (12 October 2026, data, `MetalFamilyReportTest`): **Nu metal's harmony held still**, a vamp in 40-50-50% of
   its sections and the same root leaned 75, its bars under a root held four bars or more 1% → 56% (Djent's 48%,
   Funk's 46%, Techno's 78%); **Metal's riff an octave down** as Nu metal's and Djent's already were, its lowest note
   50.3 → 38.3. Dropped, measured unneeded: a span of four bars (the vamp and the same root hold a chord as long), and
   a tuning below E2 (Nu metal's and Djent's riffs already reach C2, 36, the octave's floor; A1 would ask a guitar's
   sample stretched into mud). Asked: `numetal-vamp` and `metal-low-riff`.
2. Done (12 October 2026, data): **the band with the riff**, the bass doubling it by the genre's `doubling.riff` as
   Djent's did: Nu metal's at 90-90-80, its sections with the bass on the riff 38% → 65%, its riff's onsets with the
   bass 72% → 87%; Metal's at 90-90-80, 53% → 68%; Nu metal's kick with its riff 47% → 55% of its sections (the
   figure's weight 100 over its own 60), and its bass picked first, a slap bass in 56% of its songs from 75%. No code:
   the doubling a genre's chance already. Left to listening: a slap bass in the quiet sections alone, an instrument by
   section; asked, `numetal-bass-riff`.
3. Put off (12 October 2026, measured twice): **a riff's dissonant stabs**. A chugging riff's notes off its pedal
   already step up from it by its riff scale, so its ♭2 and ♭5 are 3% of its chugging notes off the beat (Nu metal's,
   `MetalFamilyReportTest`; Djent's 8%), and its beats stay on the root and fifth, as a band's downbeat does. Tried:
   every note off the pedal a stab within a tritone over it, 3% → 3%; with Nu metal's riff leaning to the blues scale
   (31% of its sections) and Phrygian, still 3%, the stab one of three to six notes of its scale. Left: a stab palette
   leaning to the ♭2 and ♭5 over the scale's other notes, the scale rule against a chromatic stab in a diatonic riff
   scale to be weighed; first a listening question, the stabs heard as what Nu metal lacks.
4. Done (12 October 2026, data, `MetalFamilyReportTest`): **Metal split** into Heavy metal (keeping `metal`: the
   gallop at 20-30-40%, twin solos at 40-50-60%, harmonic minor, 110 to 170, the choir, violin and organ out, chugs
   60-60-50), Thrash metal (`thrash`: 160 to 220, Phrygian, a vamp in 30-40-50%, its bars under a held root 44%, 90% of
   its riff's notes chugging, the kick with the riff and the double kick, whammy dives), Symphonic metal (`symphonic`:
   the choir, strings and organ, a violin or a voice-like lead, harmonic minor's raised cadence, power chords first and
   triads at 40-50-60, the last section lifted) and Doom metal (`doom`: 60 to 80 with half time, five to eight
   minutes, power chords held, 22% of its riff's notes chugging, its bars under a held root 40%, the ♭5 on 10% of its
   riff's beats). Groove metal left to Thrash, Nu metal and Djent between them. Ratings under `metal` from before the
   split not used to tune its presets. Left to listening: each new genre heard against its sound; Symphonic's choir
   and violin over distorted guitars, a wall or mud.
5. Done (13 October 2026, data, from `doom-blues`, 1 to 4 against): **Doom's blues scale reduced**, its riff's
   `riffScale.blues` 30-40-50 → 0-10-25, its sections on it 26% → 8%, the ♭5 on its riff's beats 10% still, its
   harmony's; the plainest Doom the funeral and death-doom kind on natural minor, Phrygian and the chromatic
   creep, the blues colour of traditional and stoner doom kept to the wilder songs. Its riff in power chords was weighed
   as the fix instead and measured unneeded: 80% of its blues notes, 76% of all its riff's, are power chords already
   (`MetalFamilyReportTest`), so the blues band heard is in the line's ♭3–4–♭5 motion, not its single notes. Left to
   listening: whether that motion, slow and stepwise, is the blues lick heard, and a held or bent ♭5 the doom way to
   play the scale; the question itself faint (0.12 to 0.18), so asked again over louder songs once reduced.
6. Asked as `numetal-narrow` (band devices' step E): **a melody spoken rather than sung**, the range at its narrowest;
   repeated notes and a range of a fifth left until it is answered, with *A melody sung rather than played* (*Genres*).

Review: **overengineering**: the tuning one drawn floor, no strings or retuning per section; the accent tone an option
of the riff's; the split data on the steps before it, Thrash without the accent tone and four bars only a fast Heavy
metal, Doom without the tuning a slow one; no sub-styles within a genre, a preset being what keeps choices together.
**Musicality**: a stretched guitar sample muddy below E2 by soundfont, so heard before tuned; dissonant accents only
over still harmony; a slap bass doubling a distorted riff turning it to funk metal, so picked where it doubles; a
narrow melody on a guitar possibly only dull, so asked before built.

### Planned: dark jazz

Planned 13 October 2026: a preset of slow, dark jazz, a noir lounge band played at a funeral's pace: a tenor sax or a
vibraphone holding long notes over an electric piano's held minor chords, an upright bass on the roots, brushes and a
ride barely there, an organ or strings under them, minutes on two or three chords, everything soft and in a hall. Its
neighbours are Jazz (its band and its harmony), Ambient (its stillness) and Doom metal (its pace and its length). A
genre is data first (`Client/genres.json`): every step below a preset's values but the guarded ones, which are code
only if listening asks. Each through the implementation loop:

1. **The preset** (`darkJazz`, "Dark jazz"), data alone. Tempo 60-72, the half-time feel likely (`feelChances.halfTime`),
   swing light or none; natural and harmonic minor, Dorian at the wild end, the Phrygian second as a colour; seventh,
   ninth and shell shapes (a minor key's seventh on its second degree is the half-diminished one); chords two bars or
   more (`harmonicRhythm.twoBars`), no turnarounds, passing chords or key changes; the electric piano's chords held
   (`rhythm.chords.speed` 0), its tremolo now and then (`strongEffect.tremolo`); the bass on roots and fifths, not
   walking (`bass.leading` low, `rhythm.bass.speed` low); the melody tenor sax, vibraphone, muted trumpet or the electric
   piano, sparse (`rhythm.melody.busyness` and `speed` low, `space`), swelling and with vibrato; the brush kit, the ride
   and the snare's backbeat alone, fills and ghosts few; an organ, strings or a choir as the pad; a counter-melody on
   the vibraphone now and then, solos rare and slow; dynamics low and even, the hall; sections of 16 and 32 bars,
   textures steady and thin, entries for the intro, a ring-out or a fade for the ending; songs of five to nine minutes.
   No riff, rhythm guitar, stops, hits, pushes or big ending. Measured against Jazz, Ambient and Doom metal on 64 songs
   each (a report of its own): the tempo felt, every part's notes a second, a chord's length in seconds, the melody's
   share of silence, the drums' hits a second; the targets the slowest and sparsest of the four, set from that
   baseline. Asked: `darkjazz-halftime` (the half-time feel against none) and `darkjazz-swing` (light swing against
   straight), the two choices most likely to tip it into a lounge ballad.
2. **Guarded: a tempo under 60** (the page's tempo runs from 60, `GenreChoices.Tempo`'s scale), only if 60 with the
   half time is heard too quick; the scale moved down, the page's slider and its tests with it.
3. **Guarded: the brushes' swirl keeping time** (the brush kit's sustained stroke, on the electric snare's key), only if
   the ride's taps are heard too busy; first checked that a genre's time role can name the electric snare.

Review: **overengineering**: no mechanism of its own, a preset of choices that exist; the tempo floor and the swirl
behind listening; no tape hiss, vinyl crackle or a film's samples (beyond General MIDI); no Mellotron, which General
MIDI has not, the strings, a flute and the choir standing in. **Musicality**: slowness alone makes a lounge ballad, not
this: the dread comes from space, held chords and the minor's dark degrees, so comping, walking and fills are the
risks and are kept down; swing at 60 easily sounds cheesy, so asked; a sax held soft is thin in General MIDI, so its
level kept up against the band's; eight static minutes bore, so the textures thin and fill the sections, the form's
plainness kept; the harmonic minor's dominant with its flat ninth the colour, not chromatic passing chords; the brush
kit is in both soundfonts (program 40), its sound heard before its weight is raised.

### Planned: lyrical guitar

Planned 13 October 2026, revised the same day from reviews of the reference records: mellow, melancholic instrumental
guitar music, mostly acoustic. Reviews agree on: acoustic guitars intertwined with a clean electric one of a dry,
neutral, jazz-like tone and a simple bass; heavy delay on everything, sometimes the pulse itself; a steady,
prominent hip-hop or trip-hop beat under most pieces, loops, now and then a near-industrial one, a heartbeat-like
pulse in the darker ones; melodies repetitive, "geometric", the album hardly varying; a distorted, heartfelt solo
building at the end of the most electric piece, over a bass-driven groove and cold synth swathes; a cello and a viola
on two pieces, turntable scratches on three; a Spanish-sounding harmony now and then; some pieces ominous rather than
sad. The companion record has no drums at all, its delays the rhythm. Tempos about 80 (two pieces estimated at 81 and
82 by tempo databases' estimates, both minor). Its neighbours are Hip-hop (its beat) and the Power ballad (its let-ringing guitars). Each through the
implementation loop:

1. **The preset** (`lyrical`, "Lyrical guitar"), data alone: tempo 70-95, up to 150 at the wild end, leaning slow; 4/4; natural minor and Dorian first,
   harmonic minor and Phrygian dominant at the middle and wild end for the Spanish colour and the ominous pieces, major
   at the plain end lower; seventh, add9, sus2 and minor-ninth shapes, chords a bar or two, few changes, vamps; the
   melody on an acoustic (steel or nylon) or a jazz guitar, then a clean one, repetitive (`rhythm.melody.repetition`
   high), its lines sung (`sung.halves`); the chords an acoustic or clean guitar's broken chords let ring, held tones
   now and then; the bass simple, on roots, repeating; the drums a steady downtempo beat on most sections, Hip-hop's
   lightly swung 16ths and syncopated kick on a standard or room kit, few fills, the drums resting at the wild end only
   (the drumless record); a heavy delay as a section's strong effect at the middle (`strongEffect.echo`), chorus,
   the hall; a cello as the counter-melody now and then, a cold pad at the wild end; the solo an overdriven guitar's, a
   build at the end of a song now and then (`solos`), the only distorted part; textures steady, no louder form, key
   change, stops, hits or big ending; songs of three to six minutes. Measured against Hip-hop and the Power ballad on
   64 songs each: the drums' share of sections, the beat's swing, the melody's repetition, the echo's share, the
   guitars' share of the parts; targets from that baseline. Asked: `lyrical-echo` (the delay against none) and
   `lyrical-beat` (the downtempo beat against a ballad's).
2. **The echo cascade**, a device of its own, then the preset's: a fast piece of a guitar alone, about a minute, at
   about 150, playing even 8ths, short and muted, its broken chords echoed once a dotted 8th later and almost as loud,
   so that the playing and the echo together sound an unbroken run of 16ths; a minor key's chords stepping down with
   their fifth in the bass (i, VII and VI as second inversions), arpeggios over the tonic, an add9 at the end. The
   echo is the synthesizer's delay (`Expression.EchoDelays`, `Midi.DelayFeedback`): its time drawn evenly among a
   dotted 8th, a quarter and an 8th, and its feedback fixed at about half, several repeats dying away. The echo's kind
   a genre's pool (`echo`: a dotted 8th, a quarter, an 8th, and the cascade, a dotted 8th repeated once, its feedback
   none and its level the note's), the songs' as now, evenly among the three; the cascade the echoing part's broken
   chords in even 8ths where it carries it, its notes short. Lyrical guitar's cascade at its wild end, where its tempo
   reaches 150 and its drums and bass rest more; Ambient's and Synthwave's now and then. Measured: the carrier's 8ths
   struck in a cascade's bars, its notes' lengths, the delay's time and feedback as written; asked as
   `lyrical-cascade` (the cascade against the half-feedback echo). The piece itself, a fast guitar alone, is the wild
   end's rare outcome, not a preset of its own.
3. **Guarded: turntable scratches** as a percussion sound (the kits' scratch keys, 29 and 30, if the soundfonts have
   them), only if listening misses them; checked first that both soundfonts sound them.
4. **Guarded: legato slides** (*Instruments and expression*), only if the lead's slides are heard as stutters.

Review: **overengineering**: a preset of choices that exist but the echo's kind, one pool generalising the echo's
fixed feedback and its even draw of a delay rather than a cascade mechanism; the scratches and slides behind listening; no
whammy, tapping or kill-switch devices (other records'); the delay the existing echo, not a delay of its own as
the rhythm, which the drumless companion record would want and the preset's wild end only approximates.
**Musicality**: the identity is the acoustic guitars, the delay and the steady beat together; General MIDI's acoustic
guitars are decent, the delay helps, but an echo's taps blur a fast figure, so heard; a beat too swung turns it
lo-fi and too plain a ballad, so asked; the sameness the reviews note is the style, but a generator's sameness
across songs is worse than an album's, so the facets vary the scale, the beat and the solo; the distorted solo only as
a build at the end, never over the acoustic theme; the minor's melancholy and the harmonic minor's Spanish colour, not
a major key's sweetness; the cascade only works with every 8th struck and the repeat almost as loud, a gap or a
long note breaking the run, and a song-wide delay echoing whatever else is sent to it, so the cascade's song sends one
part alone.

### Planned: band devices and the metal leftovers

Planned 13 October 2026 from the roadmap's priorities, reviewed the same day; each step through the implementation loop.
The architecture's three proposals (#168, #138, #126) and the drum solos' plan were found built and are not in it. In
order:

- **A.** Done (13 October 2026): the roadmap's stale entries out; Doom's blues scale reduced (the metal family's item 5).
- **B.** Done (13 October 2026, `BassOnKickTest`): **bass on the kick** (#379), a figure's `FollowsRiff` its `Follows`,
  the player it strikes with, the kick's `withRiff` the riff's and the bass's `withKick` the kick's, the fingerprint kept
  before the presets took it; Trap's bass on the kick 40% → 72% of its notes (63% of its bars on the figure), Hip-hop's
  47% → 65%, Rock's 52% → 59%, Country's 40% → 46%, Drum and bass's 45% → 55%, Punk's and Funk's a few points; in a
  bar on the figure 83% to 96% of the bass's notes with the kick, the rest its walks and fills. No heavy band's. Left to
  listening: `trap-bass-kick`, the 808 on the kick against its own line.
- **C.** Done (13 October 2026, `EnsembleHitsTest`): **ensemble hits** (#361), stop time's stage striking a section's
  last bar before a louder one too, its band on the kick's notes there, each part its note sounding at each, a chord
  held into the bar ended at its line, the drums whatever falls on a hit and their fill; never where a drop, stop
  time, a solo or a polymeter is, nor a bar of more hits than half its 8ths, which leave no rests (a double kick's), and
  a short last bar's hits before the next section's first beat. The songs' 0-3-3, as stop time's; Funk 20-30-40, Latin,
  Ska, Soul, Fusion, Jazz, Rock, Heavy metal, Thrash and Punk raised, Disco not (its kick on every beat would chop the
  quarters), the genres without a band's stops at 0. Songs struck in 26 of 64 (Funk), 24 (Ska), 21 (Soul), 20 (Fusion),
  18 (Heavy metal), 17 (Rock), 16 (Punk), 14 (Latin, Thrash), 6 (Jazz), 5 (Pop, the songs' own); 1.5 to 2.7 hits a bar.
  Left to listening: `funk-hits`, and the crash the band's hits want, not added.
- **D1.** Done (13 October 2026, `BigEndingTest`): **the big ending** (#348, #374), an ending of its own built of the
  ring-out's held home chord (one or two bars, slowing down half the time) and a button's last hit after it, the drums
  rolling over the hold in 16ths down the toms from the highest, the kick and a crash on every beat, as loud as a
  drop's build grows, and landing on the hit; the songs' never, Rock 60, Heavy metal 70, Thrash 60, Symphonic 50, Doom,
  Punk, Blues and Power ballad 40, Soul, Funk, Country, Ska and Nu metal 30, Black metal and Djent 20 beside their
  endings: 6 to 14 of 64 songs. Left to listening: `rock-big-ending`, the roll a machine's or a drummer's.
- **D2. The false ending** (#372), put off until the big ending is heard: a drop's kind before the last repeat, the
  band hitting the home chord, a beat or two of silence, the drums counting back in; its hit a harmony edit before
  `Realizer`, new work, and a gimmick worn thin quickly.
- **E.** Done (13 October 2026, `MelodyRangeReportTest`): the metal leftovers as questions. Nu metal's, Thrash's,
  Punk's and Trap's `melodyRange` at 0, the mechanism's narrowest, a section's melody 13.2 → 10.8 semitones (Nu metal),
  12.9 → 9.8 (Thrash), 14.5 → 11.7 (Punk), 11.7 → 9.5 (Trap); 20, Hip-hop's, narrowed only by two. Asked as
  `numetal-narrow`; a range of a fifth and repeated notes are code of their own, the range's floor about nine
  semitones, built only if narrower helps. And `numetal-chromatic` (without, `riffScale.chromatic` 0).

Review: **overengineering**: a field generalised and one figure, not a player following any other; ensemble hits one
stage with stop time, not a second, its growth in conditions watched; the false ending split off; the leftovers asked
before built. **Musicality**: an 808 on the kick Trap's sound, a rock bass on it idiomatic but static, so under its own
rhythm; no bass on a double kick at 200; hits on a four-on-the-floor kick only stop time, so heard; the drums striking
the hits with the band, a crash and the kick, the fill around them; the big ending the clearest rock gesture, its long
roll on General MIDI toms heard before its weight raised; a narrow line on a guitar possibly only dull.

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

- **A polymeter's bar giving way to its riff** (P3, the frame a power of two of bars done 11 October 2026,
  `Polymeter.Frames`, 4 bars 43%, 8 23%, 16 8% and 32 7% of the polymeters, Djent's leaned to 8 and 16, not told
  apart by listening, `djent-frame`): a section's last bar cut short or drawn out by the cycle's leftover, so that the riff ends its
  cycle with the section and the next starts on it, should listening ask.
- **Breakdowns beyond Djent, the rest** (P3, Punk's done 11 October 2026, 20% of its songs, heard
  and kept, `punk-breakdown`): more of Nu metal's, and a breakdown in a verse or before the last chorus beside the bridge,
  should listening want more.
- **Ghost notes beyond the backbeat** (P3): Funk, Soul, Fusion, Blues, Hip hop, Rock, Disco, Drum and bass, Reggae and
  Ska ghost on a backbeat's snare (`Ghosts`), Drum and bass fewer (`ghostNotes`, heard and kept as *dnb-few-ghosts*); a jazz drummer's comping on the snare and Latin's
  timbales are idioms of their own, a snare off the backbeat role and a kit without one, which ghosts do not reach.
- **The energy's recurrence** counts a section's appearances, not its plays: should long sections sound too quiet.
- **The chords and the bass,** which follow the energy weakly, should choruses not sound fuller; the melody sings a
  section's energy over the song's in its range's place (`MelodyLayers.EnergyRegister`), the chorus 2.6 semitones over
  the verse, higher in 61% of the songs.
- **The band's devices, the rest** (P3, each an edit after assembly among the stages, as stop time and pushes are):
  stop time's hits added where a part rests on the downbeat; a backing figure that recurs through the song.
- **Call and response, the rest** (P3, `call-response` heard and kept 11 October 2026, 5 of 7): the answer recurring as its bar
  patterns do by the scheme's letters, with the recurring backing figure. Built: a verse's melody singing each phrase's
  first half and the riff answering in the second, or the counter-melody's own line where no riff plays, a phrase none
  answers left whole (heard 11 October 2026: a cut melody with nothing answering it); the parts doubling a part cut
  with it (`CallAndResponse`, after `Doubling`).
- **Harmony's devices, the rest** (P3): a ii–V into a chord but the home, which a chord's own scale makes part of; the
  line cliché rising (the fifth, its sharp, the sixth) beside its falling one (`InnerLines`), should
  listening ask.
- **A section's parts chosen as its drum kit is** (`DrumKitGenerator.SelectKit`), leading and colouring roles, where
  now each part rests on its own (`Arrangement`).
- **The drums' changes by an appearance's energy** (their colour, a drum's stroke), which are the section's as it was
  made.

### Fills and lines

- **The melody's motif developed, the rest** (P3): the inversion cut in the review of 11 October 2026, heard on 8 October
  as hard to notice and worse in 5 of 6 Pop songs; a figure displaced by an 8th or its rhythm stretched, development a
  listener can follow, to try in its place and be heard.
- **Two toms at once** (P3, put off 10 October 2026, again with the cascade): a drum note's sound a list of
  articulations (`StateKinds.ArticulationIndex` now one, `TimelineEdits.Hit` replacing a hit at its place, the toms one
  track), so that a fill's wide window or a cascade's accent strikes a far pair of toms together, a rack and the floor;
  the cascade built without it, should listening ask for the weight.
- **Filling the melody's gaps before a chord change within a phrase,** should they sound empty now that the solo part
  answers.
- **The lift beyond the cues:** the chords' rhythm doubling, a part of the next section coming in early, should the
  crescendo and the drums' cues not lift.

### Instruments and expression

- **A song's drum stroke by its energy** (P3, by listening, October 2026): a genre leaning the open hi-hat as a stroke
  washes its loud sections more than its quiet ones only where a section draws the stroke anew; the song's own stroke is
  drawn with no energy lean, so Rock's quiet sections ride the open hi-hat in 11% of them against 17% of its loud ones
  (Punk's 5% against 25%). Should a quiet verse's wash be heard, the song's stroke leaned as a section's is.
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
- **Drum solos, the rest** (P3, by listening; the plan of October 2026 built, `drum-solo` heard better): a samba's or
  a baião's kick and a timbales player's cascara as its feet; its flams; a motif augmented or diminished; a timbales'
  abanico, the quote of the groove ghosted in Funk, and an IDM's drill of ratchets, each a gesture of its own.
- **A drum's pocket by its section's role** (P3, if a section's change of role is heard): the timing takes a drum's
  offset by its role in the song's groove (`Pocket.OffsetOf`), not a section's, which may move a ride to the time.

### Scales and chords

- **Free chords, the rest** (P3): a chord moved by semitones (`StateKinds.ChordShift`), planing (`Planing`) and a
  chord's own scale on its root (`ChordScaleKind.Own`) are built. Left: planing choosing the next chord by the voice
  leader's least move among free shapes, should it sound mechanical. Its uses past these: a fusion genre, a long-lined guitar player's, its harmony and
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
- **A melody in block harmony, the rest** (P3; `block` heard and kept, 9 October 2026): a block is the counter-melody
  stacking its chord's notes under its harmony note (`LineDoubling.BlockChance`, Soul's and Funk's 60%, Ska's and
  Latin's 50% of their harmonised sections), its passing notes alone, three voices on 56% of the notes struck with the
  melody, the melody on top 86%. Left: its passing notes harmonised too, a diminished chord or the scale's in parallel,
  should the two-voice passing notes sound thin; Jazz's soli, four voices close, which no part is free to play; the pad
  as a voice, which plays chords, not lines, and sat over the melody 81% of the time when tried.
- **Roots off the scale, the rest** (P3; `own-modes` heard and kept, 9 October 2026): a planed section's chords each in a mode
  of its own, major or minor as it makes them (`Planing.ModesChance`, `ChordScales.Planed`), the modes weighed by the
  progression facet and a genre's pool (`ChordScales.OwnModes`, `ownMode`: Fusion's Lydian, Dorian and melodic minor 82%,
  Phrygian 3%), Fusion's 72% of its planed sections, a chord's mode changing on 78% of the moves. The melody over
  planed chords is placed by where it aims, not moved with them: across a change it moves as the root does 24% of the
  time, against 14% elsewhere, and keeps its note 8% against 11% (`PlaningTest.ReportTheMelodyAcrossPlanedChanges`,
  Fusion's and Jazz's 128 songs). A mode chosen to keep a note of the chord before was tried and let go of (9 October
  2026): 38% of the planed moves keep one as drawn, a lean of odds 2 made it 42%, of 4 45%, and always the mode keeping
  the most 50%, as a move of a semitone or a whole step keeps no note whatever the qualities, the moves deciding it
  (`Planing.Moves`), which favour small steps so that the voices slide. Should the modes be heard as aimless, the move
  is the lever, a planed chord's move drawn with its mode, or a chord keeping the mode of the one before now and then;
  symmetric cycles (major or minor thirds) left out, Planing's moves holding minor thirds already.
- **Decided in the review of October 2026, not built:** a genre's one rule, which it keeps already, its amounts in
  place of the songs' but a choice drawn in roles, written down on `ByConvention`; a genre resolved once, which the
  request does already (`GenerateSongRequest` checks every choice and option); streams by name (they would move every seed and so lose the
  fingerprint as a refactor's check, for one line saved a stream); a stage's module taking the song's decisions whole
  (it moves the unpacking from `SongStages` into the module, and a module's parameters say what it reads); a stage
  declaring what it reads and writes (`SongDraft`'s fields say who makes them, and CLAUDE.md the two phases). A new
  stage takes `(SongDecisions, SongDraft)`, as fifteen do.
- **Plainer prose:** comments that say in a plain sentence what the code cannot, and commits' lines of about a hundred
  characters; CLAUDE.md's paragraph on conventionality as rules a test can check.
- **The server's figures read in full** (P4): the dashboard reads every song made in its window, nothing at hundreds a
  day and slow at the 200,000 a day the table keeps at most; then the spreads would be worked out a day at a time.
- **Tests that check less than their names** (P4, from the review of 9 October 2026; a breakdown's riff chugging and its
  kick on the riff's notes done 11 October 2026, and the solo's approach off the beat let go of, its player not told by its
  notes): the louder and quieter forms per appearance, both ways (with *A section's form*); each passing kind's shape
  (the tritone substitute's dominant, the half-diminished's whole step); the melody's joins at every pattern; the
  busyness reports' facets read from the trace; the MIDI file parsed for its program changes; the stages' hand-offs,
  each read after it is made.
- **The page's tests of sound** (P4): the page tests drive asking for songs, not hearing them; playing, seeking, the
  MP3 export and the lock screen's controls are tried by hand, as are Safari and phones.
- **A report of thousands of songs** would make, measure and let go of its seeds past the shared corpus rather than
  cache them; a corpus song keeps about 3 MB, every note's state the most of it, and its making allocates 47 MB
  more for a moment.

## Genres

A genre is the page's data (`Genre`, `GenreChoices`, `Client/genres.json`), every option three amounts, at the plain
end, the middle and the wild end; what each choice does is in the code and the numbers of each in its commit. What is
left:

- **Any genre** (P3, a plan in steps): a song asked for with no genre drawing a preset, or the songs' own as one of its
  outcomes, evenly, so that the plain band stays among them. Done: the presets the server's (`GenrePresetFile`, built
  in), the tests' and the CLI's (`--genre <id>`, 11 October 2026); the page's Any genre draws a preset other than the
  song's, and every event carries the genre, which the dashboard rates by. Put off (11 October 2026): the server
  drawing it from the seed, the page asking for any, as a draw by the seed cannot keep the page's rule of another
  preset than the last, and a link already names the preset drawn; taken up should the API or the CLI want any. Left:
  the default (the songs' own, as now, or any genre) decided by the ratings by genre once a version has enough, and
  not before the retuned presets, Pop and Metal most, are heard. Not weighed by the presets nor leaned by
  conventionality: each preset spans the plainest songs to the wildest.
- **Rock and metal as their bands play them** (P3, from 8 October 2026): what each lacks against the sound of a riff-driven
  hard rock band, a blues-rock band's shuffle, a twin-guitar heavy metal band, thrash and groove metal
  (`RockAndMetalReportTest`). Their solos (73% and 78% of songs) and their riffs of two and four bars (AAAB and ABAB a
  third of their sections) were there already; the organ's solo, Rock's shuffle at its plain end and Metal's clean
  quiet sections are presets now. Left:
  - The riff between the sections (P3): one part alone is a texture already (`TextureKind.Alone`), the riff a genre's
    weight of it, but not placed as an interlude of its own.
  - A melody sung rather than played (P3, `pop-sung-lines` asked first): no singer in General MIDI, so the melody
    stands for a voice by how it phrases, not by its sound. Measured (`SungMelodyReportTest`, 128 songs): its moves
    already sing (repeated 28%, steps 33%, thirds 19%, over a sixth 3%, legato 96%); every phrase of a pattern ends in a
    rest, and a sung section's at half its phrase too (`GenreChoices.Sung`, `PhraseRestTest`), a run outlasting two bars
    18% of the time; its rhythm does not: 20% of bars carry eight notes or more, 29% of its onsets a 16th or less apart,
    68% of phrases start on a downbeat. Left, step 3: a sung section's melody no finer than 8ths, its parts' cap
    (`CompositionStateKinds.ShortestNote`, the calm sections' own), drawn per section from a stream of its own by a second
    option of the same choice (`sung.eighths`): the songs' 100-50-0, the plain sung genres' (Folk, Country, Ballad, Rock,
    Punk, Blues, the metal family) high, Pop's, Soul's and Funk's middling, Hip-hop's and Trap's low, as their lines
    syncopate in 16ths. Measured as written (before swing): a 16th or less between onsets 30% in Pop, 29% the songs',
    25% Folk, 22% Blues, 11% Hip-hop; the target about half, the triplet's 8ths kept (1-3% now). Asked as `pop-eighths`.
    Cut on review: the busyness leaned down (the cap does it, eight notes a bar at most), the repeat chance up (28%
    already), a phrase starting after the downbeat (73% of Pop's start on it, against no measure of a sung line; by
    listening, should the cap's lines sound square). Not: lyrics or syllables, melisma,
    a singer's vibrato or scoops by pitch bend, Voice Oohs as a lead by default (a pad's slow attack; a weight only by
    listening). A genre's instrumental lines (Techno's and IDM's leads, a riff) keep the choice low, so it is a lean,
    never a rule over every melody.
- **Black metal and IDM, the rest** (October 2026, `NewGenresReportTest`): Black metal tremolo-picks 51% of its
  sections, blasts 20%, double-kicks 20%; IDM, warm and swung at its plainest, an acid line, odd meters (47% of its
  wildest songs), polymeters and rolls at its wildest, breaks 24% of its sections. Left:
  - Rolls splintering into 32nds, a breakbeat chopped and drilled, figures finer than a 16th (P3, by listening): the
    figures' grid in 32nds, should IDM's wildest want more than its hi-hat rolls and fills give.
  - Field recordings, a prepared piano, glitch edits (*Parked*, beyond General MIDI).
- **The hook and the phrase scheme as one** (P3): a hooked section a scheme sung literally, should a trial keep the
  report's repeats (50% of a hooked section's notes at their first bar's places); otherwise kept apart, with the reason.
- **The choices grouped into families** (P4): bass, harmony, form, drum devices, one choice of several chances rather
  than fifteen of one, which would read better in the editor; should the editor grow hard to use.
- **What the genres cannot play yet** (P3, each by what listening to the presets says; `GenreDevicesReportTest`,
  `GenreIdiomsReportTest` the measures):
  - The loop's figures shared, the bass's and the riff's, beside its harmony (`GenreChoices.Loop`).
  - A riser as a lift cue, into a louder section where the band plays on, the riser's own channel there now
    (`Risers`), should a lift want one.
  - The pump on the kick wherever it falls, should a pumping genre's kick leave the beat; it ducks on the beat now.
  - The pinned drums (`GenreChoices.Pins`) in the bars a drop or an entry clears, and the wilder songs that leave a
    role free.
  - The montuno's own rhythm, a two-bar figure anticipating the chord, should the part's syncopated rhythm under
    `ArpeggioPattern.Montuno` (root on the beats, the others off them, Latin's, heard and kept as *montuno*) sound loose.
  - More figures (P3, by listening): a figure for the hi-hat or the snare, a figure of more than
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
- **A tag** (put off twice, 8 and 10 October 2026): half a pattern before a section changes, which breaks the pattern's grid for the fills' lines and
  the phrases; with the meter as state, an engine's change for a rare idiom.
- **A parallel CLI batch** (P4): `--count` making its songs in parallel; a song takes about a second.
- **The rare devices pruned** (P4, the review of October 2026): a device that plays in under about 1% of songs, with
  and without genres, and was never heard as good, taken out or folded into another, the options at
  `ByConvention(0, 0, 0)` that genres alone reach first; should the stages grow too many to keep.
- **The wild end's one device** (P4, the review of October 2026): a wild song stacks key and tempo changes,
  polymeters, every gentle effect and a strong one; one or two devices a song as its signature, leaned by its facets,
  should the ratings fall at the wild end (the ratings by conventionality).

## Decided, and kept

- **Analytics written off the request** (decided 11 October 2026): an event is written in 0.042 ms
  (`EventStoreTest.Report_AnEventsWrite`), so a burst of ten thousand holds the lock under half a second; a queue's
  writer would answer no sooner that anyone could tell. The admin password's pause is built (`AdminArea.WrongPasswordPause`).
- **A heavy band's time and backbeat** (decided 11 October 2026): every section keeps time and a kit's bars sound both
  98.5% of the corpus's, Black metal's 98.8% (`KitRolesTest`); the snare under driven guitars is 8 to 11 dB down in a
  render's mids, the sound set's, not the MIDI's, so nothing in the song changes for it.
- **A mix written by the page** (decided 11 October 2026): a change of the mix asks the server for the file again for
  the download alone; writing MIDI in JavaScript to save that request would keep two writers of one format.
- **The diminished scale and tremolo picking as their own items** (decided 11 October 2026): the first is a scale of
  eight notes, against *Harmony in seven steps*; the second is Black metal's figure already, 51% of its sections.
- **Harmony in seven steps, lines in any scale** (decided 9 October 2026, after a trial on 8 October and two reviews):
  scales of other sizes as the harmony's, a section's chords built and moved in a pentatonic, whole-tone or octatonic
  scale, will not be built. The genres play as the code does: harmony from a seven-note scale, melodies gapping it,
  rock's, blues', soul's and metal's pentatonic and blues lines over it, jazz's melodic minor, whole-tone and
  diminished lines over chords moving by its degrees; the lines take any scale already (`LineScales`, the pentatonic
  68% of the plain end's lines). Harmony from another scale is another language, not this one with fewer notes: a
  pentatonic's chords are sus and quartal with no dominant, which the shapes play (Quartal triad, Quartal stack,
  So What, Sus2, Sus4); a whole-tone scale's are all augmented, every move alike, a passage's colour rather than a
  section's harmony; an octatonic's are diminished and 7♭9 moving in minor thirds, shapes and planing's moves both.
  And the seven steps are not arithmetic to generalise but the devices themselves: the root motions (down a fifth,
  ii–V), the degrees genres lean to (♭VII, ♭VI), the cadence's raised seventh, applied dominants, borrowed chords and
  the doublings' thirds and sixths mean something only in a scale with a fifth, a seventh and a parallel mode, so that
  counting them in semitones would leave most without a meaning in a pentatonic (from E in C's, a fifth up is not in
  it) and the code with a branch for every scale that is not seven, for harmony no preset plays; the trial failed at
  once at the cadence (`Progressions.GetCadenceWeights`). What it was wanted for is built or planned: the line scales,
  the shapes, planing, a chord's own scale, and roots off the scale (a planed section's own modes, *Roots off the scale, the rest*). Taken up only for a genre
  whose harmony itself is gapped, an East Asian pop or a modal pentatonic folk, as that genre's own progressions beside
  these rather than these resized.

- **Black metal ends both ways** (heard 8 October 2026, 2 better, 3 worse, 1 not told apart): a raw band's modal ending,
  ♭II, ♭VI or ♭VII into i, the most often, and a symphonic band's classical V–i now and then, its last chord V 22%, its
  leading tone raised 5% (`cadenceDegree`, `cadenceRaise`); the songs' chances of the raise and the chord into home as
  they were.
- **Long cycles, decided against** (10 October 2026, after two trials on 9 October): a part's drawn rhythm runs one
  cycle over a pair of bars or more, its second bar going on from its first; a bar whose period is longer plays the
  first half of a two-bar cycle (`Meter.GetCycles`), a sparser bar, which the counter-melody's bar patterns draw 70% of
  the time, the bass's 33% (`TwoBarCyclesTest.ReportSlowPatterns`). Played whole over a pair, it changed 230 of 256
  songs and left the second bars empty, its half a rank weaker (the counter-melody's 17% fewer, the bass's 10%,
  `TwoBarCyclesTest.ReportBarsByPlace`); with its notes held across the cycle, the lines' holes closed but the chords',
  the rhythm part's and the counter-melody's stayed, and every slow part grew sparser, the parts' densities tuned
  against a bar of the cycle. The bar is the engine's unit, of the scheme's letters, the state's layers, the melody's
  question and answer, the drums' presence, the fills' lines and the feels; and what two bars of a part give, a second
  bar that answers the first, the schemes (AABA, ABAB), the varied repeat (A′), the question and answer, the phrases of
  three and of four and two, two-bar figures (the claves) and chords of two bars give already. A cycle across the bar
  would cost the engine's unit and a retuning of every slow part for a refinement no listening has missed. A kick
  answered in its second bar or a crash every two bars, should listening miss one, is a figure's data or an edit after
  assembly, as the lift's cues are.
- **Targets from outside, decided against** (10 October 2026): measures of a corpus of real MIDI songs to tune towards
  (the notes a bar by role, the intervals, the syncopation, the chord changes a bar); the songs are tuned by listening
  and the ratings alone.
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
