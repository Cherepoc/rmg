#!/bin/bash
# The review's runs, one at a time under the memory cap: the build, the suite at each of the review's offsets
# (review.json, from offsets.py), the three preset reports, and every run's failures as findings.
#
#   runs.sh <repo> <review folder>             # the review's runs, before the reviewers
#   runs.sh <repo> <review folder> --holdout   # the suite at the holdout offset alone, once the fixes are done
set -u
repo=$(realpath "$1"); R=$(realpath "$2"); mode=${3:-}
S=$(dirname "$(realpath "$0")")
export PATH=$HOME/.dotnet:$PATH
cap() { systemd-run --user --scope --quiet -p MemoryMax=8G -p MemorySwapMax=0 -p AllowedCPUs=0-$(( $(nproc) - 3 )) -- "$@"; }
mkdir -p "$R/run"
cd "$repo"
cap dotnet build Rmg.slnx -c Release --nologo -v quiet > "$R/run/build.txt" 2>&1 || { echo "build failed: $R/run/build.txt"; exit 1; }
if [ "$mode" = --holdout ]; then
  offsets=$(python3 -c "import json; print(json.load(open('$R/review.json'))['holdout'])")
else
  offsets=$(python3 -c "import json; print(' '.join(map(str, json.load(open('$R/review.json'))['offsets'])))")
fi
for o in $offsets; do
  cap env RMG_CORPUS_OFFSET=$o dotnet run --project Rmg.Tests -c Release --no-build > "$R/run/offset-$o.txt" 2>&1
  # a holdout's failures go apart, to the record's triage, never into the review's findings
  python3 "$S/run_findings.py" "$repo" "$R${mode:+/holdout}" $o "$R/run/offset-$o.txt"
done
[ "$mode" = --holdout ] && exit 0
cap dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/*/EveryPreset_EveryChance" --output detailed > "$R/run/every-chance.txt" 2>&1
cap dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/GenreDevicesReportTest/*" --output detailed > "$R/run/genre-devices.txt" 2>&1
cap dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/*/EveryPreset_ChoicesByEnd" --output detailed > "$R/run/every-preset.txt" 2>&1
echo "runs done: $R/run"
