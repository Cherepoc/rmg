"""The review's findings merged with their verdicts, deduplicated, and written as a report.

    python3 merge.py <review folder>

Writes <review>/merged.json (every finding with its verdict), <review>/deduped.json (those kept, findings on the same
file, category and lines within six merged) and <review>/report.md (totals, the high findings in full, every area's
findings as a table; its summary at the top left for the reviewer to write).
"""
import collections, glob, json, os, sys
from areas import area, TITLES

R = sys.argv[1]
findings = {}
for path in glob.glob(os.path.join(R, 'findings', '*.json')):
    source = os.path.basename(path)[:-5]
    for k, f in enumerate(json.load(open(path)).get('findings', [])):
        findings[f'{source}#{k}'] = dict(f, id=f'{source}#{k}', source=source)

verdicts = {}
for path in glob.glob(os.path.join(R, 'verify', '*-output.json')):
    for v in json.load(open(path)):
        verdicts[v['id']] = v

merged = []
for i, f in findings.items():
    v = verdicts.get(i)
    if v:
        f['verdict'] = v['verdict']
        f['severity'] = v.get('severity') or f.get('severity')
        f['reason'] = v.get('reason', '')
        f['fix'] = v.get('fix') or f.get('suggestion')
        if v['verdict'] in ('rejected', 'duplicate'):
            f['dropped'] = f"{v['verdict']}: {v.get('reason', '')}"
    else:
        f['verdict'] = 'unverified'
        f['fix'] = f.get('suggestion')
    merged.append(f)
json.dump(merged, open(os.path.join(R, 'merged.json'), 'w'), indent=1)

rank = {'high': 0, 'medium': 1, 'low': 2}
kept = sorted((f for f in merged if 'dropped' not in f), key=lambda f: (f.get('file', ''), f.get('line') or 0))
deduped = []
for f in kept:
    for g in deduped:
        if g['file'] == f.get('file') and g['category'] == f.get('category') and abs((g.get('line') or 0) - (f.get('line') or 0)) <= 6:
            g['also'].append(f['id'])
            if rank.get(f.get('severity'), 2) < rank.get(g['severity'], 2):
                g['severity'] = f['severity']
            break
    else:
        deduped.append(dict(f, also=[]))
json.dump(deduped, open(os.path.join(R, 'deduped.json'), 'w'), indent=1)

dropped = collections.Counter(f['dropped'].split(':')[0] for f in merged if 'dropped' in f)
checked = collections.Counter(f['verdict'] for f in merged if f['verdict'] != 'unverified')
severity = collections.Counter(f['severity'] for f in deduped)
manifest = json.load(open(os.path.join(R, 'manifest.json')))
files = sum(len(s['files']) for s in manifest)
lines = sum(s['lines'] for s in manifest)
angles = sorted(p[:-5][6:] for p in os.listdir(os.path.join(R, 'findings')) if p.startswith('angle-'))


def cell(text, n=220):
    return ' '.join(str(text or '').split()).replace('|', '\\|')[:n]


out = []
w = out.append
w('# Full code review\n')
w(f"{files} files ({lines} lines) in {len(manifest)} slices, each read in full by a reviewer; {len(angles)} cross-cutting "
  f"reviewers ({', '.join(angles)}). {sum(checked.values())} findings verified: "
  f"{', '.join(f'{n} {k}' for k, n in checked.most_common())}.\n")
w(f"**Totals after merging the duplicates:** {severity.get('high', 0)} high, {severity.get('medium', 0)} medium, "
  f"{severity.get('low', 0)} low ({len(deduped)} in all; {sum(dropped.values())} dropped as rejected or duplicate).\n")
w('## The summary\n\n_To write: what is in good shape, the high findings and what to fix first, the themes across the areas._\n')
# a matter of taste is heard, not fixed: each a listening question to ask before any change
taste = [f for f in deduped if f.get('kind') == 'taste']
if taste:
    w(f'## Proposed listening questions ({len(taste)})\n')
    w('Musical findings of taste, which reading cannot settle: each to be asked on the test instance (`?listen=`) before its change.\n')
    w('| Where | Question | Without | Finding | Id |')
    w('|---|---|---|---|---|')
    for f in taste:
        listen = f.get('listen') or {}
        w(f"| `{os.path.basename(f['file'])}:{f.get('line')}` | {cell(listen.get('question'), 200)} | {cell(listen.get('without'), 120)} | {cell(f.get('summary'), 160)} | {f['id']} |")
w('## The high findings\n')
for f in (f for f in deduped if f['severity'] == 'high'):
    w(f"### {f['file']}:{f.get('line')} ({f['category']}, {f['verdict']})\n")
    w(f"{f.get('summary', '')}\n\n- **Scenario:** {cell(f.get('scenario'), 1000)}\n- **Fix:** {cell(f.get('fix'), 1000)}\n"
      f"- **Evidence:** `{cell(f.get('evidence'), 400)}`\n" + (f"- **Verifier:** {cell(f.get('reason'), 600)}\n" if f.get('reason') else ''))
by_area = collections.defaultdict(list)
for f in deduped:
    by_area[area(f['file'], f.get('category'))].append(f)
for key in TITLES:
    items = sorted(by_area.get(key, []), key=lambda f: (rank.get(f['severity'], 2), f['file'], f.get('line') or 0))
    if not items:
        continue
    count = collections.Counter(f['severity'] for f in items)
    w(f"\n## {TITLES[key]} ({', '.join(f'{count[s]} {s}' for s in ('high', 'medium', 'low') if count[s])})\n")
    w('| Severity | Where | Category | Verdict | Finding | Fix | Id |')
    w('|---|---|---|---|---|---|---|')
    for f in items:
        w(f"| {f['severity']} | `{os.path.basename(f['file'])}:{f.get('line')}` | {f['category']} | {f['verdict']} | {cell(f.get('summary'))} | {cell(f.get('fix'))} | {f['id']} |")
open(os.path.join(R, 'report.md'), 'w').write('\n'.join(out) + '\n')
print(f"{len(merged)} findings, {sum(dropped.values())} dropped, {len(deduped)} after merging: {dict(severity)}")
print(f"wrote {R}/report.md")
