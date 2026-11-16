"""Audit disposable signed packages, never Play eligibility or production authority."""
import argparse
import base64
import hashlib
import json
import re
import struct
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

PACKAGE = 'com.johngnazzo.gnazzosworld.releaselab'
PROBE = PACKAGE + '.ReleaseProbeActivity'
ANDROID = '{http://schemas.android.com/apk/res/android}'


def require(value, code):
    if not value:
        raise ValueError(code)


def run(*args):
    result = subprocess.run([str(x) for x in args], capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=180)
    require(result.returncode == 0, 'tool_failed')
    return result.stdout + result.stderr


def badging(text, version):
    match = re.search(r"package: name='([^']+)' versionCode='([0-9]+)' versionName='([^']+)'", text)
    low = re.search(r"sdkVersion:'([0-9]+)'", text)
    target = re.search(r"targetSdkVersion:'([0-9]+)'", text)
    require(match and low and target, 'package_metadata')
    require(match[1] == PACKAGE and int(match[2]) == version, 'package_identity_version')
    require(int(low[1]) == 26 and int(target[1]) >= 36, 'sdk_levels')
    require('application-debuggable' not in text, 'debuggable')
    return {'package': match[1], 'versionCode': int(match[2]), 'versionName': match[3], 'minSdk': int(low[1]), 'targetSdk': int(target[1])}


def apk_signer(text, expected):
    # Build tools label certificate rows by signer number or signature scheme.
    counts = re.findall(r'^Number of signers: (\d+)\s*$', text, re.M)
    certs = re.findall(
        r'^(?:Signer #\d+|V\d+(?:\.\d+)? Signer):? '
        r'certificate SHA-256 digest: ([a-fA-F0-9]{64})\s*$', text, re.M)
    require(counts == ['1'] and certs and
            all(value.lower() == expected for value in certs), 'apk_signer')


def manifest_check(text, version):
    root = ET.fromstring(text)
    app = root.find('application')
    sdk = root.find('uses-sdk')
    require(root.get('package') == PACKAGE and root.get(ANDROID + 'versionCode') == str(version), 'bundle_identity_version')
    require(app is not None and app.get(ANDROID + 'testOnly') == 'true' and app.get(ANDROID + 'debuggable', 'false') == 'false', 'test_only_release')
    require(app.get(ANDROID + 'allowBackup') == 'false', 'backup_policy')
    require(sdk is not None and int(sdk.get(ANDROID + 'minSdkVersion', '0')) == 26 and int(sdk.get(ANDROID + 'targetSdkVersion', '0')) >= 36, 'bundle_sdk')
    activities = [a for a in app.findall('activity') if a.get(ANDROID + 'name') == PROBE]
    require(len(activities) == 1 and activities[0].get(ANDROID + 'exported') == 'true', 'lab_probe')
    return {'package': PACKAGE, 'versionCode': version, 'minSdk': 26, 'targetSdk': int(sdk.get(ANDROID + 'targetSdkVersion'))}


def elf_alignment(data):
    require(len(data) >= 64 and data[:6] == b'\x7fELF\x02\x01', 'elf64_little_endian')
    offset = struct.unpack_from('<Q', data, 32)[0]
    entry_size, count = struct.unpack_from('<HH', data, 54)
    require(entry_size >= 56 and 0 < count <= 1024 and offset + entry_size * count <= len(data), 'elf_program_headers')
    segments = 0
    for n in range(count):
        at = offset + n * entry_size
        if struct.unpack_from('<I', data, at)[0] != 1:
            continue
        file_offset, virtual = struct.unpack_from('<QQ', data, at + 8)
        align = struct.unpack_from('<Q', data, at + 48)[0]
        require(align >= 16384 and (file_offset - virtual) % 16384 == 0, 'elf_16k_load_alignment')
        segments += 1
    require(segments > 0, 'elf_no_load_segments')
    return segments


