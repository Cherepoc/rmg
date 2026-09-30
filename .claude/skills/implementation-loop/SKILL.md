---
name: implementation-loop
description: Implement a feature or a roadmap plan step by step, each step planned, critically reviewed, implemented, measured, reviewed again and committed. Use when asked to "enter the implementation loop", "implement every step", or to work through a roadmap plan.
---

# The implementation loop

Work through the task given (a feature, a plan in `ROADMAP.md`, or the roadmap's priorities) one step at a time, without
stopping to ask between steps. Stop only when every step is done, when a decision is genuinely the user's (ask it, with a
recommendation), or when a step fails its reviews twice (postpone it in `ROADMAP.md` with the reason and go on).

## Each step

1. **Plan.** Read the code the step touches. Say what changes, where, why, and how it will be measured: the report
   tests, the targets, and what the corpus should and should not change. Measure a baseline first where the step
   changes behaviour.
2. **Critical review of the plan.** Check it against `CLAUDE.md`'s principles (simplicity, generalization, state
   decides, leaning through data, streams, no silent defaults), against what is musical, and against whether it is
   worth doing. Name what is wrong, risky or guessed, and revise the plan. Repeat until it is doable, or postpone.
3. **Implement.** Follow the surrounding code's style and comment density. Build, and run the whole test suite.
4. **Measure.** Run the reports before and after; compare against the targets. A refactor must keep the corpus
   fingerprint (`dotnet Rmg/bin/Release/net10.0/Rmg.dll --fingerprint`); a change in behaviour says in the commit what
   it changed, in numbers.
5. **Implementation review.** Review the diff critically: correctness, principles, dead code, tests reading the
   trace's typed values, what the measurements do and do not show. Fix, edit or undo, and measure again.
6. **Commit.** One summary line, no body, no trailers, on master. Do not push or deploy.
7. **Roadmap.** Remove what the step implemented from `ROADMAP.md`, add what it deferred, and keep its sections intact
   (`grep -c "^## " ROADMAP.md` before and after).

## Reporting

Give the user a short status between steps: what was done, the numbers, and what is next. At the end, a summary of
every step with its commit, its measurements, and whatever was postponed or left to listening.

## Tools

- `dotnet` is `~/.dotnet/dotnet`: `export PATH=$HOME/.dotnet:$PATH`.
- Build: `dotnet build Rmg.slnx -c Release --nologo -v quiet`.
- Tests: `dotnet run --project Rmg.Tests -c Release --no-build`; one class, explicit report tests included:
  `-- --treenode-filter "/*/*/ClassName/*" --output detailed`.
- The corpus: `TestCorpus.Range(n)`, `TestCorpus.Measure(n, song => ...)` and `TestCorpus.InParallel`, with song counts
  in powers of two.
