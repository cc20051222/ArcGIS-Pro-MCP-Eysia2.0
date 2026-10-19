"""Validate an Agent-authored task, derive display facts and atomically publish on D.

No GIS, network, configuration or automatic repair. The Agent supplies scientific
decisions and observations. A display record is not independent truth/acceptance.
"""
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
from render_task_board import validate, quality_summary, timestamp, output_path
from task_intake import intake_view


def project(record):
    plan, steps, order, evidence, presentation = validate(record)
    if not record.get('taskId') or not record.get('goal'):
        raise ValueError('taskId and business goal are required for a live workflow panel.')
    if not 1 <= len(order) <= 300:
        raise ValueError('Publish 1..300 explicit steps; do not silently truncate the workflow.')
    quality = quality_summary(record, evidence)
    raw_quality = record.get('quality') or {}
    requirements = {r['id']: r for r in raw_quality.get('requirements', [])}
    bound_requests = set()
    rows = []
    for key in order:
        step = steps[key]
        request_ids = step.get('requestIds', [])
        if (not isinstance(request_ids, list) or any(not isinstance(r, str) or not r for r in request_ids)
                or len(set(request_ids)) != len(request_ids) or bound_requests.intersection(request_ids)):
            raise ValueError('Each exact request ID must bind to one step only.')
        bound_requests.update(request_ids)
        if step['state'] == 'RUNNING' and not step.get('evidenceRefs'):
            raise ValueError('RUNNING requires an actual dispatch/start observation; a plan is not execution.')
        kind = step.get('kind', 'PROCESS')
        if kind not in {'PREFLIGHT', 'PROCESS', 'CHECK', 'REPAIR', 'RECHECK', 'FINAL', 'ACCEPTANCE'}:
            raise ValueError('Unknown business step kind.')
        req_ids = step.get('requirementIds', [])
        if not isinstance(req_ids, list) or any(r not in requirements for r in req_ids):
            raise ValueError('Step requirementIds must reference declared quality requirements.')
        if step.get('outputs'):
            for path in step['outputs']:
                output_path(path)
        if step['state'] in {'RUNNING', 'PRODUCED', 'VERIFIED'}:
            for dep in step.get('dependsOn', []):
                allowed={'VERIFIED','FAILED','STALE','BLOCKED'} if kind=='REPAIR' else {'VERIFIED'}
                if steps[dep]['state'] not in allowed:
                    raise ValueError('Started steps require verified dependencies; record a real replan instead of hiding blockers.')
        rows.append({**step, 'id': key, 'kind': kind, 'requestIds': request_ids,
                     'requirementIds': req_ids,
                     'observedAt': max((evidence[r]['observedAt'] for r in step.get('evidenceRefs', [])),
                                       key=timestamp, default=None)})
    checks = []
    for c in raw_quality.get('checks', []):
        req = requirements[c['requirementId']]
        checks.append({**c, 'effectiveState': quality['checkStates'][c['id']],
                       'title': req['title'], 'criterion': req['criterion'], 'criterionSource': req['source'],
                       'evidenceSummary': [evidence[r]['summary'] for r in c.get('evidenceRefs', [])]})
    return {'version': 1, 'taskId': record['taskId'], 'goal': record['goal'],
            'origin': record.get('recordOrigin', 'AGENT_RECORDED_NOT_INDEPENDENT_PROOF'),
            'revision': plan['revision'], 'scenario': plan.get('scenario'),
            'selectionReason': plan.get('selectionReason'), 'constraints': plan.get('constraints', []),
            'standards': raw_quality.get('standardMappings', []), 'changes': plan.get('changes', []),
            'observedAt': max((r['observedAt'] for r in evidence.values()), key=timestamp, default=None),
            'steps': rows, 'intake': intake_view(record, evidence, steps, output_path),
            'requirements': list(requirements.values()), 'checks': checks,
            'issues': raw_quality.get('issues', []), 'qualityHistory': raw_quality.get('history', []),
            'gates': quality['gates'], 'candidate': quality['candidate'], 'acceptance': quality['acceptance'],
            'artifacts': presentation.get('artifacts', []),
            'notice': 'Agent observations and recorded checks; publishing does not execute, repair or independently certify GIS.'}


def plain_d(path):
    output_path(str(path))
    target = Path(path).absolute()
    for item in (target, *target.parents):
        if item.exists() and (item.is_symlink() or (hasattr(item, 'is_junction') and item.is_junction())):
            raise ValueError('Reparse output path refused.')
    return target


