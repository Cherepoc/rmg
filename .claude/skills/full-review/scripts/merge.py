"""The review's findings merged with their verdicts, deduplicated, and written as a report.

    python3 merge.py <review folder>

Writes <review>/merged.json (every finding with its verdict), <review>/deduped.json (those kept, each with its number
`n`, the id the plan, the statuses and the record use) and <review>/report.md (totals, the listening questions, the
high findings in full, every area's findings as a table; its summary at the top left for the reviewer to write).

Two findings are merged only where they are the same issue: on the same file and category, and either within six lines
with a summary of some of the same words, or anywhere in the file with a summary of mostly the same words. The most
severe of a group leads it, and every summary merged into it is printed with it, so that no finding is lost under
another. A finding's severity is the verifier's, the reviewer's kept beside it.

Run it once: the numbers `n` are what the plan assigns, and a second run after planning renumbers them (status.py
refuses a plan whose numbers no longer match).
"""
import collections, json, os, sys
from areas import area, TITLES
from load import findings, overlap, review

# proposals reviewed as plans, not findings to fix: each in a section of its own, outside the areas' tables
PLANS = {
    'architecture': ('Architecture proposals', 'Another design for a thing the code does, from the architecture reviewer'),
    'idiom-gap': ('Idiom gaps', 'A device the engine lacks that genres\' players use, from the genre clusters'),
}

R = sys.argv[1]
verdicts = {}
for path in sorted(os.listdir(os.path.join(R, 'verify'))) if os.path.isdir(os.path.join(R, 'verify')) else []:
    if path.endswith('-output.json'):
        for v in json.load(open(os.path.join(R, 'verify', path))):
            verdicts[v['id']] = v

merged = []
for f in findings(R):
    f['reviewer_severity'] = f.get('severity')
    v = verdicts.get(f['id'])
    if v:
        f['verdict'] = v['verdict']
        f['severity'] = v.get('severity') or f.get('severity')
        f['category'] = v.get('category') or f.get('category')
        f['reason'] = v.get('reason', '')
        f['fix'] = v.get('fix') or f.get('suggestion')
        if v['verdict'] in ('rejected', 'duplicate'):
            f['dropped'] = f"{v['verdict']}: {v.get('reason', '')}"
    else:
        f['verdict'] = 'taste' if f.get('kind') == 'taste' else 'proposal' if f.get('category') in PLANS else 'unverified'
        f['fix'] = f.get('suggestion')
    merged.append(f)
json.dump(merged, open(os.path.join(R, 'merged.json'), 'w'), indent=1)

rank = {'high': 0, 'medium': 1, 'low': 2}


def same(f, g):
    if f.get('file') != g.get('file') or f.get('category') != g.get('category'):
        return False
    o = overlap(f.get('summary'), g.get('summary'))
    near = abs((f.get('line') or 0) - (g.get('line') or 0)) <= 6
    return near and o >= 0.35 or o >= 0.6


# the most severe first, so that each group is led by its most severe finding
kept = sorted((f for f in merged if 'dropped' not in f),
              key=lambda f: (rank.get(f.get('severity'), 2), f.get('file', ''), f.get('line') or 0))
deduped = []
for f in kept:
    g = next((g for g in deduped if same(g, f)), None)
    if g:
        g['also'].append({'id': f['id'], 'severity': f.get('severity'), 'line': f.get('line'), 'summary': f.get('summary')})
    else:
        deduped.append(dict(f, also=[]))
deduped.sort(key=lambda f: (f.get('file', ''), f.get('line') or 0))
for n, f in enumerate(deduped, 1):
    f['n'] = n
json.dump(deduped, open(os.path.join(R, 'deduped.json'), 'w'), indent=1)

dropped = collections.Counter(f['dropped'].split(':')[0] for f in merged if 'dropped' in f)
checked = collections.Counter(f['verdict'] for f in merged if f['verdict'] not in ('unverified', 'taste', 'proposal'))
severity = collections.Counter(f['severity'] for f in deduped)
manifest = json.load(open(os.path.join(R, 'manifest.json')))
files = sum(len(s['files']) for s in manifest)
lines = sum(s['lines'] for s in manifest)
angles = sorted(p[:-5][6:] for p in os.listdir(os.path.join(R, 'findings')) if p.startswith('angle-'))
meta = review(R)
elsewhere = sum(1 for f in deduped if f.get('elsewhere'))


def cell(text, n=220):
    return ' '.join(str(text or '').split()).replace('|', '\\|')[:n]


def sev(f):
    s, was = f['severity'], f.get('reviewer_severity')
    return s if not was or was == s else f'{s} (was {was})'


def also(f, n=160):
    return ''.join(f" **Also:** {cell(a['summary'], n)} ({a['id']})" for a in f['also'])


