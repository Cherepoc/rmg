# Principles

- **Algorithmic simplicity over realism.** The generator does not simulate players. A simple, general rule that sounds
  good beats a faithful model of how a drummer or a pianist would do it; drop a realistic constraint when it costs
  special cases.
- **Generalization over hardcoding.** Prefer one mechanism that covers several cases over a case each, and a drawn or
  derived value over a fixed table or an order chosen by hand. Where convention matters, pull the draw toward it by the
  section's conventionality instead of hardcoding the conventional choice.
- **Conventionality has two ends.** A song has a conventionality from 0, the plainest, to 1, the wildest, and a value of
  it for each facet of the music, such as its feel, its chords or its form, drawn around the song's and moved a little
  by a section, so that a song can be plain in one way and wild in another; every choice leans by its facet's value.
  At 0 a choice plays only the options its plain end allows, at their plain weights, and at 1 only those its wild end
  allows, at their wild weights; between, an option's weight follows a smooth curve from its plain weight through its
  tuned weight at 0.5 to its wild weight (`ByConvention`). A chance follows its own curve, so that an unconventional
  thing never happens at 0 and happens every time at 1. Which options each end allows, and how heavily, is data on the
  choice, never a special case. The facets reach the ends only together, as the song's value does; songs are spread
  well away from both, and whatever an end makes, however wild, is meant.

# Architecture

- **State decides, `Realizer` realizes.** Generation decides the notes as layered state (song, section, track, bar,
  note); `Realizer` only chooses their register from what came before (a chord's voicing) and
  leads into what follows. A decision that needs the song put together, such as a fill,
  the form or a stop, is an edit after assembly, not a render flag.
- **Lean, don't decide.** Bias a choice by conventionality through its ends (`ByConvention`), and by energy, a line's
  weight or anything else with `Tilt`: an option's weight times the odds to the power of its lean, a chance through its
  odds, a value around its middle by its skew. A lean is data on the option; never multiply or cap a chance by hand.
- **Ask a track's role** (`TrackRole`), never its number.
- **Streams.** Every stage and section draws from its own stream (`SongStream`, `Seeds.Derive`). A new decision
  draws from a stream of its own, or keeps the number of draws, so that with its effect off the corpus is unchanged
  (`CorpusFingerprintTest`); a refactor must keep the fingerprint.
- **Measure.** Measure a change in behaviour on the corpus before and after it (`TestCorpus`, explicit report
  tests), and tune it against the numbers. Tests read the trace's typed values (`StateTraceEntry.Value`,
  `TracePoints`), never its descriptions.
- **No silent defaults** in wiring: a missing argument or entry fails.

Planned work and the decisions behind it are in `ROADMAP.md`.
