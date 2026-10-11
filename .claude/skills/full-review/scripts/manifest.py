"""The review's slices: every source file with its lines, grouped by subsystem and cut into slices small enough to
read whole. Writes <out>/manifest.json, <out>/slices/<id>.txt, <out>/files.json (every source file, sliced or not) and
<out>/review.json (the commit read, the date, the mode), and prints the slices.

    python3 manifest.py <repo> <out> [--limit 2200] [--no-tests]                  # every file
    python3 manifest.py <repo> <out> --last <last review folder> [--no-tests]     # the files changed since its commit
    python3 manifest.py <repo> <out> --since <commit> [--no-tests]                # the files changed since a commit

A delta review slices only the files changed since the last review's commit, which take in the last review's fixes;
the angles, the device families and the genre clusters still cover the whole code.
"""
import argparse, datetime, glob, json, os, re, subprocess
from load import review, write_review

parser = argparse.ArgumentParser()
parser.add_argument('repo')
parser.add_argument('out')
parser.add_argument('--limit', type=int, default=2200, help='the most lines in a slice')
parser.add_argument('--small', type=int, default=700, help='a slice under this many lines is merged with others')
parser.add_argument('--no-tests', action='store_true', help='leave the unit and page tests out')
parser.add_argument('--last', help='the last review\'s folder: slice only the files changed since its commit')
parser.add_argument('--since', help='a commit: slice only the files changed since it')
args = parser.parse_args()
root = args.repo


def git(*a):
    return subprocess.run(['git', '-C', root, *a], check=True, capture_output=True, text=True).stdout.strip()


since = args.since
if args.last:
    since = review(args.last).get('commit')
    if not since:
        raise SystemExit(f"{args.last}/review.json names no commit: pass --since <the commit it read>")
changed = set(git('diff', '--name-only', since, 'HEAD').splitlines()) if since else None


def lines(path):
    with open(os.path.join(root, path), encoding='utf-8', errors='ignore') as f:
        return sum(1 for _ in f)


def files(pattern):
    return sorted(p for p in glob.glob(pattern, root_dir=root, recursive=True) if '/bin/' not in p and '/obj/' not in p)


# Rmg.Core/Composition by subsystem, by file name; a file no theme names lands in composition-rest
composition = files('Rmg.Core/Composition/*.cs')
themes = [
    ('pipeline', r'^(SongDecisions|SongStages|SongGenerator|SongFormGenerator|SongForms|SongStructureGenerator|SongLength|SectionLength|TimelineEdits|TracePoints|SongDescriptions|SongTracks|SongPart|SongParts|LateEntries|Arrangement|Texture|ShortBars)\.cs'),
    ('section', r'^(SectionGenerator|SectionEnergy|SectionMeter|SectionTempo|SectionBrush|SectionPedals|Unconventionality|HarmonicUnconventionality|RhythmicUnconventionality|LayerStates|StateTimelineGenerator|BarStateGenerator|VelocityLayers|CompositionStateKinds)\.cs'),
    ('harmony', r'^(Progressions|ChordScales|ChordShapes|ChordVoicing|Chord|ChordContext|PassingChords|Reharmonisation|Planing|KeyChange|InnerLines|HarmonicRhythm|Scales|VoiceLeader|VoiceLeadingLayers|Arpeggios|GatedChords|Realizer)\.cs'),
    ('lines', r'^(Line|LinePattern|LinePlacement|LineProfile|LineScales|LineFills|LineChords|LineDoubling|MelodyLayers|MelodyBusyness|CounterLayers|RiffLayers|BassLeadingLayers|PhraseSchemes|PhraseLengths|Registers|Ornaments|Chugs|Breakdowns)\.cs'),
    ('patterns', r'^(PatternGenerator|RhythmLayers|RhythmSettings|Feels|Groove|GenreRhythm|Meter|Polymeter|BeatAccent|Figures|Mutations|Pocket)\.cs'),
    ('drums', r'^(Drum.*|SectionDrumGenerator|PercussionSections|PercussionInstrumentDefinition|SongDrumRule|HatRolls|Ghosts|LiftCues|TrackGroup)\.cs'),
    ('fills', r'^(Fill.*|FormLayers|Risers|MixFilter|StopTime|Pushes|SilentStart)\.cs'),
    ('solos', r'^(Solo.*|Soloist|DrumSolos)\.cs'),
    ('expression', r'^(Expression|Articulations|DoubleTracking|LouderForms|Insertions|Panning|InstrumentsOverTime|InstrumentTraits|InstrumentRanges|InstrumentRoles)\.cs'),
    ('genres', r'^(Genre.*)\.cs'),
]
groups, used = {}, set()
for name, rx in themes:
    for f in composition:
        if f not in used and re.match(rx, os.path.basename(f)):
            groups.setdefault(name, []).append(f)
            used.add(f)
