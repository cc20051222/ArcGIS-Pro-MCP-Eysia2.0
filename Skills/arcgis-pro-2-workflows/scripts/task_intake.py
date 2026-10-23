"""Shared intake/capability contract for chat and panel; no GIS or file effects."""
from pathlib import Path, PureWindowsPath
import sys


def intake_view(record, evidence, steps, output_path):
    def need(ok, message):
        if not ok:
            raise ValueError(message)

    intake = record.get('intake')
    if intake is None:
        need(record.get('schemaVersion') != '1.3', 'Schema 1.3 requires intake.')
        return {'status': 'LEGACY_UNCONFIRMED', 'questions': ['旧记录未记录资料与输出位置确认；新任务须补全。'],
                'dataNeeds': [], 'toolChoices': [], 'inputs': [], 'outputRoot': None}
    need(isinstance(intake, dict), 'intake must be an object.')
    questions = []
    for field, question in [('requirements', '您要完成什么分析、范围和交付要求？'),
                            ('inputs', '本次数据在哪里？请提供文件或文件夹路径。'),
                            ('outputRoot', '处理完成的数据和报告保存在哪里？')]:
        row = intake.get(field, {})
        need(isinstance(row, dict), 'Intake field must be an object.')
        need(row.get('status') in {'MISSING', 'PROVIDED', 'CONFIRMED'}, 'Invalid intake status.')
        refs = row.get('evidenceRefs', [])
        need(isinstance(refs, list) and all(r in evidence for r in refs), 'Intake needs known observation references.')
        if row['status'] == 'CONFIRMED':
            need(bool(refs), 'Confirmed intake requires user-provided/confirmed observation evidence.')
            need(any(evidence[r].get('actor') == 'USER' for r in refs), 'Intake confirmation must reference a USER observation.')
        value = row.get('value')
        if row['status'] != 'MISSING':
            need(bool(value), 'Provided intake value cannot be empty.')
        if field == 'inputs':
            need(isinstance(value, list) and all(isinstance(p, str) and p for p in value), 'inputs.value must be paths array.')
        elif value is not None:
            need(isinstance(value, str), 'Intake value must be text.')
        if field == 'outputRoot' and value:
            output_path(value)
        if row['status'] != 'CONFIRMED':
            questions.append(question)
    gaps = intake.get('dataNeeds', [])
    need(isinstance(gaps, list), 'dataNeeds must be an array.')
    ids = set()
    for gap in gaps:
        need(isinstance(gap, dict) and gap.get('id') and gap['id'] not in ids, 'Data need IDs must be unique.')
        ids.add(gap['id'])
        need(gap.get('state') in {'MISSING', 'PROVIDED', 'VERIFIED', 'NOT_REQUIRED'}, 'Invalid data-need state.')
        need(type(gap.get('required')) is bool and gap.get('label') and gap.get('reason') and gap.get('recommendation'),
             'Data needs require label, required, reason and actionable recommendation.')
        need(isinstance(gap.get('stepIds', []), list) and all(s in steps for s in gap.get('stepIds', [])), 'Unknown data-need step.')
        refs = gap.get('evidenceRefs', [])
        need(isinstance(refs, list) and all(r in evidence for r in refs), 'Unknown data-need evidence.')
        if gap['state'] in {'VERIFIED', 'NOT_REQUIRED'}:
            need(bool(refs), 'Resolved data needs require observation evidence.')
    choices = intake.get('toolChoices', [])
    need(isinstance(choices, list), 'toolChoices must be an array.')
    choice_ids = set()
    for choice in choices:
        need(isinstance(choice, dict) and choice.get('stepId') in steps and choice['stepId'] not in choice_ids,
             'Tool choices need one unique known stepId.')
        choice_ids.add(choice['stepId'])
        need(choice.get('status') in {'SUPPORTED', 'SUBSTITUTE', 'UNSUPPORTED', 'DEVELOPMENT_PENDING'}, 'Invalid capability status.')
        need(choice.get('reason') and isinstance(choice.get('tools'), list), 'Tool choice needs tools and reason.')
        refs = choice.get('evidenceRefs', [])
        need(isinstance(refs, list) and all(r in evidence for r in refs), 'Unknown tool capability evidence.')
        if choice['status'] in {'SUPPORTED', 'SUBSTITUTE'}:
            need(bool(choice['tools']) and bool(refs), 'Supported choices require actual capability/schema observations.')
        if choice['status'] == 'SUBSTITUTE':
            need(choice.get('equivalence') and choice.get('limits'), 'Substitutes require equivalence conditions and limits.')
        if choice['status'] in {'UNSUPPORTED', 'DEVELOPMENT_PENDING'}:
            need(choice.get('alternativesConsidered'), 'Search supported alternatives before proposing development.')
        consent = choice.get('developmentConsentRef')
        if consent:
            need(consent in evidence and evidence[consent].get('actor') == 'USER', 'Development consent requires an explicit USER observation.')
        # Consent authorizes development; it does not make an unbuilt tool executable.
    root = intake['outputRoot'].get('value')
    blocking = [g for g in gaps if g['required'] and g['state'] not in {'VERIFIED', 'NOT_REQUIRED'}]
    ready = not questions and not blocking
    for key, step in steps.items():
        if step.get('kind', 'PROCESS') not in {'PROCESS', 'CHECK', 'RECHECK', 'REPAIR'} or step['state'] not in {'RUNNING', 'PRODUCED', 'VERIFIED'}:
            continue
        need(not questions, 'Confirm data, requirements and output location before production.')
        need(not any(not g.get('stepIds') or key in g['stepIds'] for g in blocking), 'Missing required data blocks dependent production.')
        choice = next((c for c in choices if c['stepId'] == key), None)
        need(choice is not None and choice['status'] in {'SUPPORTED', 'SUBSTITUTE'}, 'Production requires a supported, observed tool choice.')
    if root:
        base = PureWindowsPath(root)

        def contained_output(value, label):
            normalized = output_path(value)
            need(PureWindowsPath(normalized).is_relative_to(base),
                 label + ' is outside the confirmed output root.')
            if sys.platform == 'win32':
                # A D-to-D junction can pass the drive and lexical checks while
                # redirecting a future dataset outside the user's chosen folder.
                # resolve() also follows existing ancestors of not-yet-created outputs.
                try:
                    contained = Path(normalized).resolve().is_relative_to(Path(root).resolve())
                except (OSError, RuntimeError) as exc:
                    raise ValueError(label + ' containment could not be resolved.') from exc
                need(contained, label + ' resolves outside the confirmed output root.')

        for step in steps.values():
            for value in step.get('outputs', []):
                contained_output(value, 'Planned output')
        for artifact in record.get('presentation', {}).get('artifacts', []):
            if artifact.get('path'):
                contained_output(artifact['path'], 'Artifact')
    if record.get('execution', {}).get('status') == 'COMPLETED':
        need(ready, 'Completion cannot hide unresolved intake/data needs.')
    return {**intake, 'status': 'READY' if ready else 'WAITING_USER', 'questions': questions}
