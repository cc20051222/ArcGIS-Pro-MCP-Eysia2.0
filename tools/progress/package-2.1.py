"""Build a fresh 2.1 release bundle from isolated first-party build outputs."""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
import shutil
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
DLLS = ['Compatibility', 'Configuration', 'Core', 'Logging', 'Protocol', 'Security', 'Server', 'Tools']


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--build-root', required=True, type=Path)
    p.add_argument('--destination', required=True, type=Path)
    p.add_argument('--refresh-docs', action='store_true')
    p.add_argument('--refresh-payload', action='store_true', help='Rebuild an unpublished staging folder after correcting packaging')
    a = p.parse_args()
    dest = a.destination.resolve()
    if dest.drive.upper() != 'D:' or not dest.is_relative_to(ROOT / '配套交付'):
        raise ValueError('Fresh D-drive companion destination required')
    for parent in (dest, *dest.parents):
        if parent.exists() and (parent.is_symlink() or parent.is_junction()):
            raise ValueError('Reparse path refused')
    if dest.exists() and not (a.refresh_docs or a.refresh_payload):
        raise ValueError('Do not overwrite an existing release staging folder')
    dest.mkdir(parents=True, exist_ok=True)

    def copy(source, target):
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)

    if not a.refresh_docs:
        output = a.build_root / 'net8/bin/ArcGISProMCP.Compatibility/Debug/net8.0-windows'
        plugin = dest / '04_Plugin/ArcGISProMCP.Compatibility.esriAddInX'
        plugin.parent.mkdir(parents=True, exist_ok=True)
        config_path = ROOT / 'Source/ArcGISProMCP.Compatibility/Config.daml'
        config = ET.parse(config_path)
        addin = next(e for e in config.getroot() if e.tag.endswith('AddInInfo'))
        assert addin.attrib['version'] == '2.1.0'
        # Preserve the established unprefixed DAML root used by Pro discovery.
        # Parse to validate, then change only the selected payload's host version.
        config_bytes = config_path.read_bytes()
        assert config_bytes.count(b'desktopVersion="3.0"') == 1
        config_bytes = config_bytes.replace(b'desktopVersion="3.0"', b'desktopVersion="3.5"', 1)
        assert b'<ArcGIS ' in config_bytes and b'<ns0:' not in config_bytes
        with zipfile.ZipFile(plugin, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
            z.writestr('Config.daml', config_bytes)
            for image in sorted((ROOT / 'Source/ArcGISProMCP.Compatibility/Images').rglob('*')):
                if image.is_file():
                    z.write(image, 'Images/' + image.relative_to(ROOT / 'Source/ArcGISProMCP.Compatibility/Images').as_posix())
            for name in DLLS:
                source = output / ('ArcGISProMCP.' + name + '.dll')
                assert source.is_file()
                z.write(source, 'Install/' + source.name)
            deps = output / 'ArcGISProMCP.Compatibility.deps.json'
            assert json.loads(deps.read_text())['runtimeTarget']['name'].startswith('.NETCoreApp,Version=v8.0')
            z.write(deps, 'Install/' + deps.name)
            z.write(ROOT / 'Source/ArcGISProMCP.PythonBridge/bridge_runner.py', 'Install/PythonBridge/bridge_runner.py')
        with zipfile.ZipFile(plugin) as z:
            assert z.testzip() is None and len(z.namelist()) == len(set(z.namelist()))
            assert len([n for n in z.namelist() if n.endswith('.dll')]) == 8
            assert not any(Path(n).name.lower().startswith(('esri.', 'arcgis.')) for n in z.namelist())
        skill_files = list(f for f in (ROOT / 'Skills/arcgis-pro-2-workflows').rglob('*') if f.is_file())
        assert len(skill_files) == 20
        for source in skill_files:
            copy(source, dest / '02_Skill' / source.relative_to(ROOT / 'Skills/arcgis-pro-2-workflows'))
        panel = a.build_root / 'bin/ArcGISProMCP.ProgressMonitor/Debug/net8.0-windows'
        for source in panel.iterdir():
            if source.is_file() and source.suffix in {'.dll', '.exe', '.json'}:
                copy(source, dest / '03_Panel' / source.name)
        for source, target in [('install-companion.ps1', '安装配套.ps1'), ('start-companion.ps1', '启动配套.ps1'), ('setup-2.1.ps1', '安装向导.ps1')]:
            text = (ROOT / 'tools/progress' / source).read_text(encoding='utf-8-sig')
            (dest / target).write_text(text, encoding='utf-8-sig', newline='\r\n')
        (dest / '安装2.1.cmd').write_text('@echo off\r\nchcp 65001 >nul\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0安装向导.ps1"\r\nif errorlevel 1 echo Installation stopped. Read 05_说明 for recovery.\r\npause\r\n', encoding='utf-8')
        for name in ('LICENSE', 'NOTICE.md'):
            copy(ROOT / name, dest / name)
        copy(ROOT / 'Config/client-catalog.json', dest / '06_连接参考/client-catalog.json')
    for name in ('SKILL_INSTALL_GUIDE.md', 'SKILL_WORKFLOW_GUIDE.md', 'RELEASE_2_1.md', 'RELEASE_2_1_VALIDATION.json'):
        source = ROOT / 'Docs' / name
        if source.exists():
            copy(source, dest / '05_说明' / name)
    (dest / '先读我.md').write_text('''# ArcGIS Pro MCP 2.1

产品版本2.1.0，Skill0.7.7；本包安装目标为Windows x64、ArcGIS Pro3.5/net8。
核对外层ZIP的.sha256后完整解压到D盘，保存并正常关闭Pro，再双击安装2.1.cmd。
先查看安装计划，再确认安装；首次用户无需先装r29。需要.NET8桌面运行时与本机有效Pro许可，Skill脚本需Python3.10+。不自动安装依赖、不修改客户端配置。

安装、升级、回滚与Skill加载详见05_说明/SKILL_INSTALL_GUIDE.md，功能使用详见SKILL_WORKFLOW_GUIDE.md。
通过启动配套.ps1启动已有D盘工程，在Pro的MCP页签启动服务器并打开任务面板。
客户端使用标准MCP地址http://127.0.0.1:6520/mcp；Agent须向选定运行根的task-record.json发布实际业务步骤。
C盘仅更新本插件GUID的AddIns/AssemblyCache目录，运行/备份/日志/实验数据在D盘。安装完成仍需客户端重新加载Skill。

本机验收与验证边界见05_说明/RELEASE_2_1_VALIDATION.json；239工具注册不等于239项业务全部验收。
保留旧版发布，按事务回执可回滚已知且未被后续改写的文件。无可信发布者签名，勿关闭组织安全策略。
AGPL-3.0；不分发Esri SDK，不包含用户工程、试题、个人资料或密钥。
''', encoding='utf-8')
    plugin = dest / '04_Plugin/ArcGISProMCP.Compatibility.esriAddInX'
    entries = [{'path': f.relative_to(dest).as_posix(), 'bytes': f.stat().st_size, 'sha256': sha(f)}
               for f in sorted(dest.rglob('*')) if f.is_file() and f.name != 'companion-manifest.json']
    manifest = {'kind': 'ARCGIS_PRO_MCP_2_1_RELEASE', 'releaseVersion': '2.1.0', 'tag': 'v2.1',
        'skillVersion': '0.7.7', 'at': dt.datetime.now().astimezone().isoformat(),
        'plugin': {'sha256': sha(plugin), 'bytes': plugin.stat().st_size, 'pro': '3.5', 'framework': 'net8.0-windows'},
        'signing': 'No trusted publisher signature', 'files': entries}
    (dest / 'companion-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'staged': str(dest), 'files': len(entries), 'plugin': manifest['plugin']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