def _publish(record, target, expected_task=None, activate=False):
    view = project(record)
    target = plain_d(target)
    target.parent.mkdir(parents=True, exist_ok=True)
    previous = None
    if target.exists():
        previous = json.loads(target.read_text(encoding='utf-8-sig'))
        old_id = previous.get('taskId')
        if old_id != record['taskId']:
            if not activate or expected_task != (old_id if old_id is not None else 'LEGACY'):
                raise ValueError('Switching active tasks requires --activate and exact --expected-task (use LEGACY for an old unlabelled record).')
        elif previous.get('plan', {}).get('revision', 0) > record['plan']['revision']:
            raise ValueError('Cannot replace a newer plan with an older revision.')
        else:
            old_refs={r['id'] for r in previous.get('observations', [])}
            if not old_refs.issubset({r['id'] for r in record.get('observations', [])}):
                raise ValueError('Updates must retain earlier observations, including failed attempts.')
            old_at=previous.get('workflowView', {}).get('observedAt')
            if old_at and (not view['observedAt'] or timestamp(old_at)>timestamp(view['observedAt'])):
                raise ValueError('An older observation cannot overwrite current task progress.')
            active={c['id']:c for c in record.get('quality',{}).get('checks',[])}
            history=record.get('quality',{}).get('history',[])
            for c in previous.get('quality',{}).get('checks',[]):
                if c.get('state') in {'PASS','FAIL','NOT_APPLICABLE'} and active.get(c['id'])!=c:
                    if not any(h.get('check',h)==c for h in history if isinstance(h,dict)):
                        raise ValueError('Preserve replaced completed checks in quality.history; do not erase failures.')
    # Original raw record is retained, allowing the same document to drive chat formatting.
    published = {**record, 'workflowView': view,
                 'publishedAt': dt.datetime.now().astimezone().isoformat()}
    payload = (json.dumps(published, ensure_ascii=False, indent=2)+'\n').encode('utf-8')
    if len(payload) > 2*1024*1024:
        raise ValueError('Panel record exceeds 2MiB; retain bulky evidence externally with references.')
    temp = None
    try:
        # Probe + atomic replacement in the destination's D directory, never system TEMP.
        with tempfile.NamedTemporaryFile(dir=target.parent, prefix='.workflow-', suffix='.json', delete=False) as f:
            temp = Path(f.name); f.write(payload); f.flush(); os.fsync(f.fileno())
        if previous is not None:
            history = target.parent / (target.stem + '-history')
            plain_d(history); history.mkdir(exist_ok=True)
            old = target.read_bytes(); identity=hashlib.sha256(old).hexdigest()
            archived=history/(identity+'.json')
            if not archived.exists():
                with archived.open('xb') as f:f.write(old)
        os.replace(temp, target); temp = None
    finally:
        if temp is not None and temp.exists():temp.unlink()
    return {'taskId': view['taskId'], 'revision': view['revision'], 'steps': len(view['steps']),
            'path': str(target), 'sha256': hashlib.sha256(payload).hexdigest().upper(),
            'gisExecuted': False, 'candidate': view['candidate'], 'acceptance': view['acceptance']}


def publish(record, target, expected_task=None, activate=False):
    target=plain_d(target);target.parent.mkdir(parents=True,exist_ok=True)
    lock=target.with_name(target.name+'.publish-lock')
    # Single writer, fail closed on a stale/crashed lock; never erase someone else's lock.
    fd=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY)
    try:
        os.write(fd,str(os.getpid()).encode('ascii'))
        return _publish(record,target,expected_task,activate)
    finally:
        os.close(fd);lock.unlink()


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('record',type=Path);p.add_argument('--target',required=True,type=Path)
    p.add_argument('--activate',action='store_true');p.add_argument('--expected-task')
    a=p.parse_args()
    try:
        record=json.loads(a.record.read_text(encoding='utf-8-sig'))
        print(json.dumps(publish(record,a.target,a.expected_task,a.activate),ensure_ascii=False))
        return 0
    except (ValueError,OSError,KeyError,TypeError) as e:
        print(json.dumps({'error':str(e),'gisExecuted':False},ensure_ascii=False),file=sys.stderr);return 2


if __name__=='__main__':raise SystemExit(main())
