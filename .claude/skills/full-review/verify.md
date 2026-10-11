# RMG code review: verifiers

You verify findings that other reviewers made. Start fresh and sceptical, and assume a finding may be wrong.

Read `primer.md` and `decided.md` (same folder) and `/home/glyge/Projects/sw/rmg/CLAUDE.md` first. A finding `decided.md` already decides is `rejected`, citing it, unless the code changed since in a way that undoes its reason. The project rules apply:

- read only;
- never edit repository files;
- never build, run tests or run `dotnet`.

For each finding in your input file:

1. Open the cited file and line, and the surrounding code and callers needed to judge it. Check whether the evidence is quoted correctly and whether the failure scenario can actually happen. Trace the callers and the data: is the "empty list" ever empty, is the "wrong order" ever reached?
   - A `run` finding is a test failing on other songs: trace its cause by reading, a generator bug, a test assuming what
     those songs break, or a calibration over too few songs, and say which in the reason; you cannot run it.
   - An `elsewhere` finding is one a reviewer saw outside its own slice and noted in a line, with no evidence or
     scenario: find the code, judge it as any other, and give it its real `category` and `severity` in your output.
     Several reviewers may have noted the same issue: one `confirmed`, the others `duplicate` of it.
   - A `genre-fit` mistake quotes a measured number from a report in the review folder's `run/` (beside `verify/`): check the number in that report and that
     the description or the style settles it; a mistake without a number, or whose number is not there, is `rejected`.
   - Findings of taste are not sent to you; they go to listening.
2. Give a verdict:
   - `confirmed`: you traced it and it is real, with the scenario reachable.
   - `plausible`: likely real, but you could not prove reachability without running code.
   - `rejected`: wrong, unreachable, intended by design (check the doc comments and CLAUDE.md principles), or a style preference.
   - `duplicate`: the same issue as another finding in your batch. Name it.
3. Adjust the severity if the reviewer over- or under-rated it, and say why.
4. Keep the reason to one to three sentences, with the evidence you found: callers, values, lines.

Write a JSON array to the output path given in your task, one object per input finding:

```json
{ "id": "<the input id>", "verdict": "confirmed|plausible|rejected|duplicate", "severity": "high|medium|low", "category": "<only for an elsewhere finding: its real category>", "reason": "...", "fix": "<the fix, refined if needed>" }
```

Validate that the JSON parses. Your final message is one line: the path written and the counts of each verdict.
