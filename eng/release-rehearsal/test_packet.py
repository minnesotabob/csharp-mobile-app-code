import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path
from demo import create_fixture
from packet import InvalidPacket, LIMIT, build_packet, main

CODE = 'a' * 40
MANUSCRIPT = 'b' * 40


class PacketBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / 'fixture'
        create_fixture(self.root, CODE, MANUSCRIPT)

    def change(self, filename, edit):
        path = self.root / filename
        data = json.loads(path.read_text())
        edit(data)
        path.write_text(json.dumps(data))

    def packet(self):
        return build_packet(self.root, CODE, MANUSCRIPT)

    def test_matching_synthetic_evidence_never_authorizes_distribution(self):
        packet = self.packet()
        self.assertEqual('pass', packet['synthetic_gate'])
        self.assertEqual('blocked', packet['distribution'])
        self.assertEqual('unassessed', packet['release_readiness'])
        self.assertTrue(all(row['status'] == 'not_run' for row in packet['manual']))

    def test_changed_candidate_bytes_invalidate_receipts(self):
        (self.root / 'candidate.bin').write_bytes(b'different package')
        with self.assertRaises(InvalidPacket): self.packet()

    def test_stale_code_or_manuscript_revision_is_rejected(self):
        for code, manuscript in [('c'*40, MANUSCRIPT), (CODE, 'c'*40)]:
            with self.subTest(code=code, manuscript=manuscript):
                with self.assertRaises(InvalidPacket): build_packet(self.root, code, manuscript)

    def test_cross_candidate_receipt_is_rejected(self):
        self.change('android-update.json', lambda d: d.update(code_revision='c'*40))
        with self.assertRaises(InvalidPacket): self.packet()

    def test_swapped_gate_receipt_is_rejected(self):
        (self.root / 'api.json').write_bytes((self.root / 'portable.json').read_bytes())
        with self.assertRaises(InvalidPacket): self.packet()

    def test_missing_gate_cannot_be_a_pass(self):
        self.change('candidate.json', lambda d: d['evidence'].pop('api'))
        with self.assertRaises(InvalidPacket): self.packet()

    def test_failed_receipt_keeps_a_failed_synthetic_gate(self):
        self.change('api.json', lambda d: d.update(status='fail'))
        self.assertEqual('fail', self.packet()['synthetic_gate'])

    def test_status_must_be_exact(self):
        self.change('api.json', lambda d: d.update(status=True))
        with self.assertRaises(InvalidPacket): self.packet()

    def test_duplicate_json_key_is_rejected(self):
        path = self.root / 'api.json'
        path.write_text(path.read_text().replace('"status": "pass"', '"status": "fail", "status": "pass"'))
        with self.assertRaises(InvalidPacket): self.packet()

    def test_unknown_fields_are_not_exported(self):
        self.change('api.json', lambda d: d.update(private_diagnostic='never copy this'))
        with self.assertRaises(InvalidPacket): self.packet()

    def test_traversal_and_absolute_input_names_are_rejected(self):
        for name in ['../api.json', str((self.root / 'api.json').resolve())]:
            self.change('candidate.json', lambda d: d['evidence'].update(api=name))
            with self.subTest(name=name):
                with self.assertRaises(InvalidPacket): self.packet()

    def test_symlink_input_is_rejected(self):
        outside = self.root.parent / 'outside.bin'
        outside.write_bytes((self.root / 'candidate.bin').read_bytes())
        (self.root / 'candidate.bin').unlink()
        try:
            (self.root / 'candidate.bin').symlink_to(outside)
        except (OSError, NotImplementedError):
            self.skipTest('symlink unavailable on this host')
        with self.assertRaises(InvalidPacket): self.packet()

    def test_oversized_receipt_is_rejected(self):
        (self.root / 'api.json').write_bytes(b' ' * (LIMIT + 1))
        with self.assertRaises(InvalidPacket): self.packet()

    def test_missing_input_and_malformed_json_leave_no_output(self):
        for value in [None, '{private-diagnostic-broken']:
            if value is None: (self.root / 'api.json').unlink()
            else: (self.root / 'api.json').write_text(value)
            output = self.root.parent / 'packet.json'
            captured = io.StringIO()
            with contextlib.redirect_stderr(captured):
                result = main(['--directory',str(self.root),'--revision',CODE,
                               '--manuscript-revision',MANUSCRIPT,'--output',str(output)])
            self.assertEqual(2, result)
            self.assertFalse(output.exists())
            self.assertNotIn('private-diagnostic', captured.getvalue())

    def test_existing_output_is_not_overwritten(self):
        output = self.root.parent / 'packet.json'
        output.write_text('retained observation')
        with contextlib.redirect_stderr(io.StringIO()):
            result = main(['--directory',str(self.root),'--revision',CODE,
                           '--manuscript-revision',MANUSCRIPT,'--output',str(output)])
        self.assertEqual(2,result)
        self.assertEqual('retained observation', output.read_text())

    def test_boolean_schema_cannot_masquerade_as_version_one(self):
        self.change('api.json', lambda d: d.update(schema=True))
        with self.assertRaises(InvalidPacket): self.packet()

    def test_fixture_cannot_claim_observed_production_profile(self):
        self.change('candidate.json', lambda d: d.update(profile='production'))
        with self.assertRaises(InvalidPacket): self.packet()


if __name__ == '__main__':
    unittest.main()