def libraries(path, expected):
    result = []
    with zipfile.ZipFile(path) as archive:
        for info in archive.infolist():
            match = re.fullmatch(r'(?:base/)?lib/([^/]+)/([^/]+\.so)', info.filename)
            if not match:
                continue
            require(match[1] in ('x86_64', 'arm64-v8a') and info.file_size <= 100 * 1024 * 1024, 'native_library_bound')
            result.append({'abi': match[1], 'name': match[2], 'loadSegments': elf_alignment(archive.read(info)), 'elf16k': True})
    require(result and expected in {x['abi'] for x in result}, 'expected_abi')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('directory', 'sdk', 'java', 'bundletool', 'revision', 'run-id'):
        parser.add_argument('--' + name, required=True)
    args = parser.parse_args()
    require(re.fullmatch('[a-f0-9]{40}', args.revision) and re.fullmatch('[0-9]+', args.run_id), 'provenance_shape')
    root = Path(args.directory)
    signer = (root / 'expected-signer.txt').read_text().strip()
    require(re.fullmatch('[a-f0-9]{64}', signer), 'expected_signer')
    candidates = list((Path(args.sdk) / 'build-tools').glob('*'))
    tools = max((p for p in candidates if (p / 'aapt2.exe').is_file()), key=lambda p: tuple(int(x) for x in re.findall(r'\d+', p.name)))
    java = Path(args.java) / 'bin/java.exe'
    keytool = Path(args.java) / 'bin/keytool.exe'
    jarsigner = Path(args.java) / 'bin/jarsigner.exe'
    rows = []
    for file, version, abi in [('v1.apk', 1, 'x86_64'), ('v2.apk', 2, 'x86_64'), ('v2.aab', 2, 'arm64-v8a')]:
        path = root / file
        require(0 < path.stat().st_size <= 300 * 1024 * 1024, 'package_size')
        if path.suffix == '.apk':
            printed = run(tools / 'apksigner.bat', 'verify', '--verbose', '--print-certs', path)
            apk_signer(printed, signer)
            run(tools / 'zipalign.exe', '-c', '-P', '16', '4', path)
            metadata = badging(run(tools / 'aapt2.exe', 'dump', 'badging', path), version)
            tree = run(tools / 'aapt2.exe', 'dump', 'xmltree', path, '--file', 'AndroidManifest.xml')
            require(PROBE in tree and re.search(r'android:testOnly[^\n]*(0xffffffff|true)', tree), 'apk_test_only_probe')
        else:
            verified = run(jarsigner, '-J-Duser.language=en', '-verify', path)
            require('jar verified.' in verified and 'jar is unsigned' not in verified, 'bundle_signature')
            cert = run(keytool, '-J-Duser.language=en', '-printcert', '-rfc', '-jarfile', path)
            blocks = re.findall(r'-----BEGIN CERTIFICATE-----\s*(.*?)\s*-----END CERTIFICATE-----', cert, re.S)
            require(blocks and all(hashlib.sha256(base64.b64decode(b)).hexdigest() == signer for b in blocks), 'bundle_signer')
            run(java, '-jar', args.bundletool, 'validate', '--bundle=' + str(path))
            metadata = manifest_check(run(java, '-jar', args.bundletool, 'dump', 'manifest', '--bundle=' + str(path)), version)
        native = libraries(path, abi)
        rows.append({'file': file, 'bytes': path.stat().st_size, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                     'metadata': metadata, 'signerSha256': signer, 'signatureVerified': True, 'testOnly': True,
                     'nativeLibraries': native, 'zip16kChecked': path.suffix == '.apk'})
    evidence = {'schema': 1, 'revision': args.revision, 'runId': args.run_id, 'profile': 'disposable-testOnly-Release',
                'bundletool': '1.18.3', 'packages': rows, 'playSubmission': 'not_run', 'playSigning': 'not_run',
                'physicalDevice': 'not_run', 'runtime16k': 'not_run'}
    with (root / 'package-evidence.json').open('x', encoding='utf-8') as stream:
        json.dump(evidence, stream, indent=2); stream.write('\n')
    print('Signed lab packages verified: identity, versions, certificates, bundle structure and 16 KiB ELF/APK alignment. Play/runtime/device claims remain separate.')


if __name__ == '__main__':
    main()

