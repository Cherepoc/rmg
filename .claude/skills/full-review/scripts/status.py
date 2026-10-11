"""The fixing's bookkeeping, by each finding's number `n` in deduped.json: the package it is in, what became of it, and
the record written from them. A finding is looked up here, never in merged.json, whose order is another.

    python3 status.py <review> init                          # fixes the numbers; refuses once deduped.json renumbers
    python3 status.py <review> assign <package> <n>...       # puts findings in a package, moving them from another
    python3 status.py <review> show <package>                # a package's findings in full, with their statuses
    python3 status.py <review> set "<status>" <n>...          # e.g. "fixed in 6f577af", "taste: asked (?listen=x)"
    python3 status.py <review> package <package> <commit> "<songs changed>"
    python3 status.py <review> check [<package>]             # exits 1 on a finding unassigned or, in the package, unstatused
    python3 status.py <review> record <out.md>               # the record's totals and tables

A status starts with one of: fixed, partly, decided, kept, taste, roadmap (an architecture proposal accepted as a
plan), open; the rest says how, where and why. Writes
<review>/plan/assignment.json, status.json, packages.json and numbers.json.
"""
import collections, json, os, sys

R, command, args = sys.argv[1], sys.argv[2], sys.argv[3:]
plan = os.path.join(R, 'plan')
os.makedirs(plan, exist_ok=True)
KINDS = ('fixed', 'partly', 'decided', 'kept', 'taste', 'roadmap', 'open')


def load(name, default):
    p = os.path.join(plan, name)
    return json.load(open(p)) if os.path.exists(p) else default


def save(name, value):
    json.dump(value, open(os.path.join(plan, name), 'w'), indent=1)


deduped = {f['n']: f for f in json.load(open(os.path.join(R, 'deduped.json')))}
numbers = {n: [f['file'], f.get('line'), (f.get('summary') or '')[:80]] for n, f in deduped.items()}
fixed = {int(k): v for k, v in load('numbers.json', {}).items()}
if command == 'init':
    if fixed and fixed != numbers:
        raise SystemExit('numbers.json differs from deduped.json: merge.py was run again after planning')
    save('numbers.json', numbers)
    print(f"{len(numbers)} findings numbered")
    sys.exit(0)
if fixed != numbers:
    raise SystemExit('deduped.json does not match numbers.json: run init first, or merge.py was run again after planning')

assignment = {int(k): v for k, v in load('assignment.json', {}).items()}
status = {int(k): v for k, v in load('status.json', {}).items()}
packages = load('packages.json', {})


def ns(xs):
    out = [int(x) for x in xs]
    bad = [n for n in out if n not in deduped]
    if bad:
        raise SystemExit(f"no finding numbered {bad}")
    return out


def line(n):
    f = deduped[n]
    return f"{n:4} {f['severity'][0]} {f['file']}:{f.get('line')} [{f['category']}] {f.get('summary', '')}"


if command == 'assign':
    package, items = args[0], ns(args[1:])
    for n in items:
        if assignment.get(n) not in (None, package):
            print(f"moved {n} from {assignment[n]} to {package}")
        assignment[n] = package
    save('assignment.json', {str(k): v for k, v in sorted(assignment.items())})
elif command == 'show':
    for n in sorted(n for n, p in assignment.items() if p == args[0]):
        f = deduped[n]
        print(line(n))
        print(f"     scenario: {f.get('scenario', '')}\n     fix: {f.get('fix', '')}\n     status: {status.get(n, '-')}")
        for a in f.get('also', []):
            print(f"     also: {a['summary']}")
elif command == 'set':
    text, items = args[0], ns(args[1:])
    if text.split()[0].rstrip(':,') not in KINDS:
        raise SystemExit(f"a status starts with one of {KINDS}")
    for n in items:
        status[n] = text
    save('status.json', {str(k): v for k, v in sorted(status.items())})
elif command == 'package':
    packages[args[0]] = {'commit': args[1], 'songs': args[2]}
    save('packages.json', packages)
elif command == 'check':
    problems = [f"unassigned: {line(n)}" for n in deduped if n not in assignment]
    scope = [n for n, p in assignment.items() if not args or p == args[0]]
    problems += [f"no status: {line(n)}" for n in sorted(scope) if n not in status]
    for p in problems:
        print(p)
    print(f"{len(scope)} findings checked, {len(problems)} problems")
    sys.exit(1 if problems else 0)
elif command == 'record':
    kind = {n: status.get(n, 'open').split()[0].rstrip(':,') for n in deduped}

    def cell(text, k=200):
        return ' '.join(str(text or '').split()).replace('|', '\\|')[:k]

    out = []
    w = out.append
    totals = collections.Counter(kind.values())
    w('## Totals\n')
    for k in KINDS:
        if totals[k]:
            w(f'- {k}: {totals[k]}')
    w('\n## The packages\n')
    w('| Package | Commit | Corpus / preset songs changed | ' + ' | '.join(k.capitalize() for k in KINDS) + ' |')
    w('|---|---|---|' + '---|' * len(KINDS))
    names = sorted(set(assignment.values()))
    for p in names:
        c = collections.Counter(kind[n] for n, q in assignment.items() if q == p)
        meta = packages.get(p, {})
        w(f"| {p} | {meta.get('commit', '')} | {meta.get('songs', '')} | " + ' | '.join(str(c[k]) for k in KINDS) + ' |')
    w('\n## Open\n')
    w('| # | Sev | Where | Finding | Status |')
    w('|---|---|---|---|---|')
    for n in sorted((n for n in deduped if kind[n] in ('open', 'partly')), key=lambda n: (deduped[n]['severity'] != 'high', deduped[n]['severity'] != 'medium', n)):
        f = deduped[n]
        w(f"| {n} | {f['severity']} | `{f['file']}:{f.get('line')}` | {cell(f.get('summary'))} | {cell(status.get(n, 'open: not worked'), 300)} |")
    w('\n## Every finding, by package\n')
    for p in names:
        meta = packages.get(p, {})
        w(f"\n### {p}" + (f" ({meta['commit']})" if meta.get('commit') else '') + '\n')
        w('| # | Sev | Where | Category | Finding | Status |')
        w('|---|---|---|---|---|---|')
        for n in sorted(n for n, q in assignment.items() if q == p):
            f = deduped[n]
            w(f"| {n} | {f['severity']} | `{f['file']}:{f.get('line')}` | {f['category']} | {cell(f.get('summary'))} | {cell(status.get(n, 'open: not worked'), 300)} |")
    open(args[0], 'w').write('\n'.join(out) + '\n')
    print(f"wrote {args[0]}: {dict(totals)}")
else:
    raise SystemExit(f"unknown command {command}")
