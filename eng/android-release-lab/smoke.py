"""Install/upgrade only the disposable package on one fresh emulator; no real device/store."""
import argparse
import hashlib
import json
import re
import subprocess
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from audit import PACKAGE, PROBE, require


def adb(*args, timeout=45):
    result = subprocess.run(['adb', '-e', *args], capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=timeout)
    require(result.returncode == 0, 'adb_command')
    return result.stdout + result.stderr


def wait_ui(predicate):
    until = time.monotonic() + 90
    while time.monotonic() < until:
        try:
            adb('shell', 'uiautomator', 'dump', '/sdcard/gnazzo-release-window.xml', timeout=15)
            root = ET.fromstring(adb('exec-out', 'cat', '/sdcard/gnazzo-release-window.xml'))
            if predicate(list(root.iter('node'))):
                return
        except (ValueError, ET.ParseError, subprocess.TimeoutExpired):
            pass
        time.sleep(1)
    raise ValueError('expected_native_ui_not_observed')


def version(expected):
    text = adb('shell', 'dumpsys', 'package', PACKAGE)
    code = re.search(r'\bversionCode=(\d+)', text)
    uid = re.search(r'\b(?:userId|appId)=(\d+)', text)
    require(code and int(code[1]) == expected and uid, 'installed_identity_version')
    return int(uid[1])


def launch():
    adb('shell', 'am', 'force-stop', PACKAGE)
    result = adb('shell', 'am', 'start', '-W', '-a', 'android.intent.action.MAIN', '-c', 'android.intent.category.LAUNCHER', '-p', PACKAGE)
    require('Status: ok' in result, 'launcher')
    wait_ui(lambda nodes: any(n.get('package') == PACKAGE and n.get('text') == "Gnazzo's World" for n in nodes))


def probe(action, expected):
    adb('shell', 'am', 'start', '-W', '-n', PACKAGE + '/' + PROBE, '--es', 'probe', action)
    wait_ui(lambda nodes: any(n.get('package') == PACKAGE and n.get('content-desc') == 'release-probe-' + expected for n in nodes))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', required=True)
    args = parser.parse_args(); root = Path(args.directory)
    evidence = json.loads((root / 'package-evidence.json').read_text())
    require(evidence['profile'] == 'disposable-testOnly-Release', 'profile')
    require(adb('shell', 'getprop', 'ro.kernel.qemu').strip() == '1', 'emulator_only')
    # pm path exits nonzero for an absent package; list packages returns an empty successful result.
    require(not adb('shell', 'pm', 'list', 'packages', PACKAGE).strip(), 'fresh_package_required')
    abi = adb('shell', 'getprop', 'ro.product.cpu.abi').strip()
    sdk = int(adb('shell', 'getprop', 'ro.build.version.sdk').strip())
    page = int(adb('shell', 'getconf', 'PAGESIZE').strip())
    require(abi == 'x86_64' and sdk >= 26, 'emulator_configuration')
    for file in ('v1.apk', 'v2.apk'):
        row = next(x for x in evidence['packages'] if x['file'] == file)
        require(hashlib.sha256((root / file).read_bytes()).hexdigest() == row['sha256'], 'artifact_hash')
    installed = False
    try:
        require('Success' in adb('install', '-t', '--no-streaming', str(root / 'v1.apk'), timeout=180), 'install_v1')
        installed = True; first_uid = version(1); launch(); probe('seed', 'seeded')
        adb('shell', 'am', 'force-stop', PACKAGE)
        require('Success' in adb('install', '-r', '-t', '--no-streaming', str(root / 'v2.apk'), timeout=180), 'upgrade_v2')
        require(version(2) == first_uid, 'package_uid_changed')
        launch(); probe('verify', 'verified')
        report = {'schema': 1, 'revision': evidence['revision'], 'runId': evidence['runId'], 'package': PACKAGE,
                  'deviceClass': 'GitHub-hosted Android emulator', 'model': adb('shell', 'getprop', 'ro.product.model').strip(),
                  'apiLevel': sdk, 'abi': abi, 'pageSizeBytes': page, 'installedVersion': 1, 'upgradedVersion': 2,
                  'sameUid': True, 'homeLaunchV1': True, 'homeLaunchV2': True, 'syntheticDraftSeeded': True,
                  'syntheticDraftSurvivedUpgrade': True, 'sourcePackageHashesVerified': True,
                  'scope': 'Actual isolated testOnly Release APK install/update and native launcher/probe; not Play distribution, production data migration or full native acceptance.',
                  'physicalDevice': 'not_run', 'playTrack': 'not_run', 'runtime16k': 'observed' if page == 16384 else 'not_run'}
        with (root / 'upgrade-evidence.json').open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2); stream.write('\n')
        print('Emulator Release smoke passed: installed v1, launched Home, saved synthetic draft, updated to v2 with same UID, relaunched and verified preserved draft.')
    finally:
        if installed:
            adb('uninstall', PACKAGE)
        adb('shell', 'rm', '-f', '/sdcard/gnazzo-release-window.xml')


if __name__ == '__main__':
    main()

