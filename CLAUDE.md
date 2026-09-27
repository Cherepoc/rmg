# Principles

- **Algorithmic simplicity over realism.** The generator does not simulate players. A simple, general rule that sounds
  good beats a faithful model of how a drummer or a pianist would do it; drop a realistic constraint when it costs
  special cases.
- **Generalization over hardcoding.** Prefer one mechanism that covers several cases over a case each, and a drawn or
  derived value over a fixed table or an order chosen by hand. Where convention matters, pull the draw toward it by the
  section's conventionality (`RhythmicUnconventionality`) instead of hardcoding the conventional choice.

# Architecture

- **State decides, `Realizer` realizes.** Generation decides the notes as layered state (song, section, track, bar,
  note); `Realizer` only chooses their register from what came before (a chord's voicing, the bass's octave, a
  melody phrase's octave) and leads into what follows. A decision that needs the song put together, such as a fill,
  the form or a stop, is an edit after assembly, not a render flag.
- **Lean, don't decide.** Bias a choice with `Tilt`: an option's weight times the odds to the power of its lean, a
  chance through its odds, by conventionality, energy or a line's weight. A lean is data on the option; never
  multiply or cap a chance by hand.
- **Ask a track's role** (`TrackRole`), never its number.
- **Streams.** Every stage and section draws from its own stream (`SongStream`, `Seeds.Derive`). A new decision
  draws from a stream of its own, or keeps the number of draws, so that with its effect off the corpus is unchanged
  (`CorpusFingerprintTest`); a refactor must keep the fingerprint.
- **Measure.** Measure a change in behaviour on the corpus before and after it (`TestCorpus`, explicit report
  tests), and tune it against the numbers. Tests read the trace's typed values (`StateTraceEntry.Value`,
  `TracePoints`), never its descriptions.
- **No silent defaults** in wiring: a missing argument or entry fails.

Planned work and the decisions behind it are in `ROADMAP.md`.
