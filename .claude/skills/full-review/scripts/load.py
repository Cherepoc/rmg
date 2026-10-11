"""The review's findings as one list, and the review's own record (review.json), for the other scripts.

Every finding a reviewer wrote, with an id `<source>#<k>`, and every entry of its `elsewhere`, with an id
`<source>@<k>`, as a finding of category `elsewhere` for a verifier to judge: an issue several reviewers saw outside
their slice is otherwise in no table.
"""
import glob, json, os, re


def findings(review):
    out = []
    for path in sorted(glob.glob(os.path.join(review, 'findings', '*.json'))):
        source = os.path.basename(path)[:-5]
        d = json.load(open(path))
        for k, f in enumerate(d.get('findings', [])):
            out.append(dict(f, id=f'{source}#{k}', source=source))
        for k, e in enumerate(d.get('elsewhere', [])):
            out.append({
                'file': e.get('file', ''), 'line': e.get('line') or 0, 'category': 'elsewhere', 'severity': 'low',
                'summary': e.get('summary', ''), 'evidence': e.get('evidence', ''), 'scenario': e.get('scenario', ''),
                'suggestion': e.get('suggestion', ''), 'confidence': 'possible', 'elsewhere': True,
                'id': f'{source}@{k}', 'source': source,
            })
    return out


def words(text):
    return {w for w in re.findall(r'[a-z]{4,}', (text or '').lower())}


def overlap(a, b):
    a, b = words(a), words(b)
    return len(a & b) / max(1, min(len(a), len(b)))


def review(path):
    """The review's record: its commit, date, mode and offsets, written by manifest.py and offsets.py."""
    p = os.path.join(path, 'review.json')
    return json.load(open(p)) if os.path.exists(p) else {}


def write_review(path, **values):
    d = review(path)
    d.update(values)
    json.dump(d, open(os.path.join(path, 'review.json'), 'w'), indent=1)
