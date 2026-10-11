"""The review's coverage: every slice's findings file against the files it was given, and the findings by severity.

    python3 coverage.py <review folder>

Prints the slices and angles done and missing, the findings by severity, every file not accounted for or not read in
full, every device a device family's reviewer did not account for, every preset a genre cluster's reviewer did not, a
musical reviewer proposing more than three listening questions, three cuts or two idiom gaps, and invalid JSON. Exits 1 while any of these is left.
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

# a device family's reviewer accounts for every device of its family, by name, in its coverage
devices_path = os.path.join(R, 'devices.json')
if os.path.exists(devices_path):
    families = json.load(open(devices_path))['families']
    for family, d in families.items():
        angle = f'angle-device-{family}'
        if angle not in angles:
            missing.append(angle)
            continue
        try:
            covered = json.dumps(json.load(open(os.path.join(findings, angle + '.json'))).get('coverage', [])).lower()
        except Exception:
            continue
        for device in d['choices'] + d['stages']:
            name = device.split(' (')[0]
            if name.lower() not in covered:
                gaps.append((angle, name, 'device not accounted for'))

# a genre-fit reviewer for every cluster of presets, accounting for every preset of it in its coverage's `preset`
genres_path = os.path.join(R, 'genres.json')
if os.path.exists(genres_path):
    for cluster, presets in json.load(open(genres_path)).items():
        angle = f'angle-genre-{cluster}'
        if angle not in angles:
            missing.append(angle)
            continue
        try:
            covered = {c.get('preset') for c in json.load(open(os.path.join(findings, angle + '.json'))).get('coverage', [])}
        except Exception:
            continue
        for preset in presets:
            if preset not in covered:
                gaps.append((angle, preset, 'preset not accounted for'))

# a musical reviewer's few, ranked, so that the lists are ones a listener and a plan get through: three questions of
# taste, three cuts to hear with and without, two idiom gaps
CAPS = {'taste': 3, 'cut': 3, 'idiom-gap': 2}
for a in angles:
    if a.startswith(('angle-genre-', 'angle-device-')):
        try:
            found = json.load(open(os.path.join(findings, a + '.json'))).get('findings', [])
        except Exception:
            continue
        counts = {'taste': sum(1 for f in found if f.get('kind') == 'taste' and f.get('category') != 'cut'),
                  'cut': sum(1 for f in found if f.get('category') == 'cut'),
                  'idiom-gap': sum(1 for f in found if f.get('category') == 'idiom-gap')}
        for what, n in counts.items():
            if n > CAPS[what]:
                gaps.append((a, f'{n} {what} findings', f'more than {CAPS[what]}: keep those ranked first'))

print(f"slices: {len(done)} of {len(manifest)} done; angles: {len(angles)} done ({', '.join(a[6:] for a in angles)})")
print(f"findings by severity: {dict(severities)}")
if missing:
    print(f"slices missing: {' '.join(missing)}")
for i in bad:
    print(f"invalid JSON: {i[0]}: {i[1]}")
for s, f, why in gaps:
    print(f"gap: {s}: {f} ({why})")
sys.exit(1 if missing or gaps or bad else 0)
