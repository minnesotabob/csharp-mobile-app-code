"""Public-safe acceptance projection from same-job xUnit XML; no release approval."""
import argparse
import hashlib
import json
import re
import sys
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

MAX_BYTES = 8 * 1024 * 1024


class InvalidEvidence(ValueError):
    pass


def require(condition, code):
    if not condition:
        raise InvalidEvidence(code)


def bounded(path):
    with Path(path).open('rb') as stream:
        data = stream.read(MAX_BYTES + 1)
    require(0 < len(data) <= MAX_BYTES, 'input_size')
    return data


def load_suite(path):
    data = bounded(path)
    require(b'<!DOCTYPE' not in data.upper() and b'<!ENTITY' not in data.upper(), 'xml_declaration')
    root = ET.fromstring(data)
    require(root.tag == 'assemblies' and root.get('schema-version') in ('2', '3'), 'xml_schema')
    assemblies = root.findall('assembly')
    require(len(assemblies) == 1, 'assembly_count')
    assembly = assemblies[0]
    require(assembly.get('errors') == '0' and not assembly.findall('./errors/error'), 'environment_error')
    tests = assembly.findall('./collection/test')
    require(tests and len(tests) <= 10000, 'case_count')
    case_ids, results, methods = set(), [], {}
    for case in tests:
        case_id = case.get('id')
        selector = (case.get('type'), case.get('method'))
        result = case.get('result')
        require(isinstance(case_id, str) and len(case_id) <= 64 and case_id not in case_ids, 'case_identity')
        try:
            case_id = str(uuid.UUID(case_id))
        except ValueError:
            raise InvalidEvidence('case_identity') from None
        require(case_id not in case_ids, 'case_identity')
        require(result in ('Pass', 'Fail', 'Skip', 'NotRun'), 'case_result')
        case_ids.add(case_id)
        results.append(result)
        # Type/method are optional in xUnit XML. Unmapped cases still affect suite cleanliness.
        # A required method without metadata cannot match its exact selector and fails the gate.
        if all(selector):
            methods.setdefault('.'.join(selector), []).append(result)
    for attr, result in [('passed', 'Pass'), ('failed', 'Fail'), ('skipped', 'Skip'), ('not-run', 'NotRun')]:
        require(int(assembly.get(attr, '-1')) == results.count(result), 'summary_count')
    require(int(assembly.get('total', '-1')) == len(tests), 'summary_total')
    return {'sha256': hashlib.sha256(data).hexdigest(), 'total': len(tests),
            'passed': results.count('Pass'), 'clean': all(r == 'Pass' for r in results), 'methods': methods}


def load_plan(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, 'duplicate_json_key')
            result[key] = value
        return result
    plan = json.loads(bounded(path), object_pairs_hook=unique)
    require(set(plan) == {'schema', 'automated', 'manual'} and plan['schema'] == 1, 'plan_schema')
    require(isinstance(plan['automated'], list) and 0 < len(plan['automated']) <= 64, 'plan_rows')
    require(isinstance(plan['manual'], list) and 0 < len(plan['manual']) <= 64, 'plan_manual')
    ids = set()
    for row in plan['automated'] + plan['manual']:
        require(isinstance(row, dict), 'plan_row')
        identity = row.get('id', '')
        require(isinstance(identity, str) and re.fullmatch(r'A36-[A-Z-]{3,40}', identity) and identity not in ids, 'plan_id')
        ids.add(identity)
        if row in plan['automated']:
            require(set(row) == {'id', 'suite', 'method', 'cases'}, 'plan_fields')
            require(row['suite'] in ('core', 'api') and isinstance(row['method'], str)
                    and re.fullmatch(r'GnazzosWorld\.(Core|Api)\.Tests\.[A-Za-z0-9_]+\.[A-Za-z0-9_]+', row['method']), 'plan_selector')
            require(type(row['cases']) is int and 1 <= row['cases'] <= 100, 'plan_case_count')
        else:
            require(set(row) == {'id'}, 'manual_fields')
    return plan


def build_report(plan, suites, revision, run_id):
    require(isinstance(revision, str) and re.fullmatch('[0-9a-f]{40}', revision), 'revision')
    require(isinstance(run_id, str) and re.fullmatch('[0-9]{1,20}', run_id), 'run_id')
    require(set(suites) == {'core', 'api'}, 'suite_set')
    rows = []
    for row in plan['automated']:
        observed = suites[row['suite']]['methods'].get(row['method'], [])
        status = 'automated_pass' if len(observed) == row['cases'] and all(r == 'Pass' for r in observed) else 'not_satisfied'
        rows.append({'id': row['id'], 'suite': row['suite'], 'method': row['method'],
                     'expected_cases': row['cases'], 'observed_cases': len(observed), 'status': status})
    gate = all(s['clean'] for s in suites.values()) and all(r['status'] == 'automated_pass' for r in rows)
    return {'schema': 1, 'revision': revision, 'run_id': run_id,
            'automated_gate': 'pass' if gate else 'fail', 'release_readiness': 'unassessed',
            'suites': {key: {k: value[k] for k in ('sha256', 'total', 'passed', 'clean')} for key, value in suites.items()},
            'automated': rows, 'manual': [{'id': row['id'], 'status': 'not_run'} for row in plan['manual']]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for arg in ('plan', 'core', 'api', 'revision', 'run-id', 'output'):
        parser.add_argument('--' + arg, required=True)
    args = parser.parse_args()
    try:
        plan = load_plan(args.plan)
        suites = {'core': load_suite(args.core), 'api': load_suite(args.api)}
        result = build_report(plan, suites, args.revision, args.run_id)
        # New output only. Raw XML/messages/stack traces/paths never enter this projection.
        with Path(args.output).open('x', encoding='utf-8') as stream:
            json.dump(result, stream, indent=2)
            stream.write('\n')
        print('Acceptance automated gate: ' + result['automated_gate'] + '; manual checks: not_run; release readiness: unassessed.')
        return 0 if result['automated_gate'] == 'pass' else 1
    except InvalidEvidence as error:
        # Only fixed validator codes are printed, never input values or exception text.
        print('Acceptance input rejected: ' + str(error) + '; no acceptance claim.', file=sys.stderr)
        return 2
    except (OSError, ValueError, ET.ParseError, TypeError, KeyError):
        print('Invalid or unavailable acceptance input/output; no acceptance claim.', file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main())
