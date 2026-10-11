"""A review against an earlier one: the files added since, and the findings new, recurring and gone, matched by file,
category and either nearby lines or a summary of the same words. The matching is approximate; read a recurring finding
before calling it a regression.

    python3 compare.py <earlier review> <this review>        # findings, once this review has its deduped.json
    python3 compare.py <earlier review> <this review> --files  # the files added and removed since, before the review starts

Where both reviews recorded their commits (review.json), it also counts the new findings in files unchanged between
them: issues a full re-read found in code the last review read too. Their share is what tells whether a delta review,
which reads only the changed files in full, misses much.
"""
import json, os, subprocess, sys
from load import overlap, review

old, new = sys.argv[1], sys.argv[2]


def paths(r):
    p = os.path.join(r, 'files.json')
    if os.path.exists(p):
        return {f['path']: f['lines'] for f in json.load(open(p))}
    return {f['path']: f['lines'] for s in json.load(open(os.path.join(r, 'manifest.json'))) for f in s['files']}


if '--files' in sys.argv:
    a, b = paths(old), paths(new)
    added, removed = sorted(set(b) - set(a)), sorted(set(a) - set(b))
    grown = sorted((p for p in set(a) & set(b) if abs(b[p] - a[p]) > max(40, a[p] // 5)), key=lambda p: -(b[p] - a[p]))
    print(f"{len(added)} files added, {len(removed)} removed, {len(grown)} changed by a fifth or more")
    for p in added:
        print(f"  + {p} ({b[p]} lines)")
    for p in removed:
        print(f"  - {p}")
    for p in grown:
        print(f"  ~ {p} ({a[p]} -> {b[p]} lines)")
    sys.exit(0)


def same(f, g):
    if f['file'] != g['file'] or f.get('category') != g.get('category'):
        return False
    near = abs((f.get('line') or 0) - (g.get('line') or 0)) <= 10
    o = overlap(f.get('summary'), g.get('summary'))
    return near and o >= 0.25 or o >= 0.5


earlier = json.load(open(os.path.join(old, 'deduped.json')))
now = json.load(open(os.path.join(new, 'deduped.json')))
recurring = [(f, g) for f in now for g in earlier if same(f, g)]
seen_new = {id(f) for f, _ in recurring}
seen_old = {id(g) for _, g in recurring}
fresh = [f for f in now if id(f) not in seen_new]
gone = [g for g in earlier if id(g) not in seen_old]
print(f"{len(now)} findings now, {len(earlier)} then: {len(fresh)} new, {len(seen_new)} recurring, {len(gone)} gone")

a, b = review(old), review(new)
if a.get('commit') and b.get('commit'):
    changed = set(subprocess.run(['git', '-C', b['repo'], 'diff', '--name-only', a['commit'], b['commit']],
                                 check=True, capture_output=True, text=True).stdout.split())
    unchanged = [f for f in fresh if f['file'] not in changed]
    counts = ', '.join(f"{sum(1 for f in unchanged if f['severity'] == s)} {s}" for s in ('high', 'medium', 'low'))
    print(f"\nNew findings in files unchanged since {a['commit'][:7]}: {len(unchanged)} of {len(fresh)} ({counts})")
    for f in unchanged:
        if f['severity'] in ('high', 'medium'):
            print(f"  {f['severity'][0]} {f['file']}:{f.get('line')} [{f['category']}] {f.get('summary', '')[:110]}  ({f.get('source')})")
else:
    print("\n(no commit recorded for one of the reviews: the new findings in unchanged files cannot be counted)")

print("\nRecurring (fixed and back, or never fixed; read each):")
for f, g in recurring:
    print(f"  {f['severity'][0]} {f['file']}:{f.get('line')} [{f['category']}] {f.get('summary', '')[:110]}  (then {g.get('n', g['id'])})")
print("\nNew, high and medium:")
for f in fresh:
    if f['severity'] in ('high', 'medium'):
        print(f"  {f['severity'][0]} {f['file']}:{f.get('line')} [{f['category']}] {f.get('summary', '')[:120]}")
