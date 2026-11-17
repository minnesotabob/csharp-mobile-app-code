"""Create and exercise owned synthetic fixtures in a new output directory."""
import argparse
import hashlib
import json
from pathlib import Path
from packet import GATES, InvalidPacket, build_packet, main as packet_main, sha


def create_fixture(root, code_revision, manuscript_revision):
    if not sha(code_revision) or not sha(manuscript_revision):
        raise ValueError('invalid_revision')
    root.mkdir(exist_ok=False)
    data = b'Invented release rehearsal bytes. This is not an APK or IPA.\n'
    (root / 'candidate.bin').write_bytes(data)
    digest = hashlib.sha256(data).hexdigest()
    plan = {'schema': 1, 'profile': 'synthetic-rehearsal',
            'code_revision': code_revision, 'manuscript_revision': manuscript_revision,
            'artifact': {'file': 'candidate.bin', 'sha256': digest},
            'evidence': {gate: gate + '.json' for gate in GATES}}
    (root / 'candidate.json').write_text(json.dumps(plan, indent=2) + '\n', encoding='utf-8')
    for gate in GATES:
        receipt = {'schema': 1, 'profile': 'synthetic-rehearsal', 'gate': gate,
                   'code_revision': code_revision, 'artifact_sha256': digest, 'status': 'pass'}
        (root / (gate + '.json')).write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--revision', required=True)
    p.add_argument('--manuscript-revision', required=True)
    a = p.parse_args()
    a.output.mkdir(parents=True, exist_ok=False)
    fixture = a.output / 'fixture'
    create_fixture(fixture, a.revision, a.manuscript_revision)
    result = packet_main(['--directory', str(fixture), '--revision', a.revision,
                          '--manuscript-revision', a.manuscript_revision,
                          '--output', str(a.output / 'synthetic-packet.json')])
    if result != 0:
        raise SystemExit(result)
    # Meaningful failure/restoration rehearsal: retain identity, alter actual bytes.
    original = (fixture / 'candidate.bin').read_bytes()
    (fixture / 'candidate.bin').write_bytes(original + b'changed after evidence\n')
    try:
        build_packet(fixture, a.revision, a.manuscript_revision)
    except InvalidPacket:
        print('Changed candidate bytes rejected as expected.')
    else:
        raise AssertionError('changed_bytes_accepted')
    finally:
        (fixture / 'candidate.bin').write_bytes(original)
    restored = build_packet(fixture, a.revision, a.manuscript_revision)
    assert restored['distribution'] == 'blocked' and restored['synthetic_gate'] == 'pass'
    print('Restored synthetic fixture validates; all manual gates remain not_run.')


if __name__ == '__main__':
    main()
