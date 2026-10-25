---
name: full-review
description: Review the whole codebase with many agents, every file read in full by one of them, every cross-cutting concern, every musical device and every cluster of genre presets followed by another, the suite run on other songs first, every finding verified, and a report written. Use when asked for a "full review", to "review everything", to "rerun the review", or to have agents read all of the files.
---

# The full review

A review that reads every line rather than a sample, and runs the code before it reads it. One reviewer has one context
and skims what does not fit, so coverage is made a contract: every file is given to exactly one reviewer, in slices
small enough to read whole, each reviewer accounts for every file it was given, and the accounts are checked against
the file list. Cross-cutting reviewers follow one concern each across the code, device reviewers one family of musical
devices end to end and against each other, genre reviewers one cluster of presets each; verifiers check every finding
but those of taste, and the result is a report, not commits.

**Delta or full.** A delta review slices only the files changed since the last review's commit, which take in the last
review's fixes; the angles, device families and genre clusters still cover the whole code. It is the default where the
last review recorded its commit (`review.json`). A full review slices every file: when the user asks for a full re-read,
when the last review recorded no commit, or when the last full one was more than about a hundred commits ago. Each
review's comparison counts the new findings in files the last review read unchanged; while that count stays low, the
delta reads enough.

**One agent at a time.** Launch an agent, wait for its notification, check that its file exists and parses, then launch
the next. Run several at once only where the user asked for it, and as many as they said.

A full run is about 40 slices (about half of them the tests), 9 angles, the architecture and the foundations reviewers, 6 device families
and 8 genre clusters, about 25 verifier batches, and three runs of the suite; a delta run has as many slices as changed
files fill, and neither design reviewer. Confirm with the user before starting where they did not ask for the review by
name, and ask: delta or full, whether to include the tests, which model the agents use (the current one by default: the
user chose it over a faster one), and whether to run agents one at a time (the default) or several.

## The files

- **The review's folder**, outside the repository and kept between sessions: `R=~/Projects/sw/rmg-reviews/<date>`.
  The latest earlier folder there is the last review, `L`. Nothing is written in the repository until the fixes are
  done, when the review's record is (*The record*, below).
