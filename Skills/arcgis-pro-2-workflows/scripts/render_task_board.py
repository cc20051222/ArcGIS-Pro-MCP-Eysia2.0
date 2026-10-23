"""Format an existing task record as Markdown. No MCP, GIS, network or file writes."""
import argparse
from datetime import datetime
import html
import json
from pathlib import Path, PureWindowsPath
import re
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from task_intake import intake_view

STATES = {
    'PLANNED': '待执行', 'RUNNING': '执行中（内部进度见观测）', 'PRODUCED': '已产出，待核验',
    'VERIFIED': '已核验', 'WAITING_USER': '等待用户', 'BLOCKED': '受阻', 'FAILED': '失败',
    'CANCEL_REQUESTED': '已请求取消，尚未确认停止', 'CANCELLED': '已确认取消',
    'UNKNOWN': '状态/效果未知', 'SKIPPED': '已跳过', 'STALE': '已失效，不能沿用',
}
EVIDENCED_STATES = {'PRODUCED', 'VERIFIED', 'FAILED', 'CANCELLED'}
ARTIFACT_STATES = {'PLANNED', 'PRODUCED', 'VERIFIED', 'STALE'}
STEP_KINDS = {'PREFLIGHT', 'PROCESS', 'CHECK', 'REPAIR', 'RECHECK', 'FINAL', 'ACCEPTANCE'}
PHASES = ('PREFLIGHT', 'PROCESS', 'RECHECK', 'FINAL')
PHASE_LABELS = {'PREFLIGHT': '资料预检', 'PROCESS': '过程与首次检查', 'RECHECK': '复检/问题闭环', 'FINAL': '最终交付检查'}
QUALITY_LABELS = {'PASS': '检查通过', 'FAIL': '检查未通过', 'NOT_RUN': '尚未检查', 'BLOCKED': '受阻',
                  'STALE': '检查依据已失效', 'NOT_CONFIGURED': '尚未配置检查要求', 'UNKNOWN': '未知',
                  'INSUFFICIENT_COVERAGE': '检查覆盖不足', 'REQUIRED_CHECK_NOT_DONE': '必需检查未完成',
                  'RECHECK_EVIDENCE_INSUFFICIENT': '复检证据不足', 'FINAL_EVIDENCE_PREDATES_RECHECK': '最终检查未使用复检后的新证据',
                  'NOT_APPLICABLE': '不适用（有据说明）'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def text(value):
    value = html.escape(str(value), quote=False).replace('\r', ' ').replace('\n', ' ')
    return re.sub(r'([\\`*_{\[\]}()!|~])', r'\\\1', value)


def timestamp(value):
    require(isinstance(value, str), 'Observation time must be an ISO timestamp with timezone.')
    parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(parsed.tzinfo is not None, 'Observation time must include timezone.')
    return parsed


def output_path(value):
    require(isinstance(value, str), 'Artifact path must be a string.')
    path = PureWindowsPath(value)
    require(path.is_absolute() and path.drive.upper() == 'D:' and '..' not in path.parts,
            'Artifact paths must be absolute D-drive paths without parent traversal.')
    require(not any(ord(c) < 32 or c in '<>"|?*' for c in value), 'Invalid artifact path characters.')
    require(':' not in value[2:], 'Alternate data streams are not artifact paths.')
    if sys.platform == 'win32':
        require(Path(value).resolve().drive.upper() == 'D:', 'Artifact path resolves outside D drive.')
    return path.as_posix()


def quality_summary(record, evidence):
    """Derive gates from recorded assertions, never from an incoming PASS flag."""
    quality = record.get('quality')
    if quality is None:
        return {'gates': {p: {'state': 'NOT_CONFIGURED', 'passed': 0, 'total': 0} for p in PHASES},
                'candidate': 'NOT_READY', 'acceptance': 'NOT_REQUESTED', 'checkStates': {}, 'openIssues': []}
    require(isinstance(quality, dict), 'quality must be an object.')
    profile_revision = quality.get('profileRevision')
    require(type(profile_revision) is int and profile_revision >= 1, 'Quality profileRevision must be positive.')
    subjects = quality.get('subjectVersions', {})
    require(isinstance(subjects, dict) and all(isinstance(k, str) and isinstance(v, str) and v for k, v in subjects.items()),
            'subjectVersions must map subject IDs to non-empty version strings.')
    requirements = quality.get('requirements', [])
    checks = quality.get('checks', [])
    issues = quality.get('issues', [])
    require(all(isinstance(value, list) for value in (requirements, checks, issues)), 'Quality requirements/checks/issues must be arrays.')
    reqs = {}
    for req in requirements:
        require(isinstance(req, dict) and isinstance(req.get('id'), str) and req['id'] and req['id'] not in reqs,
                'Quality requirement IDs must be unique strings.')
        require(all(isinstance(req.get(k), str) and req[k] for k in ('title', 'criterion', 'source')),
                'Each requirement needs title, criterion and its source.')
        require(type(req.get('required', True)) is bool, 'Requirement required must be boolean.')
        require(type(req.get('allowEmpty', False)) is bool, 'Requirement allowEmpty must be boolean.')
        phases = req.get('phases')
        require(isinstance(phases, list) and phases and len(set(phases)) == len(phases) and all(p in PHASES for p in phases),
                'Requirement phases must be unique known phases.')
        sub_ids = req.get('subjectIds')
        require(isinstance(sub_ids, list) and sub_ids and all(isinstance(s, str) and s in subjects for s in sub_ids),
                'Requirements must bind to declared subject identities/versions.')
        require(req.get('coverageRequired') in {'FULL', 'SAMPLE_ALLOWED'}, 'Declare required coverage.')
        reqs[req['id']] = req

    def refs_ok(refs):
        return isinstance(refs, list) and bool(refs) and all(isinstance(r, str) and r in evidence for r in refs)

    def check_time(check):
        return max(timestamp(evidence[r]['observedAt']) for r in check['evidenceRefs'])

    by_pair, by_id, effective = {}, {}, {}
    valid_states = {'NOT_RUN', 'PASS', 'FAIL', 'BLOCKED', 'UNKNOWN', 'NOT_APPLICABLE', 'STALE'}
    for check in checks:
        require(isinstance(check, dict) and isinstance(check.get('id'), str) and check['id'] and check['id'] not in by_id,
                'Quality check IDs must be unique strings.')
        req_id, phase = check.get('requirementId'), check.get('phase')
        require(req_id in reqs and phase in reqs[req_id]['phases'], 'Quality check has an unknown requirement/phase.')
        require((req_id, phase) not in by_pair, 'Duplicate active requirement/phase check; preserve old attempts in history.')
        state = check.get('state')
        require(state in valid_states, 'Unknown quality check state.')
        require(isinstance(check.get('scope', {}), dict), 'Check scope must be an object.')
        refs = check.get('evidenceRefs', [])
        require(isinstance(refs, list) and all(isinstance(r, str) and r in evidence for r in refs), 'Invalid quality observation reference.')
        if state in {'PASS', 'FAIL', 'NOT_APPLICABLE'}:
            require(refs_ok(refs) and isinstance(check.get('method'), str) and check['method'],
                    'Completed checks need a method and recorded evidence.')
        bindings = check.get('bindings', {})
        require(isinstance(bindings, dict), 'Check bindings must be an object.')
        if state == 'PASS':
            if (check.get('profileRevision') != profile_revision
                    or quality.get('boundPlanRevision') != record['plan']['revision']
                    or any(bindings.get(s) != subjects[s] for s in reqs[req_id]['subjectIds'])):
                state = 'STALE'
            scope = check.get('scope', {})
            require(isinstance(scope, dict) and scope.get('mode') in {'FULL', 'SAMPLE'}, 'Passing check needs explicit coverage scope.')
            population, checked = scope.get('population'), scope.get('checked')
            require((population is None or (type(population) is int and population >= 0))
                    and type(checked) is int and checked >= 0, 'Invalid population/checked counts.')
            require(population is None or checked <= population, 'Checked count exceeds population.')
            if scope['mode'] == 'FULL' and (population is None or checked != population):
                state = 'INSUFFICIENT_COVERAGE'
            if scope['mode'] == 'FULL' and population == 0:
                rationale = scope.get('emptyRationale')
                if not (reqs[req_id].get('allowEmpty', False)
                        and isinstance(rationale, str) and rationale.strip()):
                    state = 'INSUFFICIENT_COVERAGE'
            if scope['mode'] == 'SAMPLE':
                require(isinstance(scope.get('sampleDesign'), str) and scope['sampleDesign'], 'Sampling requires a design/basis.')
                if reqs[req_id]['coverageRequired'] == 'FULL' or checked == 0:
                    state = 'INSUFFICIENT_COVERAGE'
        if state == 'NOT_APPLICABLE':
            require(isinstance(check.get('rationale'), str) and check['rationale'], 'N/A needs a rationale.')
            if reqs[req_id].get('required', True):
                state = 'REQUIRED_CHECK_NOT_DONE'
        effective[check['id']] = state
        by_pair[(req_id, phase)] = check
        by_id[check['id']] = check
    for check in checks:
        req = reqs[check['requirementId']]
        if check['phase'] == 'RECHECK' and effective[check['id']] == 'PASS' and 'PROCESS' in req['phases']:
            initial = by_pair.get((req['id'], 'PROCESS'))
            if (initial is None or check.get('recheckOf') != initial['id']
                    or effective[initial['id']] != 'PASS'
                    or not set(check['evidenceRefs']).isdisjoint(initial['evidenceRefs'])
                    or check_time(check) < check_time(initial)):
                effective[check['id']] = 'RECHECK_EVIDENCE_INSUFFICIENT'
    rechecks = [c for c in checks if c['phase'] == 'RECHECK' and effective[c['id']] == 'PASS']
    for check in checks:
        if check['phase'] == 'FINAL' and effective[check['id']] == 'PASS' and rechecks:
            if (check_time(check) < max(check_time(c) for c in rechecks)
                    or any(not set(check['evidenceRefs']).isdisjoint(c['evidenceRefs']) for c in rechecks)):
                effective[check['id']] = 'FINAL_EVIDENCE_PREDATES_RECHECK'

    gates = {}
    for phase in PHASES:
        relevant = [r for r in requirements if r.get('required', True) and phase in r['phases']]
        states = [effective.get(by_pair[(r['id'], phase)]['id'], 'NOT_RUN') if (r['id'], phase) in by_pair else 'NOT_RUN' for r in relevant]
        passed = states.count('PASS')
        state = ('NOT_CONFIGURED' if not relevant else 'PASS' if passed == len(states)
                 else 'FAIL' if 'FAIL' in states else 'STALE' if 'STALE' in states
                 else 'NOT_RUN' if all(s == 'NOT_RUN' for s in states) else 'BLOCKED')
        gates[phase] = {'state': state, 'passed': passed, 'total': len(relevant)}
    open_issues, issue_ids = [], set()
    for issue in issues:
        require(isinstance(issue, dict) and isinstance(issue.get('id'), str) and issue['id'] and issue['id'] not in issue_ids,
                'Issue IDs must be unique strings.')
        issue_ids.add(issue['id'])
        require(issue.get('severity') in {'CRITICAL', 'MAJOR', 'MINOR'}, 'Unknown issue severity.')
        require(issue.get('status') in {'OPEN', 'RESOLVED', 'ACCEPTED_RESIDUAL'}, 'Unknown issue state.')
        require(isinstance(issue.get('description'), str) and issue['description'], 'Issue description is required.')
        if issue['status'] == 'RESOLVED':
            require(refs_ok(issue.get('resolutionEvidenceRefs')), 'Resolved issue needs repair/recheck evidence.')
            recheck_refs = {r for c in rechecks for r in c['evidenceRefs']}
            require(not set(issue['resolutionEvidenceRefs']).isdisjoint(recheck_refs),
                    'Issue closure must reference a current successful recheck, not just the initial report.')
        elif issue['status'] == 'ACCEPTED_RESIDUAL':
            require(issue['severity'] == 'MINOR' and issue.get('acceptedByRole') in {'USER', 'INDEPENDENT_REVIEWER'}
                    and refs_ok(issue.get('acceptanceEvidenceRefs')), 'Only externally accepted, evidenced minor residuals are allowed.')
        else:
            open_issues.append(issue)
    required_steps = [s for s in record['plan']['steps'] if s.get('required', True)]
    production_done = bool(required_steps) and all(s['state'] == 'VERIFIED' for s in required_steps)
    candidate = 'PASS_CANDIDATE' if all(g['state'] == 'PASS' for g in gates.values()) and not open_issues and production_done else 'NOT_READY'
    acceptance = quality.get('acceptance', {})
    require(isinstance(acceptance, dict), 'acceptance must be an object.')
    acceptance_status = acceptance.get('status', 'NOT_REQUESTED')
    require(acceptance_status in {'NOT_REQUESTED', 'WAITING_REVIEW', 'ACCEPTED', 'REJECTED'}, 'Unknown acceptance status.')
    if acceptance_status in {'ACCEPTED', 'REJECTED'}:
        require(acceptance.get('reviewerRole') in {'USER', 'INDEPENDENT_REVIEWER'}
                and isinstance(acceptance.get('reviewerId'), str) and acceptance['reviewerId']
                and acceptance['reviewerId'] != quality.get('producerId')
                and refs_ok(acceptance.get('evidenceRefs')), 'Acceptance requires an external, identified reviewer and evidence.')
        if acceptance_status == 'ACCEPTED':
            require(candidate == 'PASS_CANDIDATE', 'Cannot accept an incomplete, failed or stale quality candidate.')
            check_refs = {r for c in checks for r in c.get('evidenceRefs', [])}
            require(set(acceptance['evidenceRefs']).isdisjoint(check_refs), 'Technical checks do not replace an external acceptance receipt.')
            final_times = [check_time(c) for c in checks if c['phase'] == 'FINAL']
            require(max(timestamp(evidence[r]['observedAt']) for r in acceptance['evidenceRefs']) >= max(final_times),
                    'Acceptance evidence predates the final checks.')
    return {'gates': gates, 'candidate': candidate, 'acceptance': acceptance_status,
            'checkStates': effective, 'openIssues': open_issues}


def validate(record):
    require(isinstance(record, dict), 'Task record must be an object.')
    plan = record.get('plan')
    require(isinstance(plan, dict), 'Expected plan object; use the current task-record template.')
    revision = plan.get('revision')
    require(type(revision) is int and revision >= 1, 'Plan revision must be a positive integer.')
    steps = plan.get('steps')
    require(isinstance(steps, list), 'plan.steps must be an array.')
    observations = record.get('observations', [])
    require(isinstance(observations, list), 'observations must be an array.')
    evidence = {}
    for row in observations:
        require(isinstance(row, dict), 'Observation must be an object.')
        key = row.get('id')
        require(isinstance(key, str) and key and key not in evidence, 'Observation IDs must be unique non-empty strings.')
        require(all(isinstance(row.get(k), str) and row[k] for k in ('source', 'summary')),
                'Observation source and summary are required strings.')
        timestamp(row.get('observedAt'))
        evidence[key] = row
    ids, bound_requests = {}, set()
    for step in steps:
        require(isinstance(step, dict), 'Step must be an object.')
        key = step.get('id')
        require(isinstance(key, str) and re.fullmatch(r'[A-Za-z][A-Za-z0-9_-]{0,63}', key) is not None,
                'Step ID must be a simple stable identifier.')
        require(key not in ids, 'Duplicate step ID: ' + key)
        require(isinstance(step.get('title'), str) and step['title'], 'Step title is required.')
        require(step.get('state') in STATES, 'Unknown step state: ' + str(step.get('state')))
        refs = step.get('evidenceRefs', [])
        require(isinstance(refs, list) and all(isinstance(r, str) and r in evidence for r in refs),
                'Step evidenceRefs must reference recorded observations.')
        require(step['state'] not in EVIDENCED_STATES or bool(refs), 'This state requires observation evidence: ' + key)
        require(step['state'] != 'RUNNING' or bool(refs),
                'RUNNING requires an actual dispatch/start observation; a plan is not execution.')
        require(step.get('kind', 'PROCESS') in STEP_KINDS, 'Unknown business step kind.')
        requests = step.get('requestIds', [])
        require(isinstance(requests, list) and all(isinstance(r, str) and r for r in requests)
                and len(set(requests)) == len(requests) and not bound_requests.intersection(requests),
                'Each exact request ID must bind to one step only.')
        bound_requests.update(requests)
        outputs = step.get('outputs', [])
        require(isinstance(outputs, list), 'Step outputs must be an array of paths.')
        for value in outputs:
            output_path(value)
        deps = step.get('dependsOn', [])
        require(isinstance(deps, list) and all(isinstance(d, str) for d in deps) and len(set(deps)) == len(deps),
                'dependsOn must contain unique step IDs.')
        require(type(step.get('required', True)) is bool, 'Step required must be boolean.')
        ids[key] = step
    adjacency = {key: [] for key in ids}
    indegree = {key: 0 for key in ids}
    for key, step in ids.items():
        for dep in step.get('dependsOn', []):
            require(dep in ids and dep != key, 'Unknown/self dependency: ' + str(dep))
            if step['state'] in {'RUNNING', 'PRODUCED', 'VERIFIED'}:
                allowed = {'VERIFIED', 'FAILED', 'STALE', 'BLOCKED'} if step.get('kind') == 'REPAIR' else {'VERIFIED'}
                require(ids[dep]['state'] in allowed,
                        'Started steps require verified dependencies; record a real replan instead of hiding blockers.')
            adjacency[dep].append(key)
            indegree[key] += 1
    queue = [key for key in ids if indegree[key] == 0]
    order = []
    for key in queue:
        order.append(key)
        for child in adjacency[key]:
            indegree[child] -= 1
            if indegree[child] == 0:
                queue.append(child)
    require(len(order) == len(ids), 'Step dependency graph contains a cycle.')
    execution = record.get('execution', {})
    require(isinstance(execution, dict), 'execution must be an object.')
    if execution.get('status') == 'COMPLETED':
        required = [s for s in steps if s.get('required', True)]
        require(bool(required) and all(s['state'] == 'VERIFIED' for s in required),
                'COMPLETED requires every required step to be VERIFIED.')
    presentation = record.get('presentation', {})
    require(isinstance(presentation, dict), 'presentation must be an object.')
    if presentation.get('lastObservedAt') is not None:
        last = timestamp(presentation['lastObservedAt'])
        require(bool(evidence) and last == max(timestamp(e['observedAt']) for e in evidence.values()),
                'lastObservedAt must match the most recent recorded observation, not a refresh time.')
    counts = presentation.get('runtimeCounts')
    if counts is not None:
        require(isinstance(counts, dict), 'runtimeCounts must be an object or null.')
        require(isinstance(counts.get('evidenceRef'), str) and counts['evidenceRef'] in evidence,
                'Runtime counts need a recorded observation reference.')
        require(isinstance(counts.get('basis'), str) and counts['basis'], 'Runtime counts require a scope/basis.')
        for key in ('total', 'done', 'failed', 'skipped', 'pending'):
            require(counts.get(key) is None or (type(counts[key]) is int and counts[key] >= 0),
                    'Counts must be non-negative integers or null.')
        require(counts.get('done') is not None, 'Observed success count is required.')
        total = counts.get('total')
        if total is not None:
            require(sum(counts.get(key) or 0 for key in ('done', 'failed', 'skipped', 'pending')) <= total,
                    'Counts exceed the declared total.')
    artifacts = presentation.get('artifacts', [])
    require(isinstance(artifacts, list), 'artifacts must be an array.')
    for item in artifacts:
        require(isinstance(item, dict) and isinstance(item.get('label'), str) and item['label'],
                'Artifact must have a label.')
        require(item.get('stepId') in ids, 'Artifact must reference a known step.')
        require(item.get('state') in ARTIFACT_STATES, 'Invalid artifact state.')
        if item['state'] in {'PRODUCED', 'VERIFIED'}:
            require(isinstance(item.get('observationRef'), str) and item['observationRef'] in evidence,
                    'Produced artifacts require a recorded observation reference.')
        if item['state'] == 'VERIFIED':
            require(ids[item['stepId']]['state'] == 'VERIFIED', 'Verified artifact needs a verified owning step.')
        if item.get('path') is not None:
            output_path(item['path'])
        require(type(item.get('isPreview', False)) is bool, 'isPreview must be boolean.')
    intake_view(record, evidence, ids, output_path)
    quality = quality_summary(record, evidence)
    requirements = {r['id'] for r in (record.get('quality') or {}).get('requirements', [])}
    for step in steps:
        refs = step.get('requirementIds', [])
        require(isinstance(refs, list) and all(isinstance(r, str) and r in requirements for r in refs),
                'Step requirementIds must reference declared quality requirements.')
    if execution.get('status') == 'COMPLETED' and record.get('mode') != 'inspect':
        require(quality['candidate'] == 'PASS_CANDIDATE', 'Production completion requires preflight, process, recheck and final checks; external acceptance remains separate.')
    return plan, ids, order, evidence, presentation


def render(record, include_images=False):
    plan, steps, order, evidence, presentation = validate(record)
    lines = ['**流程与成果状态板**', '',
             '**记录来源：** ' + text(record.get('recordOrigin', '未注明；不视为运行证明')),
             '**目标：** ' + text(record.get('goal') or '未填写'),
             '**计划版本：** ' + str(plan['revision'])]
    intake = intake_view(record, evidence, steps, output_path)
    active = [key + ' ' + s['title'] for key, s in steps.items() if s['state'] == 'RUNNING']
    lines.extend(['', '**▶ 当前执行步骤：** ' + text('；'.join(active) if active else '当前无已观测的执行中步骤')])
    if intake['status'] != 'LEGACY_UNCONFIRMED':
        for field, label in [('requirements', '需求'), ('inputs', '数据位置'), ('outputRoot', '成果保存位置')]:
            row = intake[field]
            value = row.get('value')
            lines.append('**' + label + '：** ' + text('；'.join(value) if isinstance(value, list) else value or '待提供') + ' · ' + text(row['status']))
        for gap in intake['dataNeeds']:
            lines.append('- 补充资料：' + text(gap['label']) + ' · ' + text(gap['state']) + '；用途：' + text(gap['reason']) + '；建议：' + text(gap['recommendation']))
        for choice in intake['toolChoices']:
            lines.append('- 工具方案 ' + text(choice['stepId']) + '：' + text(choice['status']) + ' · ' + text(' / '.join(choice['tools'])) + '；' + text(choice['reason']))
            for field in ('equivalence', 'limits', 'alternativesConsidered'):
                if choice.get(field): lines.append('  ' + text(choice[field]))
    for question in intake['questions']:
        lines.append('- 待补充确认：' + text(question))
    overall = record.get('execution', {}).get('status', 'UNKNOWN')
    overall_labels = dict(STATES, NOT_STARTED='尚未开始', COMPLETED='制作及技术检查已完成；外部验收另列',
                          PARTIAL='部分完成', WAITING='等待')
    lines.append('**任务记录状态：** ' + text(overall_labels.get(overall, overall)))
    if evidence:
        latest = max(evidence.values(), key=lambda e: timestamp(e['observedAt']))
        lines.append('**最近记录观测：** ' + text(latest['observedAt']))
    else:
        lines.append('**最近记录观测：** 尚无观测')
    lines.extend(['', '以下按提供的记录展示；格式检查不证明 GIS 执行、授权或成果正确。'])
    if plan.get('selectionReason'):
        lines.extend(['', '**选择依据：** ' + text(plan['selectionReason'])])
    verified = sum(s['state'] == 'VERIFIED' for s in steps.values())
    lines.extend(['', '**步骤核验：** ' + str(verified) + '/' + str(len(steps)) + '；这不是耗时进度。'])
    counts = presentation.get('runtimeCounts')
    if counts:
        count_text = ', '.join(label + ' ' + ('未知' if counts.get(key) is None else str(counts[key]))
                               for key, label in [('done', '执行成功'), ('failed', '失败'), ('skipped', '跳过'),
                                                  ('pending', '待处理'), ('total', '总数')])
        lines.extend(['', '**后端计数（' + text(counts['basis']) + '）：** ' + count_text,
                      '计数依据：' + text(counts['evidenceRef']) + '，观测于 '
                      + text(evidence[counts['evidenceRef']]['observedAt']) + '；执行成功不等于成果已核验。'])
    else:
        lines.extend(['', '**工具内部进度：** 未提供计数；未知，不生成百分比或预计完成时间。'])
    if steps:
        labels = {key: 'N' + str(i) for i, key in enumerate(order)}
        lines.extend(['', '客户端支持 Mermaid 时可显示流程图；文字步骤表始终保留。', '', '```mermaid', 'flowchart TD'])
        for key in order:
            label = steps[key]['title'] + '：' + STATES[steps[key]['state']]
            # Encode Mermaid metacharacters; never embed record-supplied syntax.
            safe = ''.join(c if c.isalnum() or c == ' ' else '#' + str(ord(c)) + ';' for c in label)
            lines.append('  ' + labels[key] + '["' + safe + '"]')
        for key in order:
            for dep in steps[key].get('dependsOn', []):
                lines.append('  ' + labels[dep] + ' --> ' + labels[key])
        lines.append('  classDef active fill:#10413f,stroke:#44d6b9,stroke-width:3px,color:#ffffff')
        for key in order:
            if steps[key]['state'] == 'RUNNING': lines.append('  class ' + labels[key] + ' active')
        lines.extend(['```', '', '| 步骤 | 依赖 | 状态 | 最新说明与依据 |', '|---|---|---|---|'])
        for key in order:
            step = steps[key]
            detail = text(step.get('summary', ''))
            if step.get('evidenceRefs'):
                detail += '（依据：' + text(', '.join(step['evidenceRefs'])) + '）'
            lines.append('| ' + text(key + ' ' + step['title']) + ' | ' + text(', '.join(step.get('dependsOn', [])) or '无')
                         + ' | ' + STATES[step['state']] + ' | ' + detail + ' |')
    else:
        lines.extend(['', '尚未定义步骤；不表示任务已经完成。'])
    changes = plan.get('changes', [])
    if changes:
        lines.extend(['', '**方案变更记录：**', ''])
        lines.extend('- ' + text(json.dumps(c, ensure_ascii=False) if isinstance(c, dict) else c) for c in changes)
    quality = quality_summary(record, evidence)
    lines.extend(['', '**质量检查与验收：**', '', '| 阶段 | 必需检查通过数 | 记录状态 |', '|---|---|---|'])
    for phase in PHASES:
        gate = quality['gates'][phase]
        lines.append('| ' + PHASE_LABELS[phase] + ' | ' + str(gate['passed']) + '/' + str(gate['total']) + ' | ' + QUALITY_LABELS[gate['state']] + ' |')
    acceptance_labels = {'NOT_REQUESTED':'尚未提交外部验收','WAITING_REVIEW':'待外部验收',
                         'ACCEPTED':'记录称外部已验收','REJECTED':'外部验收已拒收'}
    lines.extend(['', '技术候选：' + ('PASS CANDIDATE／待验收' if quality['candidate'] == 'PASS_CANDIDATE' else '尚未具备候选条件')
                  + '；外部验收：' + acceptance_labels[quality['acceptance']] + '。离线格式检查不授予最终 PASS。'])
    raw_quality = record.get('quality') or {}
    if raw_quality.get('requirements'):
        lines.extend(['', '| 质量要求 | 判据 | 依据 | 检查覆盖要求 |', '|---|---|---|---|'])
        for req in raw_quality['requirements']:
            lines.append('| ' + text(req['id'] + ' ' + req['title']) + ' | ' + text(req['criterion']) + ' | '
                         + text(req['source']) + ' | ' + ('全检' if req['coverageRequired'] == 'FULL' else '允许有据抽样')
                         + ('；允许空范围（须有适用说明）' if req.get('allowEmpty', False) else '') + ' |')
    if raw_quality.get('checks'):
        lines.extend(['', '| 检查 | 阶段 | 覆盖 | 有效结果 | 证据 |', '|---|---|---|---|---|'])
        for check in raw_quality['checks']:
            scope = check.get('scope', {})
            coverage = str(scope.get('mode', '未记录')) + ' ' + str(scope.get('checked', '?')) + '/' + str(scope.get('population', '?'))
            if scope.get('mode') == 'FULL' and scope.get('population') == 0:
                reason = scope.get('emptyRationale')
                coverage += '；空范围说明：' + (reason.strip() if isinstance(reason, str) and reason.strip() else '尚未记录适用依据')
            lines.append('| ' + text(check['id'] + ' / ' + check['requirementId']) + ' | ' + PHASE_LABELS[check['phase']]
                         + ' | ' + text(coverage) + ' | ' + QUALITY_LABELS[quality['checkStates'][check['id']]] + ' | ' + text(', '.join(check.get('evidenceRefs', []))) + ' |')
    if quality['openIssues']:
        lines.extend(['', '**未关闭问题：**', ''])
        lines.extend('- ' + text(i['id'] + ' [' + i['severity'] + '] ' + i['description']) for i in quality['openIssues'])
    acceptance_record = raw_quality.get('acceptance') or {}
    if acceptance_record.get('reviewerId'):
        lines.extend(['', '验收记录主体：' + text(acceptance_record['reviewerId']) + ' / '
                      + text(acceptance_record.get('reviewerRole', '未注明')) + '；依据：'
                      + text(', '.join(acceptance_record.get('evidenceRefs', []))) + '。身份与裁定需由真实会话核实。'])
    pending_phase = next((p for p in PHASES if quality['gates'][p]['state'] != 'PASS'), None)
    if quality['acceptance'] == 'REJECTED':
        next_action = '核对拒收原因，修复后重新复检和提交，不自动修改验收结论。'
    elif quality['openIssues']:
        next_action = '处理未关闭问题，并在实际复检后记录新证据。'
    elif pending_phase:
        next_action = '完成或配置“' + PHASE_LABELS[pending_phase] + '”；无依据时保持待检查/受阻。'
    elif quality['candidate'] != 'PASS_CANDIDATE':
        next_action = '完成剩余必需制作步骤及其核验。'
    elif quality['acceptance'] == 'ACCEPTED':
        next_action = '按真实验收记录交付同一版本，后续改动重新核验。'
    else:
        next_action = '提交当前成果和检查记录供有权角色验收；不代为批准。'
    lines.extend(['', '**下一步：** ' + next_action])
    if evidence:
        latest_rows = sorted(evidence.values(), key=lambda e: timestamp(e['observedAt']))[-10:]
        lines.extend(['', '**观测依据摘要（最近 ' + str(len(latest_rows)) + '/' + str(len(evidence)) + ' 条）：**', '',
                      '| 依据 | 实际记录时间 | 来源 | 摘要 |', '|---|---|---|---|'])
        for row in latest_rows:
            lines.append('| ' + text(row['id']) + ' | ' + text(row['observedAt']) + ' | ' + text(row['source']) + ' | ' + text(row['summary']) + ' |')
    artifacts = presentation.get('artifacts', [])
    if artifacts:
        lines.extend(['', '**产物与预览：**', ''])
        for item in artifacts:
            label = text(item['label'])
            state = item['state']
            path = output_path(item['path']) if item.get('path') else None
            exists = bool(path and Path(path).exists())
            if state in {'PLANNED', 'STALE'} or not exists:
                reason = '计划输出，尚未生成' if state == 'PLANNED' else ('旧版本已失效' if state == 'STALE' else '记录称已产出；当前本机无法确认路径存在')
                lines.append('- ' + label + '：' + reason + ('；路径 ' + text(path) if path else ''))
            else:
                observed = evidence[item['observationRef']]['observedAt']
                context = '步骤 ' + item['stepId'] + '；计划版本 ' + str(plan['revision']) + '；记录观测 ' + observed
                lines.append('- [' + label + '](<' + path + '>)：' + STATES[state] + '；' + text(context))
                if include_images and item.get('isPreview') and Path(path).is_file() and Path(path).suffix.lower() in {'.png', '.jpg', '.jpeg'}:
                    lines.extend(['', '![' + label + '](<' + path + '>)', ''])
    return '\n'.join(lines) + '\n'


def main():
    # A redirected Windows Python process may otherwise emit the local ANSI
    # code page. Keep saved Markdown and structured errors consistently UTF-8.
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, 'reconfigure'):
            stream.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('record', type=Path)
    parser.add_argument('--include-images', action='store_true', help='Embed existing local preview files. Does not inspect pixels.')
    args = parser.parse_args()
    try:
        record = json.loads(args.record.read_text(encoding='utf-8-sig'))
        print(render(record, args.include_images), end='')
    except (OSError, ValueError, KeyError, TypeError, RuntimeError) as exc:
        print(json.dumps({'error': str(exc), 'runtimeExecuted': False}, ensure_ascii=False), file=sys.stderr)
        return 2
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
