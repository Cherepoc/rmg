# Principles

- **Algorithmic simplicity over realism.** The generator does not simulate players. A simple, general rule that sounds
  good beats a faithful model of how a drummer or a pianist would do it; drop a realistic constraint when it costs
  special cases.
- **Generalization over hardcoding.** Prefer one mechanism that covers several cases over a case each, and a drawn or
  derived value over a fixed table or an order chosen by hand. Where convention matters, pull the draw toward it by the
  section's conventionality instead of hardcoding the conventional choice.
- **Conventionality has two ends.** A song has an unconventionality (`Unconventionality`) from 0, the plainest, to 1,
  the wildest, and a value of it for each facet of the music, such as its feel, its chords or its form, drawn around the
  song's and moved a little by a section, so that a song can be plain in one way and wild in another; every choice leans
  by its facet's value. At 0 a choice plays only the options its plain end allows, at their plain weights, and at 1 only
  those its wild end allows, at their wild weights; between, an option's weight follows a smooth curve from its plain
  weight through its tuned weight at 0.5 to its wild weight (`ByConvention`). A chance follows its own curve, so that an
  unconventional thing never happens at 0 and happens every time at 1, and a conventional one, such as a song in one of
  the forms, the other way round. Which options each end allows, and how heavily, is data on the choice, never a special
  case. Where the song decides a thing and its parts or notes then take it, such as an effect, the song's chance reaches
  its end and the parts' share may stop short of it, so that the wildest song plays the thing every time but not
  everywhere; and a thing that is no more apt in a wild song than in a middling one, such as a riff harmonised, may stop
  short at the song too, never at 0 and as often at 1 as at 0.5, the wild end choosing how it is played instead. The
  facets reach the ends only together, as the song's value does; songs are spread well away from both, and whatever an
  end makes, however wild, is meant. A genre's pool keeps to the options it names: where it allows none at an end, that end
  plays the nearer end's (`ByConvention.Filled`), so that a song of the genre plays one of its options at every
  conventionality; but a mood given past a genre's span reaches the songs' own options that lean its way, the farther
  the more (`Genre.AtMood`).

# Architecture

- **State decides, `Realizer` realizes.** Generation decides the notes as layered state (song, section, track, bar,
  note); `Realizer` only chooses their register from what came before (a chord's voicing) and leads into what follows. A
  decision that needs the song put together, such as a fill, the form or a stop, is an edit after assembly, not a render
  flag. The stages after assembly run in one order (`SongStages`): those before `Realizer` edit the state, those after
  it edit the notes, and can no longer change a chord's voicing or a line's placement. A stage that hands another its
  work does so through `SongDraft`, and comes before it.
- **Lean, don't decide.** Bias a choice by conventionality through its ends (`ByConvention`), and by energy, a line's
  weight or anything else with `Tilt`: an option's weight times the odds to the power of its lean, a chance through its
  odds, a value around its middle by its skew. A lean is data on the option; never multiply or cap a chance by hand.
- **Ask a track's role** (`TrackRole`), never its number.
- **A decision reads what it is about.** A decision depends only on the things it concerns, never on another part's
  decisions by the way: the band's lift into a louder section follows the sections, not the drums' fill lines. An edit
  to the whole band, such as a stop, reaches every track of the song, not those one section's timeline happens to hold,
  as later stages add parts such as a soloist or a riser. A part of the song made from another, such as a hook intro
  from its chorus, is treated by every stage as its source is, so that it stays what it quotes.
- **Streams.** Every stage, section and part draws from its own stream (`SongStream`, `Seeds.Derive`), a part's keyed by
  its role or track and never shared with another part's, so that a change to one leaves the others as they were. A
  new decision draws from a stream of its own, or keeps the number of draws, so that with its effect off the corpus is
  unchanged (`CorpusFingerprintTest`). Seeds are 64-bit `ulong`s and keys 64-bit `long`s, never cut to `int` or `uint`.
- **Songs change on purpose.** A refactor keeps the fingerprint where the right code keeps it, but code is never bent
  to keep it (draw orders kept by hand, helpers shaped round a draw, lookups left mixed); the change is made
  deliberately instead, and its commit says how many of 256 drawn songs it changed, their MIDI compared with the
  build's label, which names its commit, left out.
- **Measure.** Measure a change in behaviour on the corpus before and after it (`TestCorpus`, explicit report
  tests), and tune it against the numbers. Tests read the trace's typed values (`StateTraceEntry.Value`,
  `TracePoints`), never its descriptions. A change that redraws the songs meets songs the tests never met, and some
  fail for reasons older than it: each failure is checked against the committed code over the same songs (a
  `git worktree`) before the change is blamed or the test relaxed; a rule that fails is a bug to fix, a calibration
  that drifts a sample to widen.
- **Rules and calibrations.** A test asserts a rule, what must hold of every song, such as every note in its chord's
  scale. A measured share is a calibration: its test keeps a wide bound against the mechanism breaking, prints what
  it measures, and its target lives in an explicit report, so that a musical change moves numbers rather than fails
  tests. It measures 64 songs or more, the range seen written beside its bound, and compares the same songs with the
  mechanism on and off rather than one genre's songs with another's, as a few songs otherwise decide the share.
- **Drawn or given.** A value the song draws, such as its unconventionality, follows the generator's taste, which may
  keep songs away from the ends; a value supplied in its place (`SongOverrides`) is used as given, a facet set outright
  as the song's, its sections straying a little around it as around a drawn one, and a base as the centre its facets
  and sections stray around, so that a supplied 0 or 1, which nothing strays from, is the plainest or the wildest
  song. Every such value draws from a stream of its own, so that supplying one leaves the rest of the song as the seed
  made it.
- **No silent defaults** in wiring: a missing argument or entry fails.
- **The page is tested in a browser** (`Rmg.Tests.Page`): a change to `Rmg.WebApi/Client/` runs those tests, and a new
  behaviour of the page gets one there, rather than being tried by hand in Chrome, which is for looking at the page.

Planned work and the decisions behind it are in `ROADMAP.md`.
