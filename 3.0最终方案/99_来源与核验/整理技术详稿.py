from pathlib import Path
import hashlib, json, re, os

ROOT = Path(r'D:\ArcGIS-Pro-MCP 2.0')
SOURCE = ROOT / '同步' / '3.0项目规划创新'
OUT = ROOT / '3.0最终方案'
ANNEX = OUT / '02_技术详稿'
EVIDENCE = OUT / '99_来源与核验'

# This was the initial migration tool. Current technical copies contain
# authorized revisions and must not be replaced with historical source copies.
existing_provenance = EVIDENCE / '技术详稿来源与链接迁移.json'
if existing_provenance.exists():
    prior = json.loads(existing_provenance.read_text(encoding='utf-8-sig'))
    if prior.get('currentRevisions'):
        raise RuntimeError('Current final-plan revisions exist; initial migration would overwrite them')

def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest().upper()

groups = ['01_主方案（6份）', '02_模块实现详稿（5份）', '03_支持与用户体验详稿（4份）']
files = [p for group in groups for p in sorted((SOURCE / group).glob('*.md'))]
assert len(files) == 15
mapping = {p.resolve(): ANNEX / p.relative_to(SOURCE) for p in files}
mapping[(SOURCE / '创新执行规格.json').resolve()] = ANNEX / '原始创新执行规格.json'
mapping[(SOURCE / '规划核验清单.json').resolve()] = ANNEX / '原始规划核验清单.json'
ANNEX.mkdir(parents=True, exist_ok=True)
records, links, missing = [], [], []
pattern = re.compile(r'(\]\()([^\)]+)(\))')
for src, dest in mapping.items():
    original = src.read_bytes()
    dest.parent.mkdir(parents=True, exist_ok=True)
    if src.suffix == '.md':
        body = original.decode('utf-8-sig')
        def link(match):
            value = match.group(2)
            if value.startswith(('https://', 'http://', '#', 'mailto:', 'codex://')):
                return match.group(0)
            raw = value.strip('<>')
            pathpart, sep, fragment = raw.partition('#')
            resolved = (src.parent / pathpart).resolve()
            exists = resolved.exists()
            target = mapping.get(resolved)
            # All original technical text is retained. Only link destinations move.
            replacement = os.path.relpath(target, dest.parent).replace('\\', '/') if target else str(resolved).replace('\\', '/')
            if sep:
                replacement += '#' + fragment
            links.append({'from': str(dest.relative_to(OUT)), 'original': value, 'target': replacement, 'exists': exists})
            if not exists:
                missing.append(links[-1])
            return match.group(1) + '<' + replacement + '>' + match.group(3)
        updated = pattern.sub(link, body)
        dest.write_text(updated, encoding='utf-8', newline='\n')
    else:
        dest.write_bytes(original)
    assert src.read_bytes() == original
    records.append({'originalPath': str(src), 'originalBytes': len(original), 'originalSha256': hashlib.sha256(original).hexdigest().upper(),
                    'finalPath': str(dest.relative_to(OUT)), 'finalBytes': dest.stat().st_size, 'finalSha256': digest(dest),
                    'change': 'local Markdown link destinations only' if src.suffix == '.md' else 'byte-identical archive'})

spec = json.loads((SOURCE / '创新执行规格.json').read_text(encoding='utf-8-sig'))
source_record = {'kind': 'FINAL_DOCUMENT_CONSOLIDATION_PROVENANCE', 'date': '2026-10-02', 'sourceRevision': spec['revision'],
                 'sourceRoot': str(SOURCE), 'productCodeChanges': 0, 'productBuildInstallLiveRuns': 0,
                 'originalsUnchanged': True, 'retainedTechnicalMarkdownFiles': 15, 'files': records,
                 'localLinkRewrites': links, 'missingOriginalLinkTargets': missing,
                 'scopeDecision': 'All prior technical scope retained; editorial consolidation is not product acceptance.'}
