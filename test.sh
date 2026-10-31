#!/bin/bash
# The tests in a memory cap of their own, on all the cores but two, as the machine runs other work beside them: the
# suite in 3 GB, four tests at a time, each making its songs on every core, and the corpus keeping songs only in what
# the cap leaves beyond the tests' work (TestCorpus.SongsKept), none in 3 GB. Run without a cap, the tests count the
# whole machine's memory as theirs. The heap may take 85% of the cap, not the runtime's 75%: the suite passed in 3 GB
# that way (12m 39s on 16 cores, 31 October 2026) and ran out of memory in 2.
#
#   ./test.sh                                                     # the suite
#   ./test.sh --treenode-filter "/*/*/SoloTest/*" --output detailed   # one class, its reports included
#   RMG_TEST_MEMORY=8G ./test.sh                                  # a larger cap, the corpus keeping songs in it
#   RMG_CORPUS_OFFSET=1000000 ./test.sh                           # other songs
#
# Built first: dotnet build Rmg.slnx -c Release (or ./test.sh --build to build the tests, capped too).
set -eu
cd "$(dirname "$(realpath "$0")")"
export PATH=$HOME/.dotnet:$PATH DOTNET_GCHeapHardLimitPercent=0x55
memory=${RMG_TEST_MEMORY:-3G}
cpus=0-$(( $(nproc) - 3 ))
if [ "${1:-}" = --build ]; then
  shift
  systemd-run --user --scope --quiet -p MemoryMax=3G -p MemorySwapMax=0 -p AllowedCPUs=$cpus -- \
    dotnet build Rmg.Tests -c Release --nologo -v quiet
fi
exec systemd-run --user --scope --quiet -p MemoryMax=$memory -p MemorySwapMax=0 -p AllowedCPUs=$cpus -- \
  dotnet run --project Rmg.Tests -c Release --no-build -- --maximum-parallel-tests 4 "$@"
