"""The verifiers' batches: every high and medium finding (every finding with --all), with an id, grouped by area into
batches of at most 30. Writes <out>/verify/<area>-input.json and prints the batches.

    python3 verify_batches.py <review folder> [--all] [--size 30]
"""
import argparse, collections, glob, json, os
from areas import area

parser = argparse.ArgumentParser()
parser.add_argument('review')
parser.add_argument('--all', action='store_true', help='the low findings too')
parser.add_argument('--size', type=int, default=30)
args = parser.parse_args()
R = args.review

by_area = collections.defaultdict(list)
for path in sorted(glob.glob(os.path.join(R, 'findings', '*.json'))):
    source = os.path.basename(path)[:-5]
    for k, f in enumerate(json.load(open(path)).get('findings', [])):
        if args.all or f.get('severity') in ('high', 'medium'):
            by_area[area(f.get('file', ''))].append(dict(id=f'{source}#{k}', **f))

os.makedirs(os.path.join(R, 'verify'), exist_ok=True)
for name, items in sorted(by_area.items()):
    items.sort(key=lambda f: (f.get('file', ''), f.get('line') or 0))
    parts = [items[i:i + args.size] for i in range(0, len(items), args.size)]
    for k, part in enumerate(parts, 1):
        batch = name if len(parts) == 1 else f'{name}-{k}'
        with open(os.path.join(R, 'verify', f'{batch}-input.json'), 'w') as f:
            json.dump(part, f, indent=1)
        print(f"{batch}: {len(part)} findings")
