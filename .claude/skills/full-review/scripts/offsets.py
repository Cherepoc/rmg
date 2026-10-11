"""The corpora the review runs the suite on: the committed corpus (offset 0), two drawn afresh for this review, and a
holdout drawn too but run only once the fixes are done, on songs neither the review nor its fixes have seen. An offset
an earlier review ran (its review.json) is never drawn again, so that fixes are not tuned to the same songs review after
review. The draw follows the review's date, so that running it again gives the same offsets. Writes them into
<review>/review.json and prints them.

    python3 offsets.py <review folder>
"""
import os, random, sys
from load import review, write_review

R = os.path.abspath(sys.argv[1])
reviews = os.path.dirname(R)
# 1000000 and 2000000 were the fixed offsets before the reviews drew theirs, 3000000 the fourth corpus of 11 October
used = {0, 1000000, 2000000, 3000000}
for name in os.listdir(reviews):
    if os.path.join(reviews, name) != R:
        d = review(os.path.join(reviews, name))
        used |= set(d.get('offsets', [])) | ({d['holdout']} if 'holdout' in d else set())

rng = random.Random(review(R).get('date') or os.path.basename(R))
drawn = []
while len(drawn) < 3:
    o = rng.randrange(4, 1000) * 1000000
    if o not in used and o not in drawn:
        drawn.append(o)
write_review(R, offsets=[0] + drawn[:2], holdout=drawn[2])
print(f"offsets {[0] + drawn[:2]}, holdout {drawn[2]} (run after the fixes)")