- **The briefs**, this skill's own (`S=<repo>/.claude/skills/full-review`): `primer.md` (the project, the checklist,
  the output), `angles.md` (the cross-cutting concerns, the device families, the genre clusters), `verify.md` (the
  verifiers' brief) and `decided.md` (findings decided against or kept on purpose, which no reviewer raises again).
- **The scripts**, in `$S/scripts`: `manifest.py` (the slices, the commit), `offsets.py` (the corpora), `devices.py`
  (the devices, the genre clusters, the stages), `runs.sh`, `ledger.py` (what each device costs and earns), `run_findings.py`, `coverage.py`, `verify_batches.py`,
  `merge.py`, `compare.py`, `status.py` (the fixing's bookkeeping), `bisect.sh` (a held-out failure to its commit);
  `load.py` and `areas.py` are shared.
- **The review's record**, `$R/review.json`: the commit read, the date, the mode, the offsets and the holdout.

## Phase 0: what changed, and the briefs brought up to date

```
python3 $S/scripts/manifest.py <repo> $R --last $L    # a delta; no --last for a full review; --no-tests to leave them out
python3 $S/scripts/offsets.py $R                      # two corpora drawn afresh, and a holdout for after the fixes
python3 $S/scripts/compare.py $L $R --files           # the files added, removed and grown since the last review
python3 $S/scripts/devices.py <repo> $R               # every device by family, the presets by cluster, the stages in order
git -C <repo> log --since=<L's date> --oneline
```

The slicer cuts the source files (`.cs`, and the page's `.js`, `.css`, `.html`) into slices of about 2,200 lines by
subsystem. `devices.py` exits 1 on a genre choice, stage or preset its tables do not place, and on a name in them the
code no longer has: put each right in its tables first. The stage list the primer points to (`$R/stages.txt`) is
written by it, not by hand. Then, for every file added and every commit since the last review, bring the briefs up to
date in what no script lists: a mechanism the primer or the angles name that is gone taken out, a new concern named
where it belongs, an angle a guard test now keeps (*The record*) narrowed or its checklist line taken out. Read the four
briefs whole before going on.

## Phase 1: the runs

```
$S/scripts/runs.sh <repo> $R
```

It builds and runs the reports' smoke test first, stopping where it fails: the review measures with the reports, and
one that has rotted is fixed before any reviewer reads its numbers (a review once spent its first package mending
them). Then the suite at the review's offsets and the four preset reports (the fourth, `sounding.txt`, how the parts sound together), one at a time under the memory cap, every
failure run again alone at its offset, and every failure a finding (`findings/run-<offset>.json`) with its seed and
offset: category `run` for the verifiers to trace, or `flaky` where it passed alone, a test racing under the suite's
load rather than a song breaking it. Then the device ledger, from the reports:

```
python3 $S/scripts/ledger.py <repo> $R               # ledger.md: each device's presets, plays, code, past findings, questions
```
 Runs on other songs find what reading does not: a second corpus found thirteen bugs, most of them
two devices meeting, which no reviewer of one file sees. The offsets are drawn afresh each review, as corpora fixed
across reviews are ones the fixes have been tuned to.

## Phase 2: the reviewers

One at a time (see above), every one read only (never building, never running tests).

**A slice reviewer per slice** (`$R/slices/<id>.txt`):

> You are a code reviewer on one slice of a full-codebase review. First read the primer at `$S/primer.md`, then
> `$S/decided.md`, then `<repo>/CLAUDE.md`, and follow the primer exactly. The review's folder is `$R`.
>
> Your slice is "`<id>`". Its files (repo root `<repo>`) are listed in `$R/slices/<id>.txt`. Read every line of every
> one of them.
>
> Write your JSON to `$R/findings/<id>.json`. Read only: never edit repository files, never build or run dotnet.

**A cross-cutting reviewer per concern**, the numbered angles of `angles.md` but musicality, which the device and genre
reviewers below take:

> You are a cross-cutting reviewer in a full-codebase review of `<repo>`. First read `$S/primer.md`, then
> `$S/angles.md`, then `$S/decided.md`, then `<repo>/CLAUDE.md`. The review's folder is `$R`.
>
> Your angle is number `<n>`, "`<id>`". Cover it completely as angles.md describes.
>
> Write your JSON to `$R/findings/angle-<id>.json`. Read only: never edit repository files, never build or run dotnet.

**The architecture reviewer**, on a full review only, the prompt of a cross-cutting reviewer with angle 12,
"`architecture`", as `angles.md`'s *architecture reviewer* describes: at most three proposals of another design, each
with what the current one has cost by the records' numbers and measurements, its migration and what it deletes. Run it
last of the reviewers, so that this review's runs are in `$R/run/`.

**The foundations reviewer**, on a full review only, the prompt of a cross-cutting reviewer with angle 13,
"`foundations`", as `angles.md`'s *foundations reviewer* describes: at most two proposals of another model the generator
makes music with (its rhythm, melody, harmony, form, voicing), each from counted evidence and with the experiment that
would decide it by ear, one part in one genre as a genre option asked against the current model; never machine
learning. Run it after the architecture reviewer, so that it reads the runs and that reviewer's proposals.

**A device reviewer per family** (form, harmony, lines, drums, solos, sound), as `angles.md`'s *device families*
describe, its devices in `$R/devices/<family>.txt`, the ledger in `$R/ledger.md`, its output
`$R/findings/angle-device-<family>.json`: every device end to end, and whether each earns its place, a cut proposed
only as a question heard with and without it.

**A genre-fit reviewer per cluster of presets** (`$R/genres/<cluster>.txt`), as `angles.md`'s *genre-fit reviewers*
describe; on a full review it also proposes at most two idiom gaps, devices the engine lacks, telling it so in its
prompt:

> You are a genre reviewer in a full-codebase review of `<repo>`. First read `$S/primer.md`, then `$S/angles.md` (its
> *genre-fit reviewers*), then `$S/decided.md`, then `<repo>/CLAUDE.md`.
>
> Your cluster is "`<cluster>`", its presets listed in `$R/genres/<cluster>.txt`. The reports are in `$R/run/`.
>
> Write your JSON to `$R/findings/angle-genre-<cluster>.json`. Read only: never edit repository files, never build or
> run dotnet.

## The coverage check

```
python3 $S/scripts/coverage.py $R
```

It reports the slices and angles done, the findings by severity, invalid JSON, every file a slice did not read in full
or did not account for, every device a device family did not account for, every preset its cluster's reviewer did not,
and a musical reviewer proposing more than three listening questions, three cuts or two idiom gaps. Give each gap to a fresh agent (or send the
reviewer back to cut its questions to three) until none is left: a reviewer's message that it finished is not
coverage, the lists are.

## Phase 3: verification

```
python3 $S/scripts/verify_batches.py $R              # every finding but taste; --high-medium for those alone
```

Every finding, the low ones too, as the fixing plans every one and a low finding unchecked costs more there than a
verifier here; and every `elsewhere` entry, which a reviewer noted outside its slice and which is otherwise in no table
(the half and double time named the wrong way round was raised by five reviewers this way and reached no table).
Findings of taste (cuts among them) are not sent, as reading cannot verify how a thing sounds; nor are architecture
proposals and idiom gaps. Batches
of at most 30 by area, one verifier each:

> You are a verifier in a full-codebase review of `<repo>`. Read `$S/verify.md` and follow it exactly.
>
> Input: `$R/verify/<area>-input.json`
> Output: `$R/verify/<area>-output.json`
> Read only: never edit repository files, never build or run dotnet.

## Phase 4: the report

```
python3 $S/scripts/merge.py $R                       # once: the numbers it gives are the plan's
python3 $S/scripts/compare.py $L $R
```

The merge drops the rejected and the duplicates, merges only findings that are the same issue (the same file and
category, and summaries of the same words), the most severe leading each group with every merged summary printed under
it, and writes `$R/report.md`: the totals, the listening questions ranked, the high findings in full, and every area's
findings as a table numbered by `n`, the verifier's severity with the reviewer's beside it where they differ. The
comparison names the findings new, recurring and gone since the last review, and how many of the new are in files
unchanged since it; read each recurring one before calling it a regression. Write the summary at the top of `report.md`:
what is in good shape, the high findings and what to fix first, the themes, what recurs. Give the user the report and
wait. Do not commit the review's folder or its report.

## After the report

When the user asks for a plan, group every finding into packages by what they change together (the high findings
first, then each concern), each package its goal, approach, verification and critical review (overengineering and
musicality named), written in `$R/plan/plan.md`, every finding put in its package by number. Three rules shape it:

- **The failing tests first.** The `run` findings, tests failing on the review's other songs, are the first package
  after the review's instruments, worked on a worktree of the review's commit before any package changes songs: once a
  fix redraws them, a failure may pass with its cause untraced (four findings were once decided "passes since the
  songs were redrawn, no cause found"). Each is traced to its cause and fixed, or its test set right, there.
- **Two steps a package.** A package that changes songs is planned as its behaviour, one commit counting the songs it
  changes, and then its tests and docs, the fingerprint kept: a commit changing two hundred of 256 songs at once with
  its tests beside it can be neither heard nor bisected by its parts.
- **Taste waits in a queue.** The findings of taste go to the package `listening`, not one worked in order: each is
  given its status as it is planned, "open: to ask" or "open: needs code", and asked in rounds as the user listens.
  The review is done when the other packages are, the queue listed open in the record.
- **The frame before its devices.** A genre's mistakes of the song as a whole (its solos against its sung sections,
  its lead's instrument, its forms as drawn) are planned before the taste findings of the devices inside it: a device
  asked or tuned inside a wrong frame is heard through it, and three rounds once went to a break on the tune that the
  genre's frame hid.

```
python3 $S/scripts/status.py $R init
python3 $S/scripts/status.py $R assign <package> <n>...     # moving a finding to another package is assigning it there
python3 $S/scripts/status.py $R check                        # until no finding is unassigned
```

Each package goes through the implementation loop, its findings read with `status.py $R show <package>` (never from
`merged.json`, whose order is another: 44 findings were once looked up there and never worked); `status.py $R summary`
gives every package's counts, commits, songs and what is not fixed, the report per package the user asks for. As each finding is
done, `status.py $R set "<status>" <n>...`, the status starting fixed, partly, decided, kept, taste or open, and the
package's commit with `status.py $R package <package> <commit> "<songs changed>"`. A package is done only when
`status.py $R check <package>` passes.

- A finding of **taste** is not a fix: it becomes a listening question (`ListeningQuestionsTest`, `?listen=`), its
  change made only once the user has heard it. A question's song without changes the device alone: a patch on an option
  no other device shares (one switching the band's dead notes off asks nothing of a soloist's), never a pool emptied
  (refused) nor its weights made even (most songs then draw the same), so that what is heard apart is the device.
  `Write` prints how far its songs differ; one under about 0.1 is not asked, as listeners have not told such apart,
  but a device of a few places (a bar's slide into a section, a note in a run, a roll at a bar's end) is asked whatever
  its measure, its songs heard from its places: the measure takes 16 s around a moment of it, and a small place is
  heard clearly (the owner kept the surf drop at 0.04, the chromatic approach at 0.03 and IDM's 64ths at 0.05 on
  23 October 2026, to be asked). A measure is no reason to cut. Every song is heard where its device is: at the start
  of the difference holding the device's place, after a stretch alike, from the bar before, never seconds earlier to
  cover a place unsure (leads stacked to 10 s once, of the same music in both); a place where nothing differs is the
  trace's mistake or a device not heard there, reported and not asked.
  That measure counts the notes that differ, not the quality asked of them: a question of a colour, a mood or a
  brightness, measures the colour first (a song's major chords at each end), as songs a mood moved by its draws alone
  were measured apart and heard the same. A question of a device that refers to other music (a break on the tune, a
  quote) checks that what it refers to is there to hear in its songs: the tune sung before, dense enough to follow, on
  an instrument the device is not. A question of a device that is a contrast within a song (a line heard plain and
  again tremolo-picked, a reprise) hears both sides in its one song, from before the first: a device drawn alike for
  every time a section plays cannot be asked so, its songs differing by a note or two (`blackmetal-tremolo` was asked
  so, its first song differing in its last note alone, on 25 October 2026). Before a question is asked its song is
  looked at where it starts: what differs there against what the question says. A question answered "can't tell" twice is not asked again, nor its device changed,
  until its songs are looked at: what the device plays there against what the question says it does (two such
  questions failed for causes found only by looking at the songs, neither the device's amount). Ask the first-ranked first, a round of ten at most, the answers
  collected from the test instance's dashboard (`/admin/api/summary`, its `verdicts`) before the next; a question
  needing code has it built only when its round comes. A question not asked by the record stays open in it, not
  queued in the roadmap unasked. A **mistake** is fixed.
- A genre-fit change is data in `Client/genres.json`, the presets' choices named whole (a chances choice's every option,
  so that naming one does not switch the others off).
- Every commit that changes songs reruns `ListeningQuestionsTest.Write` in the same commit, so that the listening
  file's songs are the songs.
- A **cut** is a listening question heard with and without the device. Where listeners cannot tell, or prefer it
  without, the device is taken out, its code, its choice, its tests and its presets' amounts, as a commit of its own
  that counts the songs it changes; where they hear it earn its place, it goes to `decided.md` as kept, so that no
  review proposes the cut again.
- An **architecture proposal**, a **foundations proposal** or an **idiom gap** is not a package's fix: review each as the implementation loop
  reviews a plan (overengineering and musicality named; for a gap, one mechanism over a case each) and give the user
  the verdict. An accepted one goes to `ROADMAP.md` as a
  plan (`status.py set "roadmap: <its entry>"`), a rejected one to `decided.md` with its reason (`set "decided: ..."`).
- As findings are rejected or kept on purpose while fixing, add each to `decided.md` with its reason, one line each.

## The record

Once the packages are done, run the suite at the holdout offset, songs neither the review nor its fixes have met:

```
$S/scripts/runs.sh <repo> $R --holdout               # its failures in $R/holdout/findings, triaged in the record
```

Each failure is checked against the code before the review's fixes, and a new one traced to the commit that brought
it:

```
$S/scripts/bisect.sh <repo> $R <holdout> <test method>   # in a worktree of its own; its log in $R/holdout/
```

A failure at the review's commit already is older than the fixes, a finding of the record. A new one's first failing
commit says what it is: one that changed the tests alone shows older behaviour a test now sees (a rule moved onto
songs that play the device, a larger sample), a finding of the record; one that changed code is read: a fix that broke
the song is fixed now, and one that made a trace or a rule honest (figures traced as laid, not as drawn) shows older
behaviour too. A test whose own assumption a fix made stale (a part a fix now lets play) is set right now. Of the
first review's three new held-out failures, none was the fixes' doing: one stale test and two older bugs newly seen.

Then the review's record goes in the repository, `reviews/<date>.md`, committed, its tables written by
`status.py $R record <file>`: the totals, the packages with their commits and the songs they changed, every finding
open or partly done with why, and every finding by package with what became of it. To them add, by hand:

- **The guards.** For each theme of the summary, a class of finding that came more than once, the test that now keeps
  it closed (a rule over every preset at Base 0 against a crash at a plain end, a lint of calibrations under 64 songs),
  or why none can; a theme with a guard has its checklist line in the primer or its angle narrowed or taken out. A
  guard is a test that fails when the class comes back, not a package that fixed every case: calibrations moved to 64
  songs or page tests rid of their waits keep their checklist lines until a lint keeps them.
- **The briefs.** Each mechanism the fixes added that a class of finding should now be judged against (bars asked of
  the map, keep-outs as held spans, an intro played as its source) named in the primer and its angle, so that the
  next review asks whether code uses it rather than raising the old class again.
- **The holdout:** its failures and what became of each.
- **A comparison with the last review's record**, whose statuses are brought up to date from this review (found again,
  or not, its file changed by the fixes or never), naming:
  - **fix loops:** an issue fixed one way and then back. Blame every line this review's fixes removed (`git diff -U0`
    over the fixes' range, `git blame` of the base): a removed line the last review's fixes wrote is a place to read,
    and one that undoes that fix's intent is a loop. Do the same within the review's own commits.
  - **fixes that made the next finding**, the findings found again (read each pair `compare.py` gives), and those
    decided again for want of a line in `decided.md`.
  - **the delta's reach:** the new findings in files unchanged since the last review, as `compare.py` counts them.
  - the review's own slips, such as a package committed with findings unworked.
