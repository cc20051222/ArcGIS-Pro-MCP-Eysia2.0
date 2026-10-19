"""Prepare a D-only candidate, immutable rollback copy and activation gate.

Does not install, register, edit registry/config, touch AssemblyCache or start Pro.
Run with python -B. Package metadata is a local test candidate, never a release.
"""
import argparse
import datetime as dt
import hashlib
import json
import pathlib
import uuid
import zipfile
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[2]
RUNTIME = ROOT / '.runtime' / 'progress-panel'
BUILD = RUNTIME / 'net8/bin/ArcGISProMCP.Compatibility/Debug/net8.0-windows'
GUID = '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'
NS = '{http://schemas.esri.com/DADF/Registry}'

def sha(data):
    return hashlib.sha256(data).hexdigest().upper()

def d_only(path):
    resolved = path.resolve()
    if resolved.drive.upper() != 'D:':
        raise ValueError(f'D-drive output required: {path}')
    for item in (path, *path.parents):
        if item.exists() and (item.is_symlink() or item.is_junction()):
            raise ValueError(f'Reparse path not accepted for output: {item}')
    return resolved

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--installed-package', required=True)
    parser.add_argument('--folder-probe', required=True)
    parser.add_argument('--assembly-info', required=True)
    args = parser.parse_args()
    # All validation before making a candidate; input files are read only.
    d_only(RUNTIME)
    installed = pathlib.Path(args.installed_package)
    original = installed.read_bytes()
    folder_probe = json.loads(pathlib.Path(args.folder_probe).read_text(encoding='utf-8-sig'))
    assembly = json.loads(pathlib.Path(args.assembly_info).read_text(encoding='utf-8-sig'))
    if assembly['targetFramework'] != '.NETCoreApp,Version=v8.0':
        raise ValueError('Compatibility must target net8 for this Pro 3.5 candidate')
    sdk = [x for x in assembly['references'] if x['name'].startswith('ArcGIS.')]
    if not sdk or any(not x['version'].startswith('13.5.') for x in sdk):
        raise ValueError('Candidate has a mismatched ArcGIS SDK reference set')
    deps = json.loads((BUILD / 'ArcGISProMCP.Compatibility.deps.json').read_text(encoding='utf-8-sig'))
    if deps['runtimeTarget']['name'] != '.NETCoreApp,Version=v8.0':
        raise ValueError('Candidate deps target mismatch')
    files = {}
    for library in deps['targets'][deps['runtimeTarget']['name']].values():
        for name in library.get('runtime', {}):
            if not name.startswith('ArcGISProMCP.') or not name.endswith('.dll'):
                raise ValueError(f'Unreviewed runtime dependency: {name}')
            source = BUILD / name
            if not source.is_file():
                raise ValueError(f'Missing runtime dependency: {name}')
            files['Install/' + name] = source.read_bytes()
            pdb = source.with_suffix('.pdb')
            if pdb.is_file():
                files['Install/' + pdb.name] = pdb.read_bytes()
    if len([n for n in files if n.endswith('.dll')]) != 8:
        raise ValueError('Expected exactly eight first-party runtime assemblies')
    files['Install/ArcGISProMCP.Compatibility.deps.json'] = (BUILD / 'ArcGISProMCP.Compatibility.deps.json').read_bytes()
    files['Install/PythonBridge/bridge_runner.py'] = (ROOT / 'Source/ArcGISProMCP.PythonBridge/bridge_runner.py').read_bytes()
    config = ET.fromstring((ROOT / 'Source/ArcGISProMCP.Compatibility/Config.daml').read_bytes())
    info = config.find(NS + 'AddInInfo')
    if info is None or info.get('id') != GUID or info.get('version') != '1.0.2':
        raise ValueError('Unexpected Add-in identity')
    info.set('desktopVersion', '3.5')
    info.find(NS + 'Date').text = dt.datetime.now().strftime('%Y/%m/%d')
    if not any(e.get('id') == 'ArcGISProMCP_ProgressPanel' for e in config.iter()):
        raise ValueError('Progress-panel DAML button missing')
    ET.register_namespace('', NS[1:-1])
    ET.register_namespace('xsi', 'http://www.w3.org/2001/XMLSchema-instance')
    files['Config.daml'] = ET.tostring(config, encoding='utf-8', xml_declaration=True)
    files['Images/AddInIcon.png'] = (ROOT / 'Source/ArcGISProMCP.Compatibility/Images/AddInIcon.png').read_bytes()
    stamp = dt.datetime.now().strftime('%Y%m%d-%H%M%S')
    output = d_only(RUNTIME / 'activation' / f'{stamp}-{uuid.uuid4().hex[:8]}')
    output.mkdir(parents=True, exist_ok=False)
    probe = output / '.write-probe'
    with probe.open('xb') as stream:
        stream.write(b'D-only write probe')
    if probe.read_bytes() != b'D-only write probe':
        raise OSError('Write probe failed')
    probe.unlink()
    backup = output / 'original-installed.esriAddInX'
    with backup.open('xb') as stream:
        stream.write(original)
    package = output / 'ArcGISProMCP.Compatibility.esriAddInX'
    with zipfile.ZipFile(package, 'x', zipfile.ZIP_DEFLATED) as archive:
        for name, body in sorted(files.items()):
            archive.writestr(name, body)
    with zipfile.ZipFile(package) as archive:
        if len(archive.namelist()) != len(files) or archive.testzip() is not None:
            raise ValueError('Archive inventory/CRC verification failed')
        for name, body in files.items():
            if sha(archive.read(name)) != sha(body):
                raise ValueError('Archive payload/source mismatch: ' + name)
    if installed.read_bytes() != original:
        raise ValueError('Installation changed concurrently; stop before activation')
    cache_path = pathlib.Path(folder_probe['after']) / 'ESRI/ArcGISPro/AssemblyCache' / GUID
    reasons = []
    if installed.drive.upper() != 'D:':
        reasons.append('Installed Add-in path resolves to C; replacement violates current C-write prohibition.')
    if cache_path.drive.upper() != 'D:':
        reasons.append('Pro 3.5 computes its AssemblyCache from Windows LocalApplicationData on C.')
    if folder_probe['redirected']:
        reasons.append('An environment probe alone is not proof of actual Pro module path after startup.')
    else:
        reasons.append('Child-process LOCALAPPDATA override did not redirect Windows known-folder resolution.')
    receipt = {
        'schema': 'progress-activation-preflight-v1',
        'observedAt': dt.datetime.now().astimezone().isoformat(),
        'status': 'BLOCKED',
        'authorization': 'User requested new-plugin activation and live GP verification; C-write prohibition retained.',
        'candidate': {'path': str(package), 'sha256': sha(package.read_bytes()), 'bytes': package.stat().st_size,
                      'entryCount': len(files), 'desktopVersion': '3.5', 'framework': 'net8.0-windows',
                      'sdkReferences': sdk, 'release': False},
        'rollback': {'path': str(backup), 'originalPath': str(installed), 'sha256': sha(original), 'bytes': len(original)},
        'predictedCachePath': str(cache_path),
        'folderProbe': folder_probe,
        'blockReasons': reasons,
        'effects': {'installed': False, 'registryWritten': False, 'cacheWritten': False,
                    'proRestarted': False, 'gisExecuted': False},
        'inventory': [{'path': n, 'bytes': len(b), 'sha256': sha(b)} for n, b in sorted(files.items())],
    }
    with (output / 'preflight.json').open('x', encoding='utf-8') as stream:
        json.dump(receipt, stream, ensure_ascii=False, indent=2)
    print(json.dumps({'status': receipt['status'], 'receipt': str(output / 'preflight.json'),
                      'candidate': receipt['candidate'], 'blockReasons': reasons}, ensure_ascii=False, indent=2))

if __name__ == '__main__':
    main()
