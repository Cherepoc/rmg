"""A review against an earlier one: the files added since, and the findings new, recurring and gone, matched by file,
category and either nearby lines or a summary of the same words. The matching is approximate; read a recurring finding
before calling it a regression.

    python3 compare.py <earlier review> <this review>        # findings, once this review has its deduped.json
    python3 compare.py <earlier review> <this review> --files  # the files added and removed since, before the review starts
"""
import json, os, re, sys

old, new = sys.argv[1], sys.argv[2]


def paths(review):
    return {f['path']: f['lines'] for s in json.load(open(os.path.join(review, 'manifest.json'))) for f in s['files']}


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


def words(text):
    return {w for w in re.findall(r'[a-z]{4,}', (text or '').lower())}


def same(f, g):
    if f['file'] != g['file'] or f.get('category') != g.get('category'):
        return False
    near = abs((f.get('line') or 0) - (g.get('line') or 0)) <= 10
    a, b = words(f.get('summary')), words(g.get('summary'))
    overlap = len(a & b) / max(1, min(len(a), len(b)))
    return near and overlap >= 0.25 or overlap >= 0.5


earlier = json.load(open(os.path.join(old, 'deduped.json')))
now = json.load(open(os.path.join(new, 'deduped.json')))
recurring = [(f, g) for f in now for g in earlier if same(f, g)]
seen_new = {id(f) for f, _ in recurring}
seen_old = {id(g) for _, g in recurring}
fresh = [f for f in now if id(f) not in seen_new]
gone = [g for g in earlier if id(g) not in seen_old]
print(f"{len(now)} findings now, {len(earlier)} then: {len(fresh)} new, {len({id(f) for f, _ in recurring})} recurring, {len(gone)} gone")
print("\nRecurring (fixed and back, or never fixed; read each):")
for f, g in recurring:
    print(f"  {f['severity'][0]} {f['file']}:{f.get('line')} [{f['category']}] {f.get('summary', '')[:110]}  (then {g['id']})")
print("\nNew, high and medium:")
for f in fresh:
    if f['severity'] in ('high', 'medium'):
        print(f"  {f['severity'][0]} {f['file']}:{f.get('line')} [{f['category']}] {f.get('summary', '')[:120]}")
