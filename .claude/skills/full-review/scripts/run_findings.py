"""The test runs' failures as findings: every failed test of a run's output, with its message, the seed it names and the
offset it ran at, at its test's file and line. Writes <review>/findings/run-<offset>.json in the reviewers' format.

    python3 run_findings.py <repo> <review> <offset> <test output file> [<flaky list>]

A failure is a finding to triage, not yet a verdict: a bug in the generator, a test assuming what other songs break,
or a calibration over too few songs; the verifiers trace which, and the fixing checks it against the committed code.
A test the flaky list names (runs.sh: it passed run again alone) is a finding of category `flaky`, a test racing under
the suite's load rather than a song breaking it, which no verifier traces into the generator.
"""
import glob, json, os, re, sys

root, review, offset, output = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
flaky = set(open(sys.argv[5]).read().split()) if len(sys.argv) > 5 and os.path.exists(sys.argv[5]) else set()
text = open(output, encoding='utf-8', errors='ignore').read().splitlines()

# every test's file and line, by its method's name
where = {}
for path in glob.glob(os.path.join(root, 'Rmg.Tests', '**', '*.cs'), recursive=True):
    if '/obj/' in path or '/bin/' in path:
        continue
    for n, line in enumerate(open(path, encoding='utf-8', errors='ignore'), 1):
        m = re.search(r'public async Task (\w+)\(', line)
        if m:
            where.setdefault(m.group(1), (os.path.relpath(path, root), n))

findings = []
for i, line in enumerate(text):
    m = re.match(r'failed (\w+)(\([^)]*\))? \(', line)
    if not m:
        continue
    block = []
    for next_line in text[i + 1:i + 12]:
        if next_line.startswith(('failed ', 'passed ')) or not next_line.strip():
            break
        block.append(next_line.strip())
    message = ' '.join(x for x in block if 'Exception' in x or 'Expected' in x or 'because' in x or 'but found' in x)[:600]
    assertion = next((x for x in block if x.startswith('at Assert')), '')
    seed = re.search(r'seed (\d+)', message)
    file, number = where.get(m.group(1), ('Rmg.Tests', 0))
    if m.group(1) in flaky:
        findings.append({
            'file': file, 'line': number, 'category': 'flaky', 'severity': 'low',
            'summary': f"{m.group(1)} fails in the suite at RMG_CORPUS_OFFSET={offset} but passes run alone",
            'evidence': (message + ' ' + assertion).strip(),
            'scenario': f"RMG_CORPUS_OFFSET={offset}, the whole suite under load; alone it passes",
            'suggestion': 'Find what the test races: a timeout, a shared server or queue, a wait on wall time.',
            'confidence': 'certain',
        })
        continue
    findings.append({
        'file': file, 'line': number, 'category': 'run', 'severity': 'medium',
        'summary': f"{m.group(1)}{m.group(2) or ''} fails at RMG_CORPUS_OFFSET={offset}" + (f", seed {seed.group(1)}" if seed else ''),
        'evidence': (message + ' ' + assertion).strip(),
        'scenario': f"RMG_CORPUS_OFFSET={offset}, the test run alone: {message[:300]}",
        'suggestion': 'Trace the cause: a generator bug, a test assuming what other songs break, or a calibration over too few songs.',
        'confidence': 'certain',
    })

os.makedirs(os.path.join(review, 'findings'), exist_ok=True)
path = os.path.join(review, 'findings', f'run-{offset}.json')
json.dump({'slice': f'run-{offset}', 'coverage': [], 'findings': findings, 'elsewhere': [],
           'slice_notes': f'the suite at RMG_CORPUS_OFFSET={offset}'}, open(path, 'w'), indent=1)
print(f"{len(findings)} failures at offset {offset}: {path}")
