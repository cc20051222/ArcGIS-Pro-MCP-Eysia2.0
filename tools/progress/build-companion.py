"""Stage an explicitly labelled companion candidate; never changes sealed Release."""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / '配套交付/ArcGIS-Pro-MCP-2.0-Skill与面板-0.7.1候选'


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest().upper()


def main():
    p=argparse.ArgumentParser();p.add_argument('--candidate',required=True,type=Path);a=p.parse_args()
    if DEST.resolve().drive.upper()!='D:' or a.candidate.resolve().drive.upper()!='D:':raise ValueError('D only')
    for parent in (DEST,*DEST.parents):
        if parent.exists() and (parent.is_symlink() or parent.is_junction()):raise ValueError('Reparse refused')
    DEST.mkdir(parents=True,exist_ok=True)
    def copy(source,target):
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    original=ROOT/'Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip'
    copy(original,DEST/'01_原版r29'/original.name)
    copy(a.candidate,DEST/'04_Plugin/ArcGISProMCP.Compatibility.esriAddInX')
    for f in sorted((ROOT/'Skills/arcgis-pro-2-workflows').rglob('*')):
        if f.is_file():copy(f,DEST/'02_Skill'/f.relative_to(ROOT/'Skills/arcgis-pro-2-workflows'))
    monitor=ROOT/'.runtime/progress-panel/bin/ArcGISProMCP.ProgressMonitor/Debug/net8.0-windows'
    for f in monitor.iterdir():
        if f.is_file() and f.suffix in {'.exe','.dll','.json'}:copy(f,DEST/'03_Panel'/f.name)
    for source,name in [('install-companion.ps1','安装配套.ps1'),('start-companion.ps1','启动配套.ps1')]:
        data=(ROOT/'tools/progress'/source).read_text(encoding='utf-8-sig')
        (DEST/name).write_text(data,encoding='utf-8-sig')
    (DEST/'先读我.md').write_text('''# 2.0 Skill 与任务面板 0.7.1 配套候选

现有 r29 原样副本在 01_原版r29；它是原版安装入口，不包含本次新面板。先安装原版并按其教程连接 MCP，再使用本目录的配套安装脚本更新 Skill/面板/插件。不要在新版配套之后再次运行原版部署，否则插件会被降回原版。候选未获本批独立验收，不能冒称新正式 r29。

本次插件更新仅适用于 ArcGIS Pro 3.5/net8，本机实测；其它 Pro 版本保持原版，不强装本插件。独立面板需 .NET 8 Windows Desktop Runtime；缺依赖请按组织软件规范安装，本配套不会下载依赖。Skill 是指令和本地发布脚本，客户端须支持项目 Skill 与本地 Python 3.10+；仅远程 MCP 不会自动获得面板同步。

在 PowerShell 中运行（将路径换为自己的 D 盘目录）：

    & '.\\安装配套.ps1' -Action Plan -WorkRoot 'D:\\MCP运行记录' -AgentWorkspace 'D:\\你的工作区'
    & '.\\安装配套.ps1' -Action InstallAll -WorkRoot 'D:\\MCP运行记录' -AgentWorkspace 'D:\\你的工作区' -UpdateExisting -AllowOnlyThisAddinOnC
    & '.\\启动配套.ps1' -Mode Pro -WorkRoot 'D:\\MCP运行记录' -Project 'D:\\工程\\你的工程.aprx'

保存并正常关闭 Pro 后再安装；脚本检测 remaining=0，不强杀进程。InstallAll 将 Skill 放在工作区 .agents/skills/arcgis-pro-2-workflows，面板放 WorkRoot/panel，只更新本插件 GUID 的 AddIns/AssemblyCache 两处 C 盘目录；其它工程/备份/日志/临时文件在 D。脚本不写真实客户端配置。打开客户端工作区并重新加载 Skill，在 Pro MCP 页签启动服务器并打开任务面板。

独立窗口：启动配套.ps1 -Mode Panel -WorkRoot 'D:\\MCP运行记录'。Agent 必须将任务发布到 WorkRoot/task-record.json；Pro 与独立窗口使用同一任务文件和 events 目录。安装文件不等于 AI 已自动加载或实际 GIS 任务验收通过。

各项亦可单独安装：InstallSkill（需 AgentWorkspace）、InstallPanel、InstallPlugin。默认拒绝覆盖不同文件；明确 -UpdateExisting 后先备份。每次安装给出 D 盘 transactions/.../receipt.json；失败停止，先检查此记录，不盲重试。回滚：保存并正常关闭 Pro，运行安装配套.ps1 -Action Restore -WorkRoot 原运行目录 -Receipt 原安装记录 -AllowOnlyThisAddinOnC；仅恢复本事务已登记且仍匹配安装后哈希的文件，用户后续修改会阻止恢复。未知文件保留。部分失败的事务须先对账，不能保证直接自动恢复。

详细安装教程与功能原理见 05_说明 中两份 Word。最后交付逐件列出文件绝对位置、格式及检查状态。水文目录已有部分受控候选；须逐项核schema、许可和实际执行，不能将目录列出当成真实DEM整链通过。缺工具先评估等效替代，无法替代则请求用户同意开发。
''',encoding='utf-8')
    files=[{'path':f.relative_to(DEST).as_posix(),'bytes':f.stat().st_size,'sha256':sha(f)} for f in sorted(DEST.rglob('*')) if f.is_file() and f.name!='companion-manifest.json']
    manifest={'kind':'COMPANION_071_CANDIDATE','at':dt.datetime.now().astimezone().isoformat(),'skillVersion':'0.7.1',
              'originalR29':{'sha256':sha(original),'bytes':original.stat().st_size,'modified':False},
              'candidatePlugin':{'sha256':sha(a.candidate),'bytes':a.candidate.stat().st_size,'pro':'3.5','framework':'net8.0-windows'},
              'status':'PASS CANDIDATE; independent acceptance pending; not a signed release', 'files':files}
    (DEST/'companion-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'directory':str(DEST),'files':len(files),'candidateSha256':sha(a.candidate)},ensure_ascii=False))


if __name__=='__main__':main()
