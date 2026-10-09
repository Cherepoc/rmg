---
name: full-review
description: Review the whole codebase with many agents, every file read in full by one of them, every cross-cutting concern, every musical device and every genre's presets followed by another, the suite run on other songs first, every finding verified, and a report written. Use when asked for a "full review", to "review everything", to "rerun the review", or to have agents read all of the files.
---

# The full review

A review that reads every line rather than a sample, and runs the code before it reads it. One reviewer has one context
and skims what does not fit, so coverage is made a contract: every file is given to exactly one reviewer, in slices
small enough to read whole, each reviewer accounts for every file it was given, and the accounts are checked against
the file list. Cross-cutting reviewers follow one concern each across the code, device reviewers one family of musical
devices end to end and against each other, genre reviewers one family of presets; verifiers check every finding, and
the result is a report, not commits.

A run costs about sixty reviewers and twenty-five verifiers, with three runs of the suite. Confirm with the user
before starting where they did not ask for the full review by name, and ask whether to include the tests (about half
the slices) and which model the agents use (the current one by default: the user chose it over a faster one).

## The files

- **The review's folder**, outside the repository and kept between sessions: `R=~/Projects/sw/rmg-reviews/<date>`.
  The latest earlier folder there is the last review, `L`. Nothing is written in the repository.
- **The briefs**, this skill's own (`S=<repo>/.claude/skills/full-review`): `primer.md` (the project, the checklist,
  the output), `angles.md` (the cross-cutting concerns, the device families, the genre families), `verify.md` (the
  verifiers' brief) and `decided.md` (findings decided against or kept on purpose, which no reviewer raises again).
- **The scripts**, in `$S/scripts`: `manifest.py`, `devices.py`, `run_findings.py`, `coverage.py`,
  `verify_batches.py`, `merge.py`, `compare.py`.

## Phase 0: what changed, and the briefs brought up to date

```
python3 $S/scripts/manifest.py <repo> $R            # --no-tests to leave the tests out
python3 $S/scripts/compare.py $L $R --files          # the files added, removed and grown since the last review
python3 $S/scripts/devices.py <repo> $R              # every genre choice and stage, by family
git -C <repo> log --since=<L's date> --oneline
```

The slicer cuts every source file (`.cs`, and the page's `.js`, `.css`, `.html`) into slices of about 2,200 lines by
subsystem. The device inventory sorts every genre choice and stage into six families; place any it reports unplaced
in `devices.py`'s table first. Then, for every file added and every commit since the last review, bring the briefs up
to date: a new stage or device named in the primer or the angles where it belongs, a mechanism they name that is gone
taken out, an angle a test now guards narrowed. Read the four briefs whole before going on.

## Phase 1: the runs

The main agent runs these, one at a time, under the memory cap (`systemd-run --user --scope -p MemoryMax=8G
-p MemorySwapMax=0 -p AllowedCPUs=0-$(( $(nproc) - 3 ))`), after a build:

```
dotnet run --project Rmg.Tests -c Release --no-build > $R/run/offset-0.txt
RMG_CORPUS_OFFSET=1000000 dotnet run --project Rmg.Tests -c Release --no-build > $R/run/offset-1000000.txt
RMG_CORPUS_OFFSET=2000000 dotnet run --project Rmg.Tests -c Release --no-build > $R/run/offset-2000000.txt
dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/*/EveryPreset_EveryChance" --output detailed > $R/run/every-chance.txt
dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/GenreDevicesReportTest/*" --output detailed > $R/run/genre-devices.txt
python3 $S/scripts/run_findings.py <repo> $R <offset> $R/run/offset-<offset>.txt     # for every run with a failure
```

Every failure becomes a finding (`findings/run-<offset>.json`, category `run`) with its seed and offset, for the
verifiers to trace. Runs on other songs find what reading does not: a second corpus found thirteen bugs, most of them
two devices meeting, which no reviewer of one file sees.

## Phase 2: the reviewers

About twelve at a time, in the background, every one read only (never building, never running tests).

**A slice reviewer per slice** (`$R/slices/<id>.txt`):

> You are a code reviewer on one slice of a full-codebase review. First read the primer at `$S/primer.md`, then
> `$S/decided.md`, then `<repo>/CLAUDE.md`, and follow the primer exactly.
>
> Your slice is "`<id>`". Its files (repo root `<repo>`) are listed in `$R/slices/<id>.txt`. Read every line of every
> one of them.
>
> Write your JSON to `$R/findings/<id>.json`. Read only: never edit repository files, never build or run dotnet.

**A cross-cutting reviewer per concern**, the numbered angles of `angles.md` but musicality, which the families below
take:

> You are a cross-cutting reviewer in a full-codebase review of `<repo>`. First read `$S/primer.md`, then
> `$S/angles.md`, then `$S/decided.md`, then `<repo>/CLAUDE.md`.
>
> Your angle is number `<n>`, "`<id>`". Cover it completely as angles.md describes.
>
> Write your JSON to `$R/findings/angle-<id>.json`. Read only: never edit repository files, never build or run dotnet.

**A device reviewer per family** (form, harmony, lines, drums, solos, sound), as `angles.md`'s *device families*
describe, its devices in `$R/devices/<family>.txt`, its output `$R/findings/angle-device-<family>.json`.

**A genre-fit reviewer per family of genres** (heavy, groove, dance, beat, folk, score), as `angles.md`'s *genre-fit
families* describe, reading `$R/run/every-chance.txt` and `$R/run/genre-devices.txt`, its output
`$R/findings/angle-genre-<family>.json`.

## The coverage check

```
python3 $S/scripts/coverage.py $R
```

It reports the slices and angles done, the findings by severity, invalid JSON, every file a slice did not read in full
or did not account for, and every device a device family did not account for. Give each gap to a fresh agent until
none is left: a reviewer's message that it finished is not coverage, the lists are.

## Phase 3: verification

```
python3 $S/scripts/verify_batches.py $R              # every finding; --high-medium for those alone
```

Every finding, the low ones too, as the fixing plans every one and a low finding unchecked costs more there than a
verifier here; batches of at most 30 by area. One verifier per batch:

> You are a verifier in a full-codebase review of `<repo>`. Read `$S/verify.md` and follow it exactly.
>
> Input: `$R/verify/<area>-input.json`
> Output: `$R/verify/<area>-output.json`
> Read only: never edit repository files, never build or run dotnet.

## Phase 4: the report

```
python3 $S/scripts/merge.py $R
python3 $S/scripts/compare.py $L $R
```

The merge drops the rejected and the duplicates, merges findings on the same file, category and nearby lines, and
writes `$R/report.md`: the totals, the proposed listening questions (the findings of taste), the high findings in full,
and every area's findings as a table, the runs' failures and the genre fit among them. The comparison names the
findings new, recurring and gone since the last review; read each recurring one before calling it a regression. Write
the summary at the top of `report.md`: what is in good shape, the high findings and what to fix first, the themes, what
recurs. Give the user the report and wait. Do not commit the review or its report.

## After the report

When the user asks for a plan, group every finding into packages by what they change together (the high findings
first, then each concern), each package its goal, approach, verification and critical review (overengineering and
musicality named), written in the review's folder. Each package goes through the implementation loop.

- A finding of **taste** is not a fix: it becomes a listening question (`ListeningQuestionsTest`, `?listen=`), its
  change made only once the user has heard it. A **mistake** is fixed.
- A genre-fit change is data in `Client/genres.json`, the presets' choices named whole (a chances choice's every option,
  so that naming one does not switch the others off).
- As findings are rejected or kept on purpose while fixing, add each to `decided.md` with its reason, one line each.
