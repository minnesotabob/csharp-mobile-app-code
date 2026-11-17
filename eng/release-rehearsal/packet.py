"""Bounded synthetic release-packet rehearsal; never authorizes distribution."""
import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

LIMIT = 65536
GATES = ('portable', 'api', 'android-package', 'android-update')
MANUAL = ('physical-devices', 'accessibility', 'live-providers',
          'privacy-lifecycle', 'ios-release', 'production-operations')


class InvalidPacket(ValueError):
    pass


def require(condition):
    if not condition:
        raise InvalidPacket('invalid_packet')


def sha(value, length=40):
    return isinstance(value, str) and re.fullmatch('[a-f0-9]{' + str(length) + '}', value) is not None


def unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result)
        result[key] = value
    return result


def bounded(path):
    with path.open('rb') as stream:
        data = stream.read(LIMIT + 1)
    require(0 < len(data) <= LIMIT)
    return data


def owned_file(root, name):
    require(isinstance(name, str) and re.fullmatch('[a-z0-9][a-z0-9-]{0,50}\\.(json|bin)', name))
    path = root / name
    # Flat owned fixture directory only: no traversals, symlinks or absolute paths.
    require(not path.is_symlink() and path.resolve().parent == root.resolve())
    return path


def build_packet(root, code_revision, manuscript_revision):
    require(sha(code_revision) and sha(manuscript_revision))
    require(root.is_dir() and not root.is_symlink())
    plan_bytes = bounded(owned_file(root, 'candidate.json'))
    plan = json.loads(plan_bytes, object_pairs_hook=unique)
    require(isinstance(plan, dict) and set(plan) == {
        'schema', 'profile', 'code_revision', 'manuscript_revision', 'artifact', 'evidence'})
    require(type(plan['schema']) is int and plan['schema'] == 1)
    require(plan['profile'] == 'synthetic-rehearsal')
    require(plan['code_revision'] == code_revision and plan['manuscript_revision'] == manuscript_revision)
    artifact = plan['artifact']
    require(isinstance(artifact, dict) and set(artifact) == {'file', 'sha256'})
    require(sha(artifact['sha256'], 64))
    require(artifact['file'] == 'candidate.bin')
    digest = hashlib.sha256(bounded(owned_file(root, artifact['file']))).hexdigest()
    require(digest == artifact['sha256'])
    evidence = plan['evidence']
    require(isinstance(evidence, dict) and set(evidence) == set(GATES))
    rows = []
    for gate in GATES:
        require(evidence[gate] == gate + '.json')
        data = bounded(owned_file(root, evidence[gate]))
        receipt = json.loads(data, object_pairs_hook=unique)
        require(isinstance(receipt, dict) and set(receipt) == {
            'schema', 'profile', 'gate', 'code_revision', 'artifact_sha256', 'status'})
        require(type(receipt['schema']) is int and receipt['schema'] == 1)
        require(receipt['profile'] == 'synthetic-rehearsal' and receipt['gate'] == gate)
        require(receipt['code_revision'] == code_revision)
        # Every row in this deliberately strict lab is tied to the same fixture bytes.
        # A real test/package graph needs explicit per-stage subjects, not this shortcut.
        require(receipt['artifact_sha256'] == digest)
        require(receipt['status'] in ('pass', 'fail'))
        rows.append({'gate': gate, 'declared_status': receipt['status'],
                     'receipt_sha256': hashlib.sha256(data).hexdigest()})
    return {'schema': 1, 'profile': 'synthetic-rehearsal',
            'code_revision': code_revision, 'manuscript_revision': manuscript_revision,
            'candidate_sha256': digest, 'plan_sha256': hashlib.sha256(plan_bytes).hexdigest(),
            'synthetic_gate': 'pass' if all(r['declared_status'] == 'pass' for r in rows) else 'fail',
            'evidence': rows, 'manual': [{'gate': g, 'status': 'not_run'} for g in MANUAL],
            'distribution': 'blocked', 'release_readiness': 'unassessed'}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, required=True)
    parser.add_argument('--revision', required=True)
    parser.add_argument('--manuscript-revision', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        packet = build_packet(args.directory, args.revision, args.manuscript_revision)
        # Fully validate before opening output. Refuse to overwrite prior evidence.
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(packet, stream, indent=2)
            stream.write('\n')
        print('Synthetic packet: ' + packet['synthetic_gate'] + '; distribution: blocked.')
        return 0 if packet['synthetic_gate'] == 'pass' else 1
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        # Never echo supplied JSON, filesystem paths or exception details.
        print('Release rehearsal rejected; no new valid packet.', file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main())
