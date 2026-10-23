#!/bin/bash
# A held-out failure traced to the commit that brought it: the test bisected at its offset from the review's commit
# (review.json) to the repository's HEAD, in a worktree of its own, one build and run a step under the memory cap.
#
#   bisect.sh <repo> <review folder> <offset> <test method>
#
# Its verdict: the first commit failing, and whether it changed the tests alone, in which case the failure is older
# behaviour a test now sees (a finding of the record), or code too, whose diff says whether the fix broke the song or
# made a trace or a rule honest about what was there before (a finding of the record too). A test failing at the
# review's commit already is older than the fixes, and is not bisected.
set -u
repo=$(realpath "$1"); R=$(realpath "$2"); offset=$3; test=$4
base=$(python3 -c "import json; print(json.load(open('$R/review.json'))['commit'])")
wt="$R/bisect-wt"
log="$R/holdout/bisect-$test.log"
mkdir -p "$R/holdout"
[ -d "$wt" ] || git -C "$repo" worktree add -q --detach "$wt" HEAD
step="$R/holdout/bisect-step.sh"
cat > "$step" <<STEP
#!/bin/bash
export PATH=\$HOME/.dotnet:\$PATH
cap() { systemd-run --user --scope --quiet -p MemoryMax=8G -p MemorySwapMax=0 -p AllowedCPUs=0-\$(( \$(nproc) / 2 - 1 )) -E RMG_CORPUS_OFFSET=$offset -- "\$@"; }
cap dotnet build Rmg.Tests -c Release --nologo -v quiet > /dev/null 2>&1 || exit 125
out=\$(cap dotnet run --project Rmg.Tests -c Release --no-build -- --treenode-filter "/*/*/*/$test" 2>&1)
echo "\$(git log --oneline -1 | cut -c1-70): \$(echo "\$out" | grep -E 'failed:|succeeded:' | tr -s ' \n' ' ')" >> "$log"
echo "\$out" | grep -q 'failed: 0' && echo "\$out" | grep -q 'succeeded: [1-9]' && exit 0
echo "\$out" | grep -q 'failed: [1-9]' && exit 1
exit 125
STEP
chmod +x "$step"
cd "$wt"
git checkout -q --detach "$(git -C "$repo" rev-parse HEAD)"
git bisect reset > /dev/null 2>&1
# the review's commit must pass, or the failure is older than the fixes
git checkout -q --detach "$base"
if ! "$step"; then
  echo "$test fails at the review's commit $base at offset $offset: older than the fixes, a finding of the record"
  exit 0
fi
git bisect start "$(git -C "$repo" rev-parse HEAD)" "$base" > /dev/null || { echo "bisect could not start"; exit 1; }
git bisect run "$step" > /dev/null 2>&1
bad=$(git rev-parse --verify -q refs/bisect/bad)
[ -n "$bad" ] || { git bisect reset > /dev/null 2>&1; echo "bisect found no failing commit; steps: $log"; exit 1; }
git bisect reset > /dev/null 2>&1
files=$(git -C "$repo" show --format= --name-only "$bad")
echo "first failing: $(git -C "$repo" log --oneline -1 "$bad" | cut -c1-120)"
# the generator's code, its .cs files outside the tests; the roadmap, the listening file and the like are not
if ! echo "$files" | grep '\.cs$' | grep -qv '^Rmg\.Tests'; then
  echo "it changed the tests alone: older behaviour a test now sees, a finding of the record"
else
  echo "it changed code too: read its diff, a fix that broke the song (fix it now) or a trace or rule made honest (a finding of the record)"
fi
echo "steps: $log"
