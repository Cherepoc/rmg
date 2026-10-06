# Known issues

- **A flaky page test:** `GenreEditorTest.ALength_IsShownAndGivenInMinutesAndSeconds_ATempoInBeatsAMinute` fails now and
  then in a full run of the page tests (3 of 11 runs, and 2 of about 10 on 7 October 2026), never alone (4 of 4 passed)
  nor with detailed output, its message lost: a race in the tune's values shown, to find should it keep failing.
