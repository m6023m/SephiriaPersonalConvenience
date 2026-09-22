"""Dependency-aware test selection. Never infers correctness from a successful build.

plan requires explicit cases (id, kind, dependencies). Dependencies are file paths,
or {path, symbol} for a C# method/class. record accepts only results tied to that
plan and unchanged inputs. Accuracy, UI and catalog smoke cases stay separate.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
from datetime import datetime, timezone
from functools import lru_cache

ROOT = Path(__file__).resolve().parent.parent

def read_json(path, default=None):
    return json.loads(Path(path).read_text(encoding='utf-8-sig')) if Path(path).exists() else default

def extract_symbol(source, symbol):
    # Explicit symbol selectors prevent unrelated handlers from invalidating a case.
    name = symbol.split('.')[-1]
    candidates = list(re.finditer(r'(?m)^\s*(?:public|private|internal|protected)[^\n;=]*\b' + re.escape(name) + r'\s*(?:\([^;]*?\)|(?=\s*\{))', source))
    if len(candidates) != 1:
        raise ValueError(f'Expected one declaration for {symbol}, found {len(candidates)}')
    start = candidates[0].start()
    opening = source.find('{', candidates[0].end())
    if opening < 0:
        raise ValueError(f'No body for {symbol}')
    # Tokenize strings/comments so braces in interpolation/labels do not end a body.
    token = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|[{}]')
    depth = 0
    for match in token.finditer(source, opening):
        if match.group() == '{': depth += 1
        elif match.group() == '}':
            depth -= 1
            if depth == 0: return source[start:match.end()]
    raise ValueError(f'Unclosed body for {symbol}')

@lru_cache(maxsize=8)
def structured_dependency(path, modified, size):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

@lru_cache(maxsize=2048)
def dependency_hash(path, modified, size, symbol=None, json_key=None):
    source = Path(path)
    digest = hashlib.sha256()
    if json_key is not None:
        if symbol:
            raise ValueError('Choose either json_key or symbol')
        value = structured_dependency(path, modified, size)[json_key]
        digest.update(json.dumps(value, sort_keys=True, ensure_ascii=False).encode())
    elif symbol:
        digest.update(extract_symbol(source.read_text(encoding='utf-8-sig'), symbol).encode())
    else:
        with source.open('rb') as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b''):
                digest.update(chunk)
    current = source.stat()
    if (current.st_mtime_ns, current.st_size) != (modified, size):
        raise ValueError('Dependency changed while hashing: ' + path)
    return digest.digest()

def fingerprint(case):
    digest = hashlib.sha256()
    digest.update(json.dumps(case, sort_keys=True, ensure_ascii=False).encode())
    for dependency in case['dependencies']:
        entry = {'path': dependency} if isinstance(dependency, str) else dependency
        path = (ROOT / entry['path']).resolve()
        if not path.is_file(): raise FileNotFoundError(path)
        info = path.stat()
        digest.update(entry['path'].encode())
        digest.update(dependency_hash(str(path), info.st_mtime_ns, info.st_size, entry.get('symbol'), entry.get('json_key')))
    return digest.hexdigest()

def write_json(path, value):
    path = Path(path); path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    temporary.replace(path)

def make_plan(cases_path, ledger_path, output_path):
    cases = read_json(cases_path)['cases']
    ledger = read_json(ledger_path, {'passed': {}})['passed']
    if len({c['id'] for c in cases}) != len(cases): raise ValueError('Duplicate case IDs')
    selected, unchanged = [], []
    for case in cases:
        if case.get('kind') not in ('accuracy', 'ui', 'catalog'):
            raise ValueError('Explicit verification kind required: ' + case['id'])
        if not case.get('dependencies'):
            raise ValueError('Verification dependencies required: ' + case['id'])
        signature = fingerprint(case)
        old = ledger.get(case['id'])
        if not old:
            reason = 'not_verified'
        elif old['signature'] != signature:
            reason = 'dependencies_changed'
        elif not Path(old['evidence']).is_file():
            reason = 'evidence_missing'
        elif hashlib.sha256(Path(old['evidence']).read_bytes()).hexdigest() != old['evidence_sha256']:
            reason = 'evidence_changed'
        else:
            reason = 'verified_unchanged'
        row = {'case': case, 'signature': signature, 'reason': reason}
        (unchanged if reason == 'verified_unchanged' else selected).append(row)
    plan = {'selected': selected, 'unchanged': unchanged}
    write_json(output_path, plan)
    print(f'Selected {len(selected)}; unchanged verified cases {len(unchanged)}')

def record(plan_path, results_path, ledger_path):
    plan = read_json(plan_path)
    results = read_json(results_path)
    plan_hash = hashlib.sha256(Path(plan_path).read_bytes()).hexdigest()
    if results.get('plan_sha256') != plan_hash: raise ValueError('Results do not belong to this plan')
    ledger = read_json(ledger_path, {'passed': {}})
    outcomes = {r['id']: r for r in results['results']}
    if len(outcomes) != len(results['results']): raise ValueError('Duplicate result IDs')
    selected = {row['case']['id']: row for row in plan['selected']}
    if set(outcomes) - set(selected): raise ValueError('Result contains unplanned cases')
    for case_id, row in selected.items():
        outcome = outcomes.get(case_id)
        if not outcome or outcome.get('status') != 'passed':
            ledger['passed'].pop(case_id, None)
            continue
        if fingerprint(row['case']) != row['signature']: raise ValueError('Inputs changed during verification: ' + case_id)
        evidence = Path(outcome['evidence']).resolve()
        if not evidence.is_file(): raise FileNotFoundError(evidence)
        ledger['passed'][case_id] = {'signature': row['signature'], 'evidence': str(evidence),
            'evidence_sha256': hashlib.sha256(evidence.read_bytes()).hexdigest(),
            'kind': row['case']['kind'], 'checked_at': datetime.now(timezone.utc).isoformat()}
    write_json(ledger_path, ledger)

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest='command', required=True)
    plan = sub.add_parser('plan'); plan.add_argument('--cases', required=True); plan.add_argument('--ledger', required=True); plan.add_argument('--output', required=True)
    save = sub.add_parser('record'); save.add_argument('--plan', required=True); save.add_argument('--results', required=True); save.add_argument('--ledger', required=True)
    args = parser.parse_args()
    if args.command == 'plan': make_plan(args.cases, args.ledger, args.output)
    else: record(args.plan, args.results, args.ledger)