(EVIDENCE / '技术详稿来源与链接迁移.json').write_text(json.dumps(source_record, ensure_ascii=False, indent=2), encoding='utf-8')
state = {k: spec[k] for k in ['originalQualityBaseline', 'additionalOriginalScope', 'quantity', 'workItems', 'innovations', 'feasibilityPrototypes']}
state.update({'kind': 'FINAL_PRODUCT_PLAN_SCOPE', 'revision': 'FINAL-20261002', 'technicalSourceRevision': spec['revision'],
              'documentStatus': 'CONSOLIDATED_DOCUMENTS', 'productAcceptance': 'NOT_RUN', 'fullScope60DayFeasibility': 'UNKNOWN_UNTIL_MEASURED',
              'startDate': None, 'targetCalendarDays': 60, 'engineeringTargetDays': 56, 'contingencyDays': 4,
              'historicalSource': str(SOURCE), 'documentationChangesOnly': True})
(OUT / '最终方案范围与状态.json').write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding='utf-8')
(ANNEX / 'README_技术详稿导航.md').write_text('''# 技术详稿导航

本目录完整保留上一轮 V11 的 15 份 Markdown 技术文件，按主方案 6 份、模块实现 5 份、支持体验 4 份分类。配套两份 JSON 是原资料的逐字节存档。本轮没有删除任何历史功能、验收门或科学合同，也没有修改原规划目录。

新读者先读 [完整最终总方案](../01_完整最终方案/01_项目最终总方案.md)，再按需要查阅本目录。技术文件保留历史深化章节，以便追踪规则形成过程；其中的“本轮”“V5 至 V11”“已完成”均须结合所在章节和原证据理解，不代表产品已经实现或通过最终验收。

本目录 Markdown 仅迁移本地链接：能在本目录找到的链接指向复制件，外部源码、历史快照、研究和核验脚本指向原工作空间的绝对路径。JSON 中的旧相对路径仍以原资料目录为基准，它们属于证据存档，不是本目录的一键执行命令。迁移记录在 [来源与链接迁移台账](../99_来源与核验/技术详稿来源与链接迁移.json)。

## 主方案 6 份

1. [总入口与最终方案导航](01_主方案（6份）/README_FINAL.md)
2. [功能覆盖与支持矩阵](01_主方案（6份）/CAPABILITY_AND_SUPPORT_MATRIX.md)
3. [智能决策与任务编排](01_主方案（6份）/INTELLIGENCE_AND_ORCHESTRATION.md)
4. [技术合同与执行流程](01_主方案（6份）/CONTRACTS_AND_EXECUTION_RECIPES.md)
5. [完整开发计划与安装发布安排](01_主方案（6份）/IMPLEMENTATION_AND_RELEASE_PLAN.md)
6. [验收标准 竞争对比与证据追踪](01_主方案（6份）/VERIFICATION_AND_TRACEABILITY.md)

## 模块实现详稿 5 份

7. [实现详稿总入口](02_模块实现详稿（5份）/README_IMPLEMENTATION_DETAILS.md)
8. [模块接口 开发任务与依赖](02_模块实现详稿（5份）/MODULE_INTERFACES_AND_BACKLOG.md)
9. [领域数据合同与任务状态机](02_模块实现详稿（5份）/DOMAIN_CONTRACTS_AND_STATE_MACHINES.md)
10. [三条标准业务链的实现](02_模块实现详稿（5份）/THREE_CHAIN_IMPLEMENTATION.md)
11. [GP扩展 PS自动化与创新功能实现](02_模块实现详稿（5份）/GP_PS_AND_INNOVATION_RECIPES.md)

## 支持与用户体验详稿 4 份

12. [支持详稿总入口](03_支持与用户体验详稿（4份）/README_SUPPORT_DETAILS.md)
13. [模型接入 切换与故障降级](03_支持与用户体验详稿（4份）/MODEL_PROVIDER_AND_FALLBACK_CONTRACTS.md)
14. [跨版本兼容 环境能力与用户工作台](03_支持与用户体验详稿（4份）/ENVIRONMENT_AND_WORKBENCH_CONTRACTS.md)
15. [格式支持 转换损失与行业资源包](03_支持与用户体验详稿（4份）/FORMAT_AND_RESOURCE_PACK_CONTRACTS.md)
''', encoding='utf-8')
print(json.dumps({'copiedMarkdown': 15, 'archivedJson': 2, 'rewrittenLinks': len(links), 'missingOriginalLinks': len(missing)}, ensure_ascii=False))
