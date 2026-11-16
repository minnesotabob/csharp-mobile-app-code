import struct
import unittest
from audit import PACKAGE, PROBE, apk_signer, badging, elf_alignment, manifest_check


class AuditTests(unittest.TestCase):
    def test_numbered_and_scheme_certificate_labels(self):
        digest = 'a' * 64
        for label in ('Signer #1', 'V3.0 Signer:', 'V3.1 Signer:'):
            with self.subTest(label=label):
                apk_signer('Number of signers: 1\n' + label +
                           ' certificate SHA-256 digest: ' + digest + '\n', digest)
        apk_signer('Number of signers: 1\nV2 Signer: certificate SHA-256 digest: ' +
                   digest + '\nV3.0 Signer: certificate SHA-256 digest: ' + digest, digest)

    def test_missing_foreign_or_multiple_signers_do_not_pass(self):
        digest = 'a' * 64
        good = 'Number of signers: 1\nV3.0 Signer: certificate SHA-256 digest: ' + digest
        for changed in (good.replace('signers: 1', 'signers: 2'),
                        good.replace(digest, 'b' * 64),
                        good.replace('certificate SHA-256', 'public key SHA-256'),
                        good + '\nV2 Signer: certificate SHA-256 digest: ' + 'b' * 64,
                        good.replace('Number of signers: 1\n', '')):
            with self.subTest(changed=changed), self.assertRaises(ValueError):
                apk_signer(changed, digest)

    def elf(self, align=16384, offset=0, virtual=0):
        data = bytearray(120); data[:6] = b'\x7fELF\x02\x01'
        struct.pack_into('<Q', data, 32, 64); struct.pack_into('<HH', data, 54, 56, 1)
        struct.pack_into('<I', data, 64, 1); struct.pack_into('<QQ', data, 72, offset, virtual); struct.pack_into('<Q', data, 112, align)
        return data

    def manifest(self):
        return f'<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="{PACKAGE}" android:versionCode="2"><uses-sdk android:minSdkVersion="26" android:targetSdkVersion="36"/><application android:testOnly="true" android:allowBackup="false"><activity android:name="{PROBE}" android:exported="true"/></application></manifest>'

    def test_valid_elf_load_alignment(self):
        self.assertEqual(1, elf_alignment(self.elf()))

    def test_4k_alignment_is_rejected(self):
        with self.assertRaises(ValueError): elf_alignment(self.elf(4096))

    def test_offset_congruence_is_required(self):
        with self.assertRaises(ValueError): elf_alignment(self.elf(offset=4096))

    def test_invalid_or_truncated_headers_are_rejected(self):
        for data in (b'not elf', self.elf()[:80], bytes(120)):
            with self.subTest(size=len(data)), self.assertRaises(ValueError): elf_alignment(data)

    def test_no_load_segment_is_not_a_pass(self):
        data = self.elf(); struct.pack_into('<I', data, 64, 0)
        with self.assertRaises(ValueError): elf_alignment(data)

    def test_expected_package_and_sdk_are_required(self):
        source = f"package: name='{PACKAGE}' versionCode='1' versionName='0.1.0-lab.1'\nsdkVersion:'26'\ntargetSdkVersion:'36'"
        self.assertEqual(1, badging(source, 1)['versionCode'])
        for changed in (source.replace(PACKAGE, 'com.other.app'), source.replace("targetSdkVersion:'36'", "targetSdkVersion:'35'"), source + '\napplication-debuggable'):
            with self.subTest(changed=changed), self.assertRaises(ValueError): badging(changed, 1)
        with self.assertRaises(ValueError): badging(source, 2)

    def test_package_field_order_and_extra_build_fields(self):
        source = (f"package: name='{PACKAGE}' versionCode='2' "
                  "versionCodeMajor='0' versionName='0.1.0-lab.2' "
                  "compileSdkVersion='37'\nsdkVersion:'26'\ntargetSdkVersion:'37'")
        self.assertEqual(2, badging(source, 2)['versionCode'])
        reordered = (f"package: versionName='0.1.0-lab.2' "
                     f"name='{PACKAGE}' versionCode='2'\n"
                     "sdkVersion:'26'\ntargetSdkVersion:'37'")
        self.assertEqual(badging(source, 2), badging(reordered, 2))

    def test_observed_min_sdk_label_and_ambiguous_minimum(self):
        source = (f"package: name='{PACKAGE}' versionCode='1' "
                  "versionName='0.1.0-lab.1' platformBuildVersionCode='37'\n"
                  "minSdkVersion:'26'\ntargetSdkVersion:'37'")
        self.assertEqual(26, badging(source, 1)['minSdk'])
        self.assertEqual(badging(source, 1),
                         badging(source.replace('minSdkVersion', 'sdkVersion'), 1))
        for changed in (source.replace("minSdkVersion:'26'", "minSdkVersion:'25'"),
                        source + "\nsdkVersion:'26'",
                        source.replace("minSdkVersion:'26'", '')):
            with self.subTest(changed=changed), self.assertRaises(ValueError):
                badging(changed, 1)

    def test_disposable_bundle_contract(self):
        self.assertEqual(2, manifest_check(self.manifest(), 2)['versionCode'])

    def test_missing_test_only_debuggable_or_foreign_probe_is_rejected(self):
        source = self.manifest()
        for changed in (source.replace('android:testOnly="true"', ''), source.replace('<application ', '<application android:debuggable="true" '), source.replace(PROBE, PACKAGE + '.Other')):
            with self.subTest(changed=changed), self.assertRaises(ValueError): manifest_check(changed, 2)


if __name__ == '__main__':
    unittest.main()

