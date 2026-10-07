"""Standalone planning arithmetic; does not execute or accept product work."""
from __future__ import annotations
import math

import json
from datetime import date
from fractions import Fraction
from pathlib import Path


def ceil_fraction(value: Fraction) -> int:
    return -(-value.numerator // value.denominator)


def numeric(value, label: str, minimum: Fraction = Fraction(0)) -> Fraction:
    if isinstance(value, bool) or not isinstance(value, (int, float, str)):
        raise ValueError(f'{label}: finite numeric value required')
    try:
        result = Fraction(str(value))
    except (ValueError, ZeroDivisionError) as error:
        raise ValueError(f'{label}: finite numeric value required') from error
    if result < minimum:
        raise ValueError(f'{label}: value below {minimum}')
    return result


def unique_refs(values: list, label: str) -> set:
    if not isinstance(values, list) or any(not isinstance(v, str) or not v for v in values):
        raise ValueError(f'{label}: list of nonempty identities required')
    result = set(values)
    if len(result) != len(values):
        raise ValueError(f'{label}: duplicate identity')
    return result


def analyze_service_capacity(capacity: dict | None, classification: dict | None) -> dict:
    """Estimate remaining serial service loads; never proves a project deadline."""
    unknown = {'status': 'UNKNOWN', 'missingFields': [], 'resourcePoolRows': [],
               'fullProjectFeasibility': 'UNKNOWN', 'isCapacityProof': False,
               'actualProductAcceptance': 'NOT_RUN'}
    if not isinstance(capacity, dict) or not isinstance(classification, dict):
        unknown['missingFields'] = ['actualServiceCapacity', 'qualificationStrata']
        return unknown
    required_capacity = ['measurementWindowStart', 'measurementWindowEnd',
                         'capacityWindowStart', 'capacityWindowEnd',
                         'remainingQualificationUnitRefs', 'resourcePools', 'stages', 'evidenceRefs']
    required_classification = ['classificationVersion', 'remainingQualificationUnitRefs',
                               'assignments', 'strata']
    missing = [f'actualServiceCapacity.{k}' for k in required_capacity if capacity.get(k) is None]
    missing += [f'qualificationStrata.{k}' for k in required_classification
                if classification.get(k) is None]
    if missing:
        unknown['missingFields'] = missing
        return unknown
    for prefix in ['measurementWindow', 'capacityWindow']:
        start, end = (date.fromisoformat(capacity[prefix + suffix]) for suffix in ['Start', 'End'])
        if start > end:
            raise ValueError(f'{prefix}: reversed window')
    if not capacity['evidenceRefs']:
        missing.append('actualServiceCapacity.evidenceRefs')
    remaining = unique_refs(capacity['remainingQualificationUnitRefs'], 'remainingQualificationUnitRefs')
    if remaining != unique_refs(classification['remainingQualificationUnitRefs'],
                                'qualificationStrata.remainingQualificationUnitRefs'):
        raise ValueError('Classification must use the same authoritative remaining qualification set')
    strata = classification['strata']
    if not isinstance(strata, list):
        raise ValueError('strata must be a list')
    pools = capacity['resourcePools']
    stages = capacity['stages']
    assignments = classification['assignments']
    for rows, label, identity_fields in [
        (strata, 'strata', ['stratumId']),
        (assignments, 'assignments', ['unitRef', 'primaryStratumId']),
        (pools, 'resourcePools', ['poolId', 'physicalResourceIdentity']),
        (stages, 'stages', ['stageId', 'poolId', 'unitCostKey', 'fixedCostKey'])
    ]:
        if not isinstance(rows, list):
            raise ValueError(label + ': list required')
        for index, row in enumerate(rows):
            if not isinstance(row, dict):
                raise ValueError(label + ': each row must be an object')
            missing += [f'{label}[{index}].{field}' for field in identity_fields if row.get(field) is None]
    if missing:
        unknown['missingFields'] = sorted(set(missing))
        return unknown
    ids = [s['stratumId'] for s in strata]
    unique_refs(ids, 'stratumId')
    unique_refs([a['unitRef'] for a in assignments], 'assignments.unitRef')
    if {a['unitRef'] for a in assignments} != remaining:
        raise ValueError('Exactly one primary stratum must cover every remaining unit, with no extras')
    counts = {stratum_id: 0 for stratum_id in ids}
    for assignment in assignments:
        primary = assignment.get('primaryStratumId')
        if primary not in counts:
            raise ValueError(f'Unknown primary stratum: {primary}')
        unique_refs(assignment.get('riskTags', []), 'riskTags')
        counts[primary] += 1
    if not isinstance(pools, list) or not pools or not isinstance(stages, list) or not stages:
        raise ValueError('Nonempty resourcePools and stages required')
    pool_ids = [p['poolId'] for p in pools]
    unique_refs(pool_ids, 'poolId')
    unique_refs([p['physicalResourceIdentity'] for p in pools], 'physicalResourceIdentity')
    stage_ids = [s['stageId'] for s in stages]
    unique_refs(stage_ids, 'stageId')
    used_cost_keys = set()

    def claim_cost_keys(values: list, label: str) -> None:
        keys = unique_refs(values, label)
        if keys & used_cost_keys:
            raise ValueError(f'{label}: duplicate costKey would count the same cost twice')
        used_cost_keys.update(keys)

    def measured(row: dict, field: str, label: str) -> Fraction | None:
        if row.get(field) is None:
            missing.append(label + '.' + field)
            return None
        return numeric(row[field], label + '.' + field)

    pool_info = {}
    for pool in pools:
        label = 'resourcePools.' + pool['poolId']
        for field in ['reservedCostKeys', 'backlogCostKeysOutsideUnitCosts']:
            if pool.get(field) is None:
                missing.append(label + '.' + field)
            else:
                claim_cost_keys(pool[field], label + '.' + field)
        if not pool.get('evidenceRefs'):
            missing.append(label + '.evidenceRefs')
        fields = ['availableMinutes', 'unavailableMinutes', 'reservedNonQualificationMinutes',
                  'reservedControlAndReconciliationMinutes', 'backlogMinutesOutsideUnitCosts']
        values = {field: measured(pool, field, label) for field in fields}
        if all(value is not None for value in values.values()):
            reserved = values['reservedNonQualificationMinutes'] + values['reservedControlAndReconciliationMinutes']
            if reserved and not pool.get('reservedCostKeys'):
                raise ValueError(label + ': nonzero reservations require distinct costKeys')
            if values['backlogMinutesOutsideUnitCosts'] and not pool.get('backlogCostKeysOutsideUnitCosts'):
                raise ValueError(label + ': backlog outside unit costs needs its own costKeys')
            net = values['availableMinutes'] - values['unavailableMinutes'] - reserved
        else:
            net = None
        pool_info[pool['poolId']] = {'net': net, 'demand': values['backlogMinutesOutsideUnitCosts'],
                                     'stageRows': [], 'input': values}
    for stage in stages:
        stage_id, pool_id = stage['stageId'], stage['poolId']
        if pool_id not in pool_info:
            raise ValueError(f'{stage_id}: unknown physical pool {pool_id}')
        claim_cost_keys([stage['unitCostKey'], stage['fixedCostKey']], 'stages.' + stage_id)
        fixed = measured(stage, 'remainingFixedMinutes', 'stages.' + stage_id)
        demand = fixed
        group_rows = []
        for stratum in strata:
            stratum_id = stratum['stratumId']
            count = counts[stratum_id]
            if count == 0:
                continue
            if stratum.get('observedSampleSize') is None:
                missing.append('strata.' + stratum_id + '.observedSampleSize')
            elif isinstance(stratum['observedSampleSize'], bool) or not isinstance(stratum['observedSampleSize'], int) or stratum['observedSampleSize'] < 1:
                raise ValueError(f'{stratum_id}: nonempty observed sample required')
            costs = stratum.get('stageCosts')
            if costs is None:
                missing.append('strata.' + stratum_id + '.stageCosts')
                demand = None
                continue
            unique_refs([cost['stageId'] for cost in costs], 'strata.' + stratum_id + '.stageIds')
            known_stage_ids = {cost['stageId'] for cost in costs}
            if known_stage_ids - set(stage_ids):
                raise ValueError(f'{stratum_id}: unknown stage in cost rows')
            if stage_id not in known_stage_ids:
                missing.append('strata.' + stratum_id + '.' + stage_id + '.stageCost')
                demand = None
                continue
            cost = next(c for c in costs if c['stageId'] == stage_id)
            label = 'strata.' + stratum_id + '.' + stage_id
            if not cost.get('evidenceRefs'):
                missing.append(label + '.evidenceRefs')
            method = cost.get('costAllocation')
            if method == 'NET_ACCEPTED_ALL_INCLUSIVE':
                if cost.get('minutesPerAttempt') is not None or cost.get('acceptanceRate') is not None:
                    raise ValueError(label + ': all-inclusive net cost cannot also charge attempts/rework')
                if fixed is not None and fixed != 0:
                    raise ValueError(label + ': fixed setup already allocated in net cost; do not add it again')
                per_unit = measured(cost, 'netAcceptedServiceMinutes', label)
            elif method == 'ATTEMPT_ACCEPTANCE_MODEL':
                if cost.get('netAcceptedServiceMinutes') is not None:
                    raise ValueError(label + ': attempt cost cannot also charge a net-item cost')
                attempt = measured(cost, 'minutesPerAttempt', label)
                rate = measured(cost, 'acceptanceRate', label)
                if rate is not None and not (0 < rate <= 1):
                    raise ValueError(label + ': acceptanceRate must lie in (0,1]')
                per_unit = None if attempt is None or rate is None else attempt / rate
            elif method is None:
                missing.append(label + '.costAllocation')
                per_unit = None
            else:
                raise ValueError(label + ': unknown cost allocation method')
            if per_unit is None:
                demand = None
            elif demand is not None:
                demand += count * per_unit
            group_rows.append({'stratumId': stratum_id, 'remainingUniqueUnits': count,
                               'estimatedMinutesPerNetAccepted': None if per_unit is None else float(per_unit),
                               'estimatedLoadMinutes': None if per_unit is None else float(count * per_unit)})
        info = pool_info[pool_id]
        info['demand'] = None if info['demand'] is None or demand is None else info['demand'] + demand
        info['stageRows'].append({'stageId': stage_id, 'stratumRows': group_rows,
                                 'estimatedDemandMinutes': None if demand is None else float(demand)})
    if missing:
        unknown['missingFields'] = sorted(set(missing))
        return unknown
    rows = []
    for pool_id, info in pool_info.items():
        demand, net = info['demand'], info['net']
        rows.append({'poolId': pool_id, 'netAvailableMinutesExact': str(net),
                     'estimatedDemandMinutesExact': str(demand),
                     'netAvailableMinutes': float(net), 'estimatedDemandMinutes': float(demand),
                     'estimatedLoadGapMinutes': float(demand - net),
                     'estimatedOverloadMinutes': float(max(Fraction(0), demand - net)),
                     'stageRows': info['stageRows'], 'capacityCountedOnce': True})
    return {'status': 'ESTIMATED_SERVICE_OVERLOAD' if any(r['estimatedOverloadMinutes'] > 0 for r in rows)
            else 'SERVICE_BOUNDS_WITHIN_ESTIMATES', 'missingFields': [], 'resourcePoolRows': rows,
            'remainingUniqueUnits': len(remaining), 'primaryStratumCounts': counts,
            'fullProjectFeasibility': 'UNKNOWN', 'isCapacityProof': False,
            'actualProductAcceptance': 'NOT_RUN',
            'caveat': 'Observed means are estimates; calendar/arrival/peak load and full activities still require a schedule witness'}


def analyze_lifecycle_costs(model: dict) -> dict:
    ledger = model.get('lifecycleCostLedger')
    if not ledger or not ledger.get('entries'):
        return {'status': 'UNKNOWN', 'wholeProjectWitness': False, 'missingFields': ['lifecycleCostLedger.entries']}
    if not ledger.get('notAddedTwiceToOriginalWiTotals') or not ledger.get('notContingencyWork'):
        raise ValueError('Lifecycle details must be original WI costs, not duplicate or contingency work')
    owners = {item['id']: item['owner'] for item in model['workItems']}
    keys, missing = set(), []
    fields = ['engineeringHours', 'professionalHours', 'hostServiceMinutes', 'independentQaMinutes',
              'integrationMinutes', 'unallocatedWaitMinutes', 'peakOwnedDiskBytes']
    for entry in ledger['entries']:
        key = entry['costKey']
        wi = entry['workItemId']
        if key in keys or wi not in owners or entry['owner'] != owners[wi] or not key.startswith(wi + ':'):
            raise ValueError('Invalid lifecycle cost identity/owner: ' + key)
        keys.add(key)
        for field in fields:
            value = entry.get(field)
            if value is None:
                missing.append(key + '.' + field)
            elif isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
                raise ValueError('Invalid lifecycle cost: ' + key + '.' + field)
        if not entry.get('evidenceRefs'):
            missing.append(key + '.evidenceRefs')
    return {'status': 'UNKNOWN' if missing else 'MEASURED_LEDGER_NOT_PROJECT_WITNESS',
            'entryCount': len(keys), 'missingFields': missing, 'wholeProjectWitness': False,
            'originalWiDetailOnly': True, 'noInventedTotalsOrDates': True}


def analyze(model: dict) -> dict:
    example_inputs = model['examplesOnly']
    qualification_weeks_input = numeric(example_inputs['qualificationWindowWeeks'], 'qualificationWindowWeeks', Fraction(1))
    review_minutes = 60 * numeric(example_inputs['reviewHoursPerWeek'], 'reviewHoursPerWeek')
    host_minutes = 60 * numeric(example_inputs['hostHoursPerWeek'], 'hostHoursPerWeek')
    preparation_minutes = 60 * numeric(example_inputs['preparationHoursPerWeek'], 'preparationHoursPerWeek')
    items = model['workItems']
    by_id = {item['id']: item for item in items}
    if len(by_id) != 30 or len(items) != 30:
        raise ValueError('The complete source must contain exactly 30 unique work items')
    visiting, completed = set(), set()

    def visit(item_id: str) -> None:
        if item_id in visiting:
            raise ValueError(f'Dependency cycle at {item_id}')
        if item_id in completed:
            return
        visiting.add(item_id)
        for predecessor in by_id[item_id]['dependencies']:
            if predecessor not in by_id:
                raise ValueError(f'Missing predecessor {predecessor}')
            visit(predecessor)
        visiting.remove(item_id)
        completed.add(item_id)

    for item_id in by_id:
        visit(item_id)
    owners = {owner: sum(item['owner'] == owner for item in items)
              for owner in ['E1', 'E2', 'E3', 'E4', 'E5']}
    quantity_rows = []
    for example in model['qualificationExamplesOnly']:
        pool = example['K_GP'] + example['K_sem'] - example['K_overlap']
        remaining = pool - example['Q0_union']
        required = ceil_fraction(Fraction(remaining) / qualification_weeks_input)
        quantity_rows.append({**example, 'K_union': pool, 'requiredNetAcceptedPerWeek': required,
                              'reviewMinuteUpperBoundAt25Hours': float(review_minutes / required),
                              'hostMinuteUpperBoundAt60Hours': float(host_minutes / required)})
    review_cost = (numeric(example_inputs['reviewFixedMinutesPerBatch'], 'reviewFixedMinutesPerBatch')
                   / numeric(example_inputs['reviewBatchSize'], 'reviewBatchSize', Fraction(1))
                   + numeric(example_inputs['reviewMinutesPerItem'], 'reviewMinutesPerItem')
                   + numeric(example_inputs['reviewReworkMinutesPerNetItem'], 'reviewReworkMinutesPerNetItem'))
    review_weekly = int(review_minutes // review_cost)
    review_total = int((review_minutes * qualification_weeks_input) // review_cost)
    host_weekly = int(host_minutes // numeric(example_inputs['hostMinutesPerNetItem'], 'hostMinutesPerNetItem', Fraction(1, 1000000)))
    preparation_weekly = int(preparation_minutes // numeric(example_inputs['preparationMinutesPerNetItem'], 'preparationMinutesPerNetItem', Fraction(1, 1000000)))
    worst_example_pool = max(row['K_union'] for row in quantity_rows)
    lower_bounds = []
    for example in model['lowerBoundExamplesOnly']:
        qualification_weeks = Fraction(example['K_union'] - example['Q0_union'], example['netRatePerWeek'])
        weeks = (numeric(example_inputs['lowerBoundPrefixWeeks'], 'lowerBoundPrefixWeeks')
                 + max(qualification_weeks, numeric(example_inputs['lowerBoundOtherImplementationWeeks'], 'lowerBoundOtherImplementationWeeks'))
                 + numeric(example_inputs['lowerBoundFinalFunctionalWeeks'], 'lowerBoundFinalFunctionalWeeks')
                 + numeric(example_inputs['lowerBoundReleaseWeeks'], 'lowerBoundReleaseWeeks'))
        lower_bounds.append({**example, 'qualificationWeeks': float(qualification_weeks),
                             'lowerBoundWeeks': float(weeks),
                             'lowerBoundCalendarDaysCeil': ceil_fraction(weeks * 7),
                             'isWholeProjectForecast': False})
    residual = []
    residual_days = example_inputs['qualificationEndDay'] - example_inputs['residualDayEnd']
    if residual_days <= 0:
        raise ValueError('Residual example needs a positive qualification window')
    for accepted in example_inputs['residualAcceptedCounts']:
        remaining = example_inputs['residualTarget'] - accepted
        residual.append({'dayEnd': example_inputs['residualDayEnd'], 'target': example_inputs['residualTarget'], 'currentValidAccepted': accepted,
                         'remaining': remaining, 'qualificationDaysLeft': residual_days,
                         'requiredNetAcceptedPerWeek': ceil_fraction(Fraction(7 * remaining, residual_days))})
    late = example_inputs['lateWI30']
    functional_earliest = late['prerequisiteEndDay'] + late['implementationDays']
    pipeline = example_inputs['pipeline']
    integrated, reviewed = 0, 0
    for _ in range(pipeline['packages']):
        integrated += pipeline['integrationMinutesEach']
        reviewed = max(reviewed, integrated) + pipeline['reviewMinutesEach']
    long_tail = example_inputs['sharedPoolLongTail']
    return {
        'status': 'PLANNING_ARITHMETIC_CHECKED_NOT_PRODUCT_ACCEPTANCE',
        'sourceRevision': model['sourceRevision'],
        'actualStartDate': model['actualStartDate'],
        'actualObservedCosts': model['actualObservedCosts'],
        'actualActivityLedger': model['actualActivityLedger'],
        'actualScheduleWitness': model['actualScheduleWitness'],
        'actualProjectFeasibility': 'UNKNOWN',
        'actualProductAcceptance': 'NOT_RUN',
        'full60DayCapacityProven': False,
        'lifecycleCostAssessment': analyze_lifecycle_costs(model),
        'actualServiceCapacityAssessment': analyze_service_capacity(model.get('actualServiceCapacity'), model.get('qualificationStrata')),
        'sharedPoolLongTailExampleOnly': analyze_service_capacity(long_tail['capacity'], long_tail['classification']),
        'workItemCount': len(items), 'ownerCounts': owners, 'dependenciesAcyclic': True,
        'originalGateCount': len(model['originalGateIds']),
        'additionalGateCount': len(model['additionalGateIds']),
        'qualificationExamplesOnly': quantity_rows,
        'serviceBoundsExamplesOnly': {
            'reviewCostMinutesExact': str(review_cost), 'reviewWeeklyUpperBound': review_weekly,
            'hostWeeklyUpperBound': host_weekly, 'preparationWeeklyUpperBound': preparation_weekly,
            'jointResourceSchedulingStillRequired': True,
            'fourWeekNonCarryoverReviewUpperBound': int(review_weekly * qualification_weeks_input),
            'fourWeekNonCarryoverMinimumOverlap': int(worst_example_pool - review_weekly * qualification_weeks_input),
            'fourWeekCarryoverReviewUpperBound': review_total,
            'fourWeekCarryoverMinimumOverlap': worst_example_pool - review_total,
            'inputsAreExamplesOnly': True,
            'ratesFromObservedMeansWouldStillBeEstimates': True,
            'isCapacityProof': False},
        'D28ResidualExamplesOnly': residual,
        'lowerBoundExamplesOnly': lower_bounds,
        'lateWI30CounterexampleNotMeasured': {
            **late, 'functionalEarliestDay': functional_earliest,
            'releaseEarliestDay': functional_earliest + late['packageDaysAfterFunctional']},
        'fivePackagePipelineExampleNotMeasured': {
            **pipeline, 'lastIntegrationFinishMinutes': integrated,
            'lastReviewFinishMinutes': reviewed},
        'noProductCommandsOrLiveEffects': True,
    }


if __name__ == '__main__':
    directory = Path(__file__).resolve().parent
    if directory.drive.upper() != 'D:':
        raise RuntimeError('Project planning writes must remain on D drive')
    model = json.loads((directory / '工期计算模型.json').read_text(encoding='utf-8'))
    result = analyze(model)
    (directory / '复算结果.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'status': result['status'], 'workItems': result['workItemCount'],
                      'owners': result['ownerCounts'], 'projectFeasibility': 'UNKNOWN'}, ensure_ascii=False))