rest = [f for f in composition if f not in used]
if rest:
    groups['composition-rest'] = rest
groups['core-foundation'] = files('Rmg.Core/*.cs') + files('Rmg.Core/Events/*.cs') + files('Rmg.Core/Probabilities/*.cs') + files('Rmg.Core/Versions/*.cs')
groups['core-songs-render'] = files('Rmg.Core/Songs/*.cs') + files('Rmg.Core/Rendering/*.cs') + files('Rmg/**/*.cs')
groups['server'] = files('Rmg.WebApi/**/*.cs')
groups['client'] = [f for f in files('Rmg.WebApi/Client/**/*') if os.path.splitext(f)[1] in ('.js', '.css', '.html')]
if not args.no_tests:
    groups['tests'] = files('Rmg.Tests/**/*.cs')
    groups['page-tests'] = files('Rmg.Tests.Page/**/*.cs')

every_file = sorted(p for paths in groups.values() for p in paths)
if changed is not None:
    groups = {g: [p for p in paths if p in changed] for g, paths in groups.items()}
    groups = {g: paths for g, paths in groups.items() if paths}

# every group cut into slices of at most the limit, a file never split
slices = []
for group, paths in groups.items():
    cut, current, count = [], [], 0
    for path in paths:
        n = lines(path)
        if current and count + n > args.limit:
            cut.append(current)
            current, count = [], 0
        current.append((path, n))
        count += n
    if current:
        cut.append(current)
    for k, part in enumerate(cut, 1):
        slices.append({'id': group if len(cut) == 1 else f'{group}-{k}', 'group': group, 'files': [{'path': p, 'lines': n} for p, n in part]})

# the small slices merged, a group's with its own where it has a slice to spare, else into one of their own
def size(s): return sum(f['lines'] for f in s['files'])
small = [s for s in slices if size(s) < args.small]
slices = [s for s in slices if size(s) >= args.small]
pool = []
for s in small:
    home = min((x for x in slices if x['group'] == s['group'] and size(x) + size(s) <= args.limit * 1.15), key=size, default=None)
    if home:
        home['files'] += s['files']
    else:
        pool.append(s)
batch, k = [], 1
for s in pool + [None]:
    if s is None or batch and sum(size(x) for x in batch) + size(s) > args.limit:
        if batch:
            slices.append({'id': f'small-{k}', 'group': 'small', 'files': [f for x in batch for f in x['files']]})
            k += 1
        batch = []
    if s is not None:
        batch.append(s)

for s in slices:
    s['lines'] = size(s)
os.makedirs(os.path.join(args.out, 'slices'), exist_ok=True)
os.makedirs(os.path.join(args.out, 'findings'), exist_ok=True)
os.makedirs(os.path.join(args.out, 'verify'), exist_ok=True)
with open(os.path.join(args.out, 'manifest.json'), 'w') as f:
    json.dump(slices, f, indent=1)
json.dump([{'path': p, 'lines': lines(p)} for p in every_file], open(os.path.join(args.out, 'files.json'), 'w'), indent=1)
write_review(args.out, repo=os.path.abspath(root), commit=git('rev-parse', 'HEAD'), date=datetime.date.today().isoformat(),
             mode='delta' if since else 'full', since=since or '', tests=not args.no_tests)
for s in slices:
    with open(os.path.join(args.out, 'slices', s['id'] + '.txt'), 'w') as f:
        f.write('\n'.join(f"{x['path']} ({x['lines']} lines)" for x in s['files']) + '\n')

total = sum(s['lines'] for s in slices)
print(f"{'delta since ' + since[:7] if since else 'full'}: {len(slices)} slices, {sum(len(s['files']) for s in slices)} files, {total} lines")
for s in slices:
    print(f"  {s['id']:24} {s['lines']:6}  {' '.join(os.path.basename(x['path']) for x in s['files'])[:110]}")
