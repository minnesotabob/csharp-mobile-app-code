import copy
import json
import tempfile
import unittest
from pathlib import Path
from report import InvalidEvidence, build_report, load_plan, load_suite

METHOD = 'GnazzosWorld.Api.Tests.Journey.Recover'


class ReportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.plan = {'schema': 1, 'automated': [{'id': 'A36-RECOVERY', 'suite': 'api', 'method': METHOD, 'cases': 1}], 'manual': [{'id': 'A36-DEVICE'}]}

    def xml(self, result='Pass', extra='', **attrs):
        counts = {'passed': str(int(result == 'Pass')), 'failed': str(int(result == 'Fail')), 'skipped': str(int(result == 'Skip')), 'not-run': str(int(result == 'NotRun')), 'total': '1', 'errors': '0'}
        counts.update(attrs)
        attr = ' '.join(f'{k}="{v}"' for k, v in counts.items())
        text = f'<assemblies schema-version="3"><assembly {attr}><collection><test name="PRIVATE_DISPLAY" type="GnazzosWorld.Api.Tests.Journey" method="Recover" result="{result}"><output>PRIVATE_STORY</output></test>{extra}</collection></assembly></assemblies>'
        path = self.root / 'results.xml'
        path.write_text(text)
        return path

    def report(self, suite=None, plan=None):
        suite = suite or load_suite(self.xml())
        return build_report(plan or self.plan, {'core': suite, 'api': suite}, 'a' * 40, '123')

    def test_projection_omits_raw_private_values_and_retains_pending(self):
        r = self.report()
        self.assertEqual('pass', r['automated_gate'])
        self.assertEqual('unassessed', r['release_readiness'])
        self.assertEqual('not_run', r['manual'][0]['status'])
        self.assertNotIn('PRIVATE', json.dumps(r))

    def test_failed_case_fails_gate(self):
        self.assertEqual('fail', self.report(load_suite(self.xml('Fail')))['automated_gate'])

    def test_skipped_case_fails_gate(self):
        self.assertEqual('fail', self.report(load_suite(self.xml('Skip')))['automated_gate'])

    def test_not_run_case_fails_gate(self):
        self.assertEqual('fail', self.report(load_suite(self.xml('NotRun')))['automated_gate'])

    def test_missing_selector_is_not_coverage(self):
        p = copy.deepcopy(self.plan); p['automated'][0]['method'] += '_renamed'
        self.assertEqual('not_satisfied', self.report(plan=p)['automated'][0]['status'])

    def test_theory_case_count_cannot_shrink_silently(self):
        p = copy.deepcopy(self.plan); p['automated'][0]['cases'] = 2
        self.assertEqual('fail', self.report(plan=p)['automated_gate'])

    def test_unmapped_failure_still_fails_gate(self):
        s = load_suite(self.xml()); s['clean'] = False
        self.assertEqual('fail', self.report(s)['automated_gate'])

    def test_environment_error_is_rejected(self):
        with self.assertRaises(InvalidEvidence): load_suite(self.xml(errors='1'))

    def test_summary_disagreement_is_rejected(self):
        with self.assertRaises(InvalidEvidence): load_suite(self.xml(total='2'))

    def test_duplicate_display_identity_is_rejected(self):
        extra = '<test name="PRIVATE_DISPLAY" type="GnazzosWorld.Api.Tests.Journey" method="Recover" result="Pass"/>'
        with self.assertRaises(InvalidEvidence): load_suite(self.xml(extra=extra, total='2', passed='2'))

    def test_dtd_is_rejected(self):
        p = self.xml(); p.write_text('<!DOCTYPE assemblies []>' + p.read_text())
        with self.assertRaises(InvalidEvidence): load_suite(p)

    def test_unsupported_schema_is_rejected(self):
        p = self.xml(); p.write_text(p.read_text().replace('schema-version="3"', 'schema-version="99"'))
        with self.assertRaises(InvalidEvidence): load_suite(p)

    def test_duplicate_plan_id_is_rejected(self):
        p = copy.deepcopy(self.plan); p['manual'][0]['id'] = 'A36-RECOVERY'
        path = self.root / 'plan.json'; path.write_text(json.dumps(p))
        with self.assertRaises(InvalidEvidence): load_plan(path)

    def test_manual_pass_field_is_rejected(self):
        p = copy.deepcopy(self.plan); p['manual'][0]['status'] = 'pass'
        path = self.root / 'plan.json'; path.write_text(json.dumps(p))
        with self.assertRaises(InvalidEvidence): load_plan(path)

    def test_bad_revision_is_rejected(self):
        with self.assertRaises(InvalidEvidence): build_report(self.plan, {}, 'main', '123')

    def test_existing_output_is_not_replaced(self):
        import report
        from unittest.mock import patch
        suite = self.xml(); plan = self.root / 'plan.json'; plan.write_text(json.dumps(self.plan))
        output = self.root / 'report.json'; output.write_text('marker')
        with patch('sys.argv', ['report.py', '--plan', str(plan), '--core', str(suite), '--api', str(suite), '--revision', 'a'*40, '--run-id', '123', '--output', str(output)]):
            self.assertEqual(2, report.main())
        self.assertEqual('marker', output.read_text())


if __name__ == '__main__':
    unittest.main()
