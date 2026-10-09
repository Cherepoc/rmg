"""The review's coverage: every slice's findings file against the files it was given, and the findings by severity.

    python3 coverage.py <review folder>

Prints the slices and angles done and missing, the findings by severity, every file not accounted for or not read in
full, and invalid JSON. Exits 1 while any slice is missing or any file is not read in full.
"""
import collections, json, os, sys

R = sys.argv[1]
manifest = json.load(open(os.path.join(R, 'manifest.json')))
findings = os.path.join(R, 'findings')
severities = collections.Counter()
done, missing, gaps, bad = [], [], [], []

for s in manifest:
    path = os.path.join(findings, s['id'] + '.json')
    if not os.path.exists(path):
        missing.append(s['id'])
        continue
    try:
        d = json.load(open(path))
    except Exception as e:
        bad.append((s['id'], str(e)))
        continue
    done.append(s['id'])
    coverage = {c.get('file'): c for c in d.get('coverage', [])}
    for f in s['files']:
        c = coverage.get(f['path'])
        if not c:
            gaps.append((s['id'], f['path'], 'not accounted for'))
        elif c.get('read') != 'full':
            gaps.append((s['id'], f['path'], c.get('note') or 'partial'))
    severities.update(x.get('severity', '?') for x in d.get('findings', []))

angles = sorted(p[:-5] for p in os.listdir(findings) if p.startswith('angle-') and p.endswith('.json'))
for a in angles:
    try:
        d = json.load(open(os.path.join(findings, a + '.json')))
        severities.update(x.get('severity', '?') for x in d.get('findings', []))
    except Exception as e:
        bad.append((a, str(e)))

print(f"slices: {len(done)} of {len(manifest)} done; angles: {len(angles)} done ({', '.join(a[6:] for a in angles)})")
print(f"findings by severity: {dict(severities)}")
if missing:
    print(f"slices missing: {' '.join(missing)}")
for i in bad:
    print(f"invalid JSON: {i[0]}: {i[1]}")
for s, f, why in gaps:
    print(f"not read in full: {s}: {f} ({why})")
sys.exit(1 if missing or gaps or bad else 0)
