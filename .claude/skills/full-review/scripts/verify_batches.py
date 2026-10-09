"""The verifiers' batches: every finding (the high and medium alone with --high-medium), with an id, grouped by area
into batches of at most 30. Writes <out>/verify/<area>-input.json and prints the batches.

    python3 verify_batches.py <review folder> [--high-medium] [--size 30]
"""
import argparse, collections, glob, json, os
from areas import area

parser = argparse.ArgumentParser()
parser.add_argument('review')
parser.add_argument('--high-medium', action='store_true', help='the high and medium findings alone')
parser.add_argument('--size', type=int, default=30)
args = parser.parse_args()
R = args.review

by_area = collections.defaultdict(list)
for path in sorted(glob.glob(os.path.join(R, 'findings', '*.json'))):
    source = os.path.basename(path)[:-5]
    for k, f in enumerate(json.load(open(path)).get('findings', [])):
        if not args.high_medium or f.get('severity') in ('high', 'medium'):
            by_area[area(f.get('file', ''), f.get('category'))].append(dict(id=f'{source}#{k}', **f))

os.makedirs(os.path.join(R, 'verify'), exist_ok=True)
for name, items in sorted(by_area.items()):
    items.sort(key=lambda f: (f.get('file', ''), f.get('line') or 0))
    parts = [items[i:i + args.size] for i in range(0, len(items), args.size)]
    for k, part in enumerate(parts, 1):
        batch = name if len(parts) == 1 else f'{name}-{k}'
        with open(os.path.join(R, 'verify', f'{batch}-input.json'), 'w') as f:
            json.dump(part, f, indent=1)
        print(f"{batch}: {len(part)} findings")