out = []
w = out.append
w('# Full code review\n')
scope = (f"delta since {meta.get('since', '?')[:7]}, the files changed since" if meta.get('mode') == 'delta' else 'every file')
w(f"Commit {meta.get('commit', '?')[:7]}; {scope}: {files} files ({lines} lines) in {len(manifest)} slices, each read in "
  f"full by a reviewer; {len(angles)} cross-cutting reviewers ({', '.join(angles)}). {sum(checked.values())} findings "
  f"verified: {', '.join(f'{n} {k}' for k, n in checked.most_common())}.\n")
w(f"**Totals after merging the duplicates:** {severity.get('high', 0)} high, {severity.get('medium', 0)} medium, "
  f"{severity.get('low', 0)} low ({len(deduped)} in all, {elsewhere} of them raised under *elsewhere*; "
  f"{sum(dropped.values())} dropped as rejected or duplicate).\n")
w('## The summary\n\n_To write: what is in good shape, the high findings and what to fix first, the themes across the areas._\n')
# a matter of taste is heard, not fixed: each a listening question to ask before any change, the reviewer's rank first
taste = sorted((f for f in deduped if f.get('kind') == 'taste'),
               key=lambda f: ((f.get('listen') or {}).get('rank') or 9, f['source']))
if taste:
    w(f'## Proposed listening questions ({len(taste)})\n')
    w('Musical findings of taste, which reading cannot settle and no verifier checked: each to be asked on the test '
      'instance (`?listen=`) before its change, the reviewer\'s first-ranked first.\n')
    w('| # | Rank | Where | Question | Without | Finding |')
    w('|---|---|---|---|---|---|')
    for f in taste:
        listen = f.get('listen') or {}
        w(f"| {f['n']} | {listen.get('rank', '')} | `{os.path.basename(f['file'])}:{f.get('line')}` | {cell(listen.get('question'), 200)} | {cell(listen.get('without'), 120)} | {cell(f.get('summary'), 160)} |")
for category, (title, what) in PLANS.items():
    plans = [f for f in deduped if f.get('category') == category]
    if not plans:
        continue
    w(f'## {title} ({len(plans)})\n')
    w(f'{what}: each reviewed as a plan, an accepted one to `ROADMAP.md`, a rejected one to `decided.md`. Not verified.\n')
    for f in plans:
        w(f"### {f['n']}. {f.get('summary', '')}\n")
        w(f"`{f['file']}:{f.get('line')}`, {f['severity']}.\n")
        for key in ('cost', 'alternative', 'deletes', 'adds', 'migration', 'decided', 'scenario', 'suggestion'):
            if f.get(key):
                w(f"- **{key.capitalize()}:** {cell(f.get(key), 1500)}")
        w('')
w('## The high findings\n')
for f in (f for f in deduped if f['severity'] == 'high' and f.get('category') not in PLANS):
    w(f"### {f['n']}. {f['file']}:{f.get('line')} ({f['category']}, {f['verdict']})\n")
    w(f"{f.get('summary', '')}\n\n- **Scenario:** {cell(f.get('scenario'), 1000)}\n- **Fix:** {cell(f.get('fix'), 1000)}\n"
      f"- **Evidence:** `{cell(f.get('evidence'), 400)}`\n" + (f"- **Verifier:** {cell(f.get('reason'), 600)}\n" if f.get('reason') else '')
      + ''.join(f"- **Merged with:** {a['severity']}, line {a['line']}: {cell(a['summary'], 400)} ({a['id']})\n" for a in f['also']))
by_area = collections.defaultdict(list)
for f in deduped:
    if f.get('category') not in PLANS:
        by_area[area(f['file'], f.get('category'))].append(f)
for key in TITLES:
    items = sorted(by_area.get(key, []), key=lambda f: (rank.get(f['severity'], 2), f['file'], f.get('line') or 0))
    if not items:
        continue
    count = collections.Counter(f['severity'] for f in items)
    w(f"\n## {TITLES[key]} ({', '.join(f'{count[s]} {s}' for s in ('high', 'medium', 'low') if count[s])})\n")
    w('| # | Severity | Where | Category | Verdict | Finding | Fix |')
    w('|---|---|---|---|---|---|---|')
    for f in items:
        w(f"| {f['n']} | {sev(f)} | `{os.path.basename(f['file'])}:{f.get('line')}` | {f['category']} | {f['verdict']} | {cell(f.get('summary'))}{also(f)} | {cell(f.get('fix'))} |")
open(os.path.join(R, 'report.md'), 'w').write('\n'.join(out) + '\n')
print(f"{len(merged)} findings, {sum(dropped.values())} dropped, {len(deduped)} after merging: {dict(severity)}")
print(f"wrote {R}/report.md")
