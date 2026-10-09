---
name: full-review
description: Review the whole codebase with many agents, every file read in full by one of them and every cross-cutting concern followed by another, the high and medium findings verified, and a report written. Use when asked for a "full review", to "review everything", to "rerun the review", or to have agents read all of the files.
---

# The full review

A review that reads every line rather than a sample. One reviewer has one context and skims what does not fit, so
coverage is made a contract: every file is given to exactly one reviewer, in slices small enough to read whole, each
reviewer accounts for every file it was given, and the accounts are checked against the file list before the findings
are trusted. Cross-cutting reviewers follow one concern each across the whole code, verifiers check the findings, and
the result is a report, not commits.

The run costs about fifty agents, several times a normal review. Confirm with the user before starting where they did
not ask for the full review by name, and ask whether to include the tests (about half the lines) and which model the
agents use (the current one by default: the user chose it over a faster one for the slice readers).

## The files

Everything lives in a folder of its own in the scratchpad, `R=<scratchpad>/review-<date>`; nothing is written in the
repository. The briefs are this skill's own (`S=.claude/skills/full-review`):

- `primer.md`: the project, the checklist and the output every reviewer follows.
- `angles.md`: the cross-cutting concerns.
- `verify.md`: the verifiers' brief.
- `decided.md`: findings decided against or kept on purpose in earlier reviews, which no reviewer raises again unless
  the code changed since.

Read the four before starting and bring them up to date where the code has moved on (a stage, a mechanism or a
principle the primer names that no longer exists, an angle that a test now guards). Agents read them from the skill's
folder by absolute path.

## Phase 0: the slices

```
python3 $S/scripts/manifest.py <repo> $R          # add --no-tests to leave the tests out
```

It lists every source file (`.cs`, and `.js`, `.css`, `.html` of the page) with its lines, groups them by subsystem
and cuts each group into slices of about 2,200 lines, merging the small ones. It writes `$R/manifest.json` and one
`$R/slices/<id>.txt` per slice, and prints them. Look at the grouping: a new file lands in its subsystem by its name,
else in `composition-rest`; adjust the themes in the script where a new subsystem deserves its own.

## Phase 1: the slice reviewers (one per slice)

Spawn one agent per slice, about twelve at a time, in the background, each with this prompt:

> You are a code reviewer on one slice of a full-codebase review. First read the primer at `$S/primer.md`, then
> `$S/decided.md`, then `<repo>/CLAUDE.md`, and follow the primer exactly.
>
> Your slice is "`<id>`". Its files (repo root `<repo>`) are listed in `$R/slices/<id>.txt`. Read every line of every
> one of them.
>
> Write your JSON to `$R/findings/<id>.json`. Read only: never edit repository files, never build or run dotnet.

## Phase 2: the angle reviewers (one per angle in `angles.md`)

Alongside phase 1, one agent per angle:

> You are a cross-cutting reviewer in a full-codebase review of `<repo>`. First read `$S/primer.md`, then
> `$S/angles.md`, then `$S/decided.md`, then `<repo>/CLAUDE.md`.
>
> Your angle is number `<n>`, "`<id>`". Cover it completely as angles.md describes.
>
> Write your JSON to `$R/findings/angle-<id>.json`. Read only: never edit repository files, never build or run dotnet.

## The coverage check

As agents finish, and once all have:

```
python3 $S/scripts/coverage.py $R
```

It reports the slices done, findings by severity, invalid JSON, and every file a slice did not read in full or did
not account for. Give each such file to a fresh agent (a slice of its own) until none is left. A reviewer's message
that it finished is not coverage; the file list is.

## Phase 3: verification

```
python3 $S/scripts/verify_batches.py $R            # high and medium; --all to verify the low ones too
```

It writes `$R/verify/<area>-input.json`, batches of at most 30 findings grouped by area, each with an id
(`<slice>#<index>`). Spawn one verifier per batch:

> You are a verifier in a full-codebase review of `<repo>`. Read `$S/verify.md` and follow it exactly.
>
> Input: `$R/verify/<area>-input.json`
> Output: `$R/verify/<area>-output.json`
> Read only: never edit repository files, never build or run dotnet.

## Phase 4: the report

```
python3 $S/scripts/merge.py $R
```

It merges the verdicts into the findings, drops the rejected and the duplicates, merges findings on the same file,
category and nearby lines, and writes `$R/merged.json`, `$R/deduped.json` and `$R/report.md`: the totals, the high
findings in full, and every area's findings as a table. Then write its summary at the top of `report.md` yourself:
what is in good shape, the high findings and what to fix first, the themes across the areas. Low findings are listed
unverified unless phase 3 ran with `--all`.

Give the user the report (its path, and the summary in the reply) and wait. Do not commit the review or its report.

## After the report

When the user asks for a plan, group every finding (the low ones too, where asked) into packages by what they change
together (high findings first, then each concern: loudness, genre data, stage interactions, musicality, correctness,
streams, server, page, performance, duplication, the tests, dead code), and write the plan in the review's folder,
each package its goal, approach, how it is verified and its critical review (overengineering and musicality named).
Each package then goes through the implementation loop (`implementation-loop`), its low findings checked against the
code before they are fixed.

As findings are rejected or kept on purpose while fixing, add each to `decided.md` with its reason, so the next run
does not raise it again: one line each, the finding's file and line, what was decided and why.

## Comparing with the last run

The report of an earlier run, where its folder still exists, lets the summary say what is new: compare the deduped
findings by file and summary, and name the ones that come back after being fixed (a regression) apart from the new.
