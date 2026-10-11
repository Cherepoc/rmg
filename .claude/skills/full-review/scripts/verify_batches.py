"""The verifiers' batches: every finding (the high and medium alone with --high-medium) and every `elsewhere` entry, with
an id, grouped by area into batches of at most 30. A finding of taste is not sent: reading cannot verify how a thing
sounds, and it goes to the report as a listening question; nor is an architecture proposal or an idiom gap, each
reviewed as a plan. Writes <out>/verify/<area>-input.json and prints the batches.

    python3 verify_batches.py <review folder> [--high-medium] [--size 30]
"""
import argparse, collections, json, os
from areas import area
from load import findings

parser = argparse.ArgumentParser()
parser.add_argument('review')
parser.add_argument('--high-medium', action='store_true', help='the high and medium findings alone')
parser.add_argument('--size', type=int, default=30)
args = parser.parse_args()
R = args.review

by_area = collections.defaultdict(list)
taste = proposals = 0
for f in findings(R):
    if f.get('kind') == 'taste':
        taste += 1
        continue
    if f.get('category') in ('architecture', 'idiom-gap'):
        proposals += 1
        continue
    if not args.high_medium or f.get('severity') in ('high', 'medium') or f.get('elsewhere'):
        by_area[area(f.get('file', ''), f.get('category'))].append(f)

os.makedirs(os.path.join(R, 'verify'), exist_ok=True)
for name, items in sorted(by_area.items()):
    items.sort(key=lambda f: (f.get('file', ''), f.get('line') or 0))
    parts = [items[i:i + args.size] for i in range(0, len(items), args.size)]
    for k, part in enumerate(parts, 1):
        batch = name if len(parts) == 1 else f'{name}-{k}'
        with open(os.path.join(R, 'verify', f'{batch}-input.json'), 'w') as f:
            json.dump([{x: v for x, v in g.items() if x != 'source'} for g in part], f, indent=1)
        print(f"{batch}: {len(part)} findings")
print(f"{taste} findings of taste left to listening, {proposals} architecture proposals and idiom gaps to review as plans, not verified")
