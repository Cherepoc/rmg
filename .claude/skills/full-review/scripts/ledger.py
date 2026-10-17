"""The device ledger: for every device of devices.json, what it costs and what it earns, in numbers, for the device
reviewers' verdict on whether it earns its place. Per device: the presets that name it, how often the songs and each
preset play it (run/genre-devices.txt, by the report's own measure-to-choice table), the code that reads it, the
findings past reviews raised in that code, and the listening questions asking it now. Writes <review>/ledger.md and
<review>/ledger.json.

    python3 ledger.py <repo> <review>

A preset that does not name a choice plays it at the songs' own amounts, so a device no preset names is not unused: its
plays are what says so. The findings are matched by file and are approximate; a stage written inside SongStages has no
file of its own, and no findings are counted for it.
"""
import glob, json, os, re, statistics, sys

root, R = sys.argv[1], sys.argv[2]
reviews = os.path.dirname(os.path.abspath(R))
families = json.load(open(os.path.join(R, 'devices.json')))['families']
core = os.path.join(root, 'Rmg.Core')


def read(path):
    return open(path, encoding='utf-8', errors='ignore').read()


def rel(path):
    return os.path.relpath(path, root)


sources = {rel(p): read(p) for p in glob.glob(os.path.join(core, '**', '*.cs'), recursive=True) if '/obj/' not in p and '/bin/' not in p}
size = {p: s.count('\n') for p, s in sources.items()}
choices_cs = read(os.path.join(core, 'Composition', 'GenreChoices.cs'))
prop = dict((name, p) for p, name in re.findall(r'public static GenreChoice (\w+) \{ get; \} = [^"]*?\("(\w+)"', choices_cs))
stages_cs = read(os.path.join(core, 'Composition', 'SongStages.cs'))
stage_fn = dict(re.findall(r'new\("([^"]+)", ([\w.]+), CanSkip', stages_cs))

# the presets that name each choice
presets = json.load(open(os.path.join(root, 'Rmg.WebApi', 'Client', 'genres.json')))
presets = presets if isinstance(presets, list) else presets.get('genres', presets)
naming = {}
for p in presets:
    for key in p.get('choices') or {}:
        naming.setdefault(key.split('.')[0], []).append(p['id'])

# how often each device plays: the report's rows, by the report's own table of the choice each measure is by
report_cs = read(os.path.join(root, 'Rmg.Tests', 'Genres', 'GenreDevicesReportTest.cs'))
measure_of = {}
for measure, choice in re.findall(r'\["([^"]+)"\] = "(\w+)"', report_cs):
    measure_of.setdefault(choice, []).append(measure)
plays = {}
report = os.path.join(R, 'run', 'genre-devices.txt')
# the report's table: a label of 36 characters, then a cell of 11 for the songs' own and each preset, in GenrePresets'
# order, a value as a share or a number with a * where the preset inherits the songs' amounts, blank or NaN where
# nothing was measured (GenreDevicesReportTest.Report)
LABEL, CELL = 36, 11


def cell(text):
    text = text.strip()
    m = re.fullmatch(r'(-?\d+(?:\.\d+)?)%?(\*?)', text)
    return (float(m.group(1)), m.group(2) == '*') if m else (None, text.endswith('*'))


if os.path.exists(report):
    lines = open(report, encoding='utf-8', errors='ignore').read().splitlines()
    # the test runner's progress lines come before the table: start at its header
    lines = lines[next(k for k, line in enumerate(lines) if line.split()[:1] == ['none']):]
    # counted on a row of numbers, as a long preset name runs into the next in the header
    columns = (len(lines[1]) - LABEL + CELL - 1) // CELL
    if columns != len(presets) + 1:
        raise SystemExit(f"{report} has {columns - 1} presets' columns, genres.json {len(presets)}: run the report again")
    measures = {m for ms in measure_of.values() for m in ms}
    for line in lines[1:]:
        measure = line[:LABEL].strip()
        if measure in measures:
            values = [cell(line[LABEL + CELL * k:LABEL + CELL * (k + 1)]) for k in range(columns)]
            plays[measure] = {'songs': values[0][0], 'presets': {p['id']: v for p, v in zip(presets, values[1:])}}
