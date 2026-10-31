#!/bin/bash
# The review's runs, one at a time under the memory cap: the build, the suite at each of the review's offsets
# (review.json, from offsets.py), the five preset reports, and every run's failures as findings; first the reports'
# smoke test, as the review measures with them, and each failure run again alone, one passing alone flaky.
#
#   runs.sh <repo> <review folder>             # the review's runs, before the reviewers
#   runs.sh <repo> <review folder> --holdout   # the suite at the holdout offset alone, once the fixes are done
set -u
repo=$(realpath "$1"); R=$(realpath "$2"); mode=${3:-}
S=$(dirname "$(realpath "$0")")
# test.sh's settings, as the machine runs other work beside the review: everything on all the cores but two and in
# 3 GB, the heap 85% of it, the corpus keeping no song in it (TestCorpus.SongsKept), the suite four tests at a time
export PATH=$HOME/.dotnet:$PATH DOTNET_GCHeapHardLimitPercent=0x55
cap() { systemd-run --user --scope --quiet -p MemoryMax=3G -p MemorySwapMax=0 -p AllowedCPUs=0-$(( $(nproc) - 3 )) -- "$@"; }
build() { cap "$@"; }
suite() { cap "$@"; }
mkdir -p "$R/run"
cd "$repo"
build dotnet build Rmg.slnx -c Release --nologo -v quiet > "$R/run/build.txt" 2>&1 || { echo "build failed: $R/run/build.txt"; exit 1; }
# the review's instruments first: a report that no longer runs is fixed before any reviewer reads its numbers
if [ "$mode" != --holdout ]; then
  { cap dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/GenreDevicesReportTest/TheReports_MeasureASongOfEveryPreset*" \
      && cap dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/SoundingReportTest/TheReport_MeasuresASongOfEveryPreset"; } > "$R/run/smoke.txt" 2>&1 \
    || { echo "the reports' smoke test fails: fix the review's instruments first ($R/run/smoke.txt)"; exit 1; }
fi
if [ "$mode" = --holdout ]; then
  offsets=$(python3 -c "import json; print(json.load(open('$R/review.json'))['holdout'])")
else
  offsets=$(python3 -c "import json; print(' '.join(map(str, json.load(open('$R/review.json'))['offsets'])))")
fi
for o in $offsets; do
  suite env RMG_CORPUS_OFFSET=$o dotnet run --project Rmg.Tests -c Release --no-build -- --maximum-parallel-tests 4 > "$R/run/offset-$o.txt" 2>&1
  # each failure run again alone at the same offset: one passing alone fails only under the suite's load, a flake
  : > "$R/run/flaky-$o.txt"
  for t in $(grep -oP '^failed \K\w+' "$R/run/offset-$o.txt" | sort -u); do
    alone=$(cap env RMG_CORPUS_OFFSET=$o dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/*/$t" 2>&1)
    echo "$alone" | grep -q 'failed: 0' && echo "$alone" | grep -q 'succeeded: [1-9]' && echo "$t" >> "$R/run/flaky-$o.txt"
  done
  # a holdout's failures go apart, to the record's triage, never into the review's findings
  python3 "$S/run_findings.py" "$repo" "$R${mode:+/holdout}" $o "$R/run/offset-$o.txt" "$R/run/flaky-$o.txt"
done
[ "$mode" = --holdout ] && exit 0
cap dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/*/EveryPreset_EveryChance" --output detailed > "$R/run/every-chance.txt" 2>&1
cap dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/GenreDevicesReportTest/Report" --output detailed > "$R/run/genre-devices.txt" 2>&1
cap dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/*/EveryPreset_ChoicesByEnd" --output detailed > "$R/run/every-preset.txt" 2>&1
# how the parts sound together: a note held into its part's next, a semitone between parts, a strong note off its chord
cap dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/SoundingReportTest/Report" --output detailed > "$R/run/sounding.txt" 2>&1
# every preset's mood at its ends, the same seeds at darkness and intensity -1, 0 and 1 given
cap dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/MoodReportTest/Ends" --output detailed > "$R/run/mood.txt" 2>&1
echo "runs done: $R/run"