else:
    print(f"no {report}: run the reports first; plays left out")

# findings past reviews raised, by file
past = []
for path in glob.glob(os.path.join(reviews, '*', 'deduped.json')):
    if os.path.dirname(os.path.abspath(path)) != os.path.abspath(R):
        past += json.load(open(path))


def files_of(device, kind):
    if kind == 'choice':
        p = prop.get(device)
        return sorted(f for f, s in sources.items() if p and f'GenreChoices.{p}' in s and not f.endswith('GenreChoices.cs'))
    fn = stage_fn.get(device, '')
    cls = fn.split('.')[-2] if '.' in fn else None
    return [f for f in sources if cls and os.path.basename(f) == cls + '.cs']


def listening(device, kind):
    path = os.path.join(root, 'Rmg.WebApi', 'Client', 'listening.json')
    asked = []
    for q in json.load(open(path)) if os.path.exists(path) else []:
        without = q.get('without') or {}
        if kind == 'choice' and device in (without.get('genre') or {}) or kind == 'stage' and device in (without.get('skip') or []):
            asked.append(q['id'])
    return asked


rows = []
for family, d in families.items():
    for kind, items in (('choice', d['choices']), ('stage', d['stages'])):
        for item in items:
            device = item.split(' (')[0]
            files = files_of(device, kind)
            row = {'family': family, 'kind': kind, 'device': device, 'label': item[len(device):].strip(' ()'),
                   'named_by': naming.get(device, []) if kind == 'choice' else [],
                   'files': files, 'lines': sum(size[f] for f in files),
                   'past_findings': sum(1 for f in past if f.get('file') in files),
                   'listening_now': listening(device, kind), 'plays': []}
            for measure in measure_of.get(device, []) if kind == 'choice' else []:
                m = plays.get(measure)
                if m:
                    shares = [v for v, _ in m['presets'].values() if v is not None]
                    if not shares:
                        continue
                    row['plays'].append({'measure': measure, 'songs': m['songs'], 'median': statistics.median(shares),
                                         'max': max(shares), 'presets_at_0': sum(1 for v in shares if v == 0),
                                         'inherited': sum(1 for _, star in m['presets'].values() if star)})
            rows.append(row)
json.dump(rows, open(os.path.join(R, 'ledger.json'), 'w'), indent=1)

def num(v):
    return '–' if v is None else f'{v:g}'


out = ['# The device ledger\n',
       'What each device costs and earns, for the device reviewers\' verdict. Plays are shares from '
       '`run/genre-devices.txt`: the songs\' own, the median and the most of the presets, the presets at 0, the presets '
       'inheriting the songs\' amounts. A device no preset names still plays at the songs\' own amounts. Findings are '
       'those past reviews raised in the files that read the device, approximate.\n']
for family in families:
    out.append(f'\n## {family}\n')
    out.append('| Device | Kind | Named by | Plays (songs / median / most; at 0; inherited) | Code (files, lines) | Past findings | Asked now |')
    out.append('|---|---|---|---|---|---|---|')
    for r in (r for r in rows if r['family'] == family):
        p = '; '.join(f"{x['measure']}: {num(x['songs'])} / {x['median']:g} / {x['max']:g}; {x['presets_at_0']} at 0; {x['inherited']} inherited" for x in r['plays'])
        named = f"{len(r['named_by'])}" + (f" ({' '.join(r['named_by'][:6])}{' …' if len(r['named_by']) > 6 else ''})" if r['named_by'] else '')
        out.append(f"| {r['device']} ({r['label']}) | {r['kind']} | {named if r['kind'] == 'choice' else '–'} | "
                   f"{p or ('unmeasured' if r['kind'] == 'choice' else '–')} | {len(r['files'])}, {r['lines']} | "
                   f"{r['past_findings']} | {' '.join(r['listening_now']) or '–'} |")
open(os.path.join(R, 'ledger.md'), 'w').write('\n'.join(out) + '\n')
unmeasured = [r['device'] for r in rows if r['kind'] == 'choice' and not r['plays']]
print(f"{len(rows)} devices; {len(unmeasured)} choices the genre-devices report does not measure: {' '.join(unmeasured)}")
print(f"wrote {R}/ledger.md")
