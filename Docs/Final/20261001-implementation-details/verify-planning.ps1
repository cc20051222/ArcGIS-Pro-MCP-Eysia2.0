param([switch]$WriteReport)
$ErrorActionPreference = 'Stop'
$detailRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$projectRoot = [IO.Path]::GetFullPath((Join-Path $detailRoot '..\..\..'))
if ([IO.Path]::GetPathRoot($detailRoot) -ne 'D:\') { throw 'Planning validator must remain on D drive.' }
$designPath = Join-Path $detailRoot 'DESIGN_SPECIFICATION.json'
$design = Get-Content -LiteralPath $designPath -Raw -Encoding UTF8 | ConvertFrom-Json
$baseline = Get-Content -LiteralPath (Join-Path $detailRoot 'preservation-baseline.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$checks = [Collections.Generic.List[object]]::new()
function Add-DesignCheck([string]$checkName, [bool]$ok, [object]$detail) {
    $checks.Add([ordered]@{ name=$checkName; passed=$ok; detail=$detail })
}
Add-DesignCheck 'explicit-planning-only' ($design.kind -eq 'planning-implementation-design-only' -and -not $design.isExecutableMcpSchema -and @($design.runtimeEvidence).Count -eq 0) 'Not MCP schema or runtime evidence'
Add-DesignCheck 'contract-service-work-counts' (@($design.contracts).Count -eq 12 -and @($design.services).Count -eq 12 -and @($design.workItems).Count -eq 30) 'DC=12; SV=12; WI=30'
Add-DesignCheck 'state-and-experiment-counts' (@($design.taskMachine.states).Count -eq 14 -and @($design.stepMachine.states).Count -eq 15 -and @($design.effectStates).Count -eq 7 -and @($design.innovationExperiments).Count -eq 6) 'task=14; step=15; effect=7; experiment=6'
foreach ($kind in @('taskMachine','stepMachine')) {
    $machine = $design.$kind
    $unknownStates = [Collections.Generic.List[string]]::new()
    foreach ($edge in $machine.transitions.PSObject.Properties) {
        if ($edge.Name -notin $machine.states) { $unknownStates.Add($edge.Name) }
        foreach ($target in $edge.Value) { if ($target -notin $machine.states) { $unknownStates.Add([string]$target) } }
    }
    $missingRows = @($machine.states | Where-Object { $_ -notin $machine.transitions.PSObject.Properties.Name })
    Add-DesignCheck ($kind + '-transitions-resolve') ($unknownStates.Count -eq 0 -and $missingRows.Count -eq 0) ([ordered]@{ unknown=$unknownStates.ToArray(); missingRows=$missingRows })
}
Add-DesignCheck 'no-success-redispatch-edge' (@($design.stepMachine.transitions.RECORDED).Count -eq 0 -and 'EXECUTING' -notin $design.stepMachine.transitions.RECORD_PENDING -and 'VALIDATED' -notin $design.stepMachine.transitions.UNKNOWN_EFFECT -and 'EXECUTING' -notin $design.stepMachine.transitions.PARTIAL_EFFECT) 'Recorded and pending receipt never redispatch; unknown must reconcile'
Add-DesignCheck 'dispatch-guard-and-failure-receipts' ($design.stepMachine.guards.EXECUTING -match 'approval/policy/license' -and $design.stepMachine.guards.EXECUTING -match 'resource reservation' -and $design.stepMachine.guards.EXECUTING -match 'no unresolved effects' -and $design.stepMachine.guards.FAILURE_RECEIPTS -match 'durable result') 'Required proposal guards explicitly present; not an implementation proof'
$workIds = @($design.workItems.id)
$dependencyFailures = [Collections.Generic.List[string]]::new()
foreach ($work in $design.workItems) {
    foreach ($dep in $work.dependsOn) { if ($dep -notin $workIds -or $dep -eq $work.id) { $dependencyFailures.Add($work.id + ':' + $dep) } }
    foreach ($contract in $work.contractRefs) { if ($contract -notin $design.contracts.id) { $dependencyFailures.Add($work.id + ':' + $contract) } }
    foreach ($service in $work.serviceRefs) { if ($service -notin $design.services.id) { $dependencyFailures.Add($work.id + ':' + $service) } }
    if ($work.status -ne 'PLANNED_NOT_DISPATCHED' -or @($work.runtimeEvidence).Count -ne 0) { $dependencyFailures.Add($work.id + ':invalid-evidence-status') }
}
$resolved = [Collections.Generic.HashSet[string]]::new()
$topology = [Collections.Generic.List[string]]::new()
$remaining = @($design.workItems)
while ($remaining.Count -gt 0) {
    $ready = @($remaining | Where-Object { @($_.dependsOn | Where-Object { -not $resolved.Contains($_) }).Count -eq 0 })
    if ($ready.Count -eq 0) { break }
    foreach ($work in $ready) { [void]$resolved.Add($work.id); $topology.Add($work.id) }
    $remaining = @($remaining | Where-Object { -not $resolved.Contains($_.id) })
}
Add-DesignCheck 'work-references-and-acyclic' ($dependencyFailures.Count -eq 0 -and $remaining.Count -eq 0 -and @($workIds | Select-Object -Unique).Count -eq 30) ([ordered]@{ failures=$dependencyFailures.ToArray(); topologicalOrder=$topology.ToArray(); cyclicRemaining=@($remaining | ForEach-Object { $_.id }) })
$missingPrerequisites = [Collections.Generic.List[string]]::new()
foreach ($writeId in @('WI14','WI17','WI19','WI20','WI21','WI24','WI25','WI26')) {
    $closure = [Collections.Generic.HashSet[string]]::new()
    $queue = [Collections.Generic.Queue[string]]::new()
    $queue.Enqueue($writeId)
    while ($queue.Count -gt 0) {
        $nextId = $queue.Dequeue()
        if (-not $closure.Add($nextId)) { continue }
        $nextWork = $design.workItems | Where-Object id -eq $nextId
        foreach ($dep in $nextWork.dependsOn) { $queue.Enqueue($dep) }
    }
    foreach ($required in @('WI01','WI02','WI03','WI04','WI10')) { if (-not $closure.Contains($required)) { $missingPrerequisites.Add($writeId + ':' + $required) } }
}
Add-DesignCheck 'actual-write-work-reliability-prerequisites' ($missingPrerequisites.Count -eq 0) $missingPrerequisites.ToArray()
$experimentBad = @($design.innovationExperiments | Where-Object { $_.workItem -notin $workIds -or @($_.dependsOn | Where-Object { $_ -notin $workIds }).Count -ne 0 })
Add-DesignCheck 'experiment-references' ($experimentBad.Count -eq 0) 'All six refer to proposed work items'
$backlogText = Get-Content -LiteralPath (Join-Path $detailRoot 'MODULE_INTERFACES_AND_BACKLOG.md') -Raw -Encoding UTF8
$docIds = @([regex]::Matches($backlogText, '\| (WI\d{2}) /') | ForEach-Object { $_.Groups[1].Value })
Add-DesignCheck 'markdown-work-items-match-json' ($docIds.Count -eq 30 -and @(Compare-Object ($docIds | Sort-Object) ($workIds | Sort-Object)).Count -eq 0) '30 matching work item rows'
$planning = $design.goldenSpecifications | Where-Object id -eq 'PL-GOLD-01'
$unionSet = [Collections.Generic.HashSet[string]]::new()
$facilityTotals = @()
foreach ($coverage in $planning.coverageSets) {
    $sum = 0
    foreach ($entity in $coverage) { [void]$unionSet.Add($entity); $sum += $planning.weights.PSObject.Properties[$entity].Value }
    $facilityTotals += $sum
}
$totalPopulation = ($planning.weights.PSObject.Properties.Value | Measure-Object -Sum).Sum
$unionPopulation = 0
foreach ($entity in $unionSet) { $unionPopulation += $planning.weights.PSObject.Properties[$entity].Value }
$overlapPopulation = ($facilityTotals | Measure-Object -Sum).Sum - $unionPopulation
$planningOk = $totalPopulation -eq $planning.expected.totalPopulation -and $unionPopulation -eq $planning.expected.unionPopulation -and $overlapPopulation -eq $planning.expected.overlapPopulation -and ($totalPopulation-$unionPopulation) -eq $planning.expected.uncoveredPopulation -and @(Compare-Object $facilityTotals @($planning.expected.perFacilityCovered)).Count -eq 0
Add-DesignCheck 'PL-GOLD-01-independent-arithmetic' $planningOk ([ordered]@{ total=$totalPopulation; perFacility=$facilityTotals; overlap=$overlapPopulation; union=$unionPopulation; coveragePercent=(100*$unionPopulation/$totalPopulation); scope='fixture-specification-arithmetic-only' })
$knownTotal = ($planning.weights.PSObject.Properties | Where-Object Name -ne $planning.negativeVariant.missingPopulationEntity | Measure-Object -Property Value -Sum).Sum
Add-DesignCheck 'PL-NEG-01-null-and-denominator' ($null -eq $planning.negativeVariant.overallPopulation -and $null -eq $planning.negativeVariant.overallCoverage -and $knownTotal -eq $planning.negativeVariant.adoptedKnownRecordsOnly.denominator -and $planning.negativeVariant.adoptedKnownRecordsOnly.mustNotBeLabelledOverall) 'D missing: overall unknown; known-records-only ratio must be relabelled'
$science = $design.goldenSpecifications | Where-Object id -eq 'SCI-GOLD-01'
$matrix = @(@(0,0),@(0,0))
$valid = 0
$changed = 0
for ($row = 0; $row -lt $science.t0.Count; $row++) {
    for ($col = 0; $col -lt $science.t0[$row].Count; $col++) {
        $v0 = $science.t0[$row][$col]; $v1 = $science.t1[$row][$col]
        if ($v0 -eq $science.noData -or $v1 -eq $science.noData) { continue }
        $i = [array]::IndexOf([object[]]$science.classOrder,[object]$v0)
        $j = [array]::IndexOf([object[]]$science.classOrder,[object]$v1)
        if ($i -lt 0 -or $j -lt 0) { throw 'Invalid synthetic class' }
        $matrix[$i][$j]++
        $valid++
        if ($v0 -ne $v1) { $changed++ }
    }
}
$rowMarginal = @(($matrix[0][0]+$matrix[0][1]),($matrix[1][0]+$matrix[1][1]))
$columnMarginal = @(($matrix[0][0]+$matrix[1][0]),($matrix[0][1]+$matrix[1][1]))
$netArea = @( (($columnMarginal[0]-$rowMarginal[0])*$science.cellAreaSquareMeters), (($columnMarginal[1]-$rowMarginal[1])*$science.cellAreaSquareMeters) )
$scienceOk = (ConvertTo-Json -InputObject $matrix -Compress) -eq (ConvertTo-Json -InputObject $science.expected.transitionCounts -Compress) -and $valid -eq $science.expected.validCells -and $changed -eq $science.expected.changedCells -and ($valid*$science.cellAreaSquareMeters) -eq $science.expected.validAreaSquareMeters -and ($changed*$science.cellAreaSquareMeters) -eq $science.expected.changedAreaSquareMeters -and @(Compare-Object $rowMarginal @($science.expected.rowMarginal)).Count -eq 0 -and @(Compare-Object $columnMarginal @($science.expected.columnMarginal)).Count -eq 0 -and @(Compare-Object $netArea @($science.expected.netAreaChangeSquareMeters)).Count -eq 0
Add-DesignCheck 'SCI-GOLD-01-independent-arithmetic' $scienceOk ([ordered]@{ transition=$matrix; validCells=$valid; changedCells=$changed; validSquareMeters=($valid*$science.cellAreaSquareMeters); changedSquareMeters=($changed*$science.cellAreaSquareMeters); netArea=$netArea; scope='fixture-specification-arithmetic-only' })
$norm = $design.goldenSpecifications | Where-Object id -eq 'NORM-GOLD-01'
$counts = [ordered]@{ PASS=0; FAIL=0; UNKNOWN=0; NOT_APPLICABLE=0 }
$normFailures = [Collections.Generic.List[string]]::new()
$determinedApplicable = 0
$evaluatedApplicable = 0
foreach ($record in $norm.records) {
    if ($record.region -eq 'IN' -and -not $record.exempt) { $determinedApplicable++; if ($null -ne $record.value) { $evaluatedApplicable++ } }
    if ($record.region -eq 'OUT' -or $record.exempt) { $state = 'NOT_APPLICABLE' }
    elseif ($null -eq $record.region -or $null -eq $record.value) { $state = 'UNKNOWN' }
    else {
        if ($record.unit -eq 'cm') { $height = $record.value / $norm.rule.centimetersPerMeter }
        elseif ($record.unit -eq 'm') { $height = $record.value }
        else { throw 'Unknown synthetic unit' }
        $state = if ($height -le $norm.rule.maximumHeightMeters) { 'PASS' } else { 'FAIL' }
    }
    $counts[$state]++
    if ($state -ne $record.expected) { $normFailures.Add($record.id) }
}
foreach ($state in $counts.Keys) { if ($counts[$state] -ne $norm.expectedCounts.$state) { $normFailures.Add($state) } }
Add-DesignCheck 'NORM-GOLD-01-independent-logic' ($normFailures.Count -eq 0 -and $determinedApplicable -eq $norm.determinedApplicableRecords -and $evaluatedApplicable -eq $norm.evaluatedApplicableRecords -and $norm.rule.effectiveTimeAssumption -match 'All seven') ([ordered]@{ counts=$counts; determinedApplicable=$determinedApplicable; evaluatedApplicable=$evaluatedApplicable; scope='synthetic-practice-rule-only-not-regulation-validation' })
$planDrift = @($baseline.plans | Where-Object { (Get-FileHash -LiteralPath (Join-Path $projectRoot $_.path) -Algorithm SHA256).Hash -ne $_.sha256 })
Add-DesignCheck 'original-plans-preserved' ($planDrift.Count -eq 0 -and @($baseline.plans).Count -eq 15) ([ordered]@{ observedFiles=@($baseline.plans).Count; changed=@($planDrift | ForEach-Object { $_.path }) })
$sourceDrift = @($baseline.source | Where-Object { -not (Test-Path -LiteralPath (Join-Path $projectRoot $_.path)) -or (Get-FileHash -LiteralPath (Join-Path $projectRoot $_.path) -Algorithm SHA256).Hash -ne $_.sha256 })
$currentSourcePaths = @(rg --files Source | Sort-Object)
$sourceSetDrift = @(Compare-Object @($baseline.source.path | Sort-Object) $currentSourcePaths)
Add-DesignCheck 'observed-source-tree-preserved' ($sourceDrift.Count -eq 0 -and $sourceSetDrift.Count -eq 0) ([ordered]@{ observedFiles=@($baseline.source).Count; changed=@($sourceDrift | ForEach-Object { $_.path }); setChanges=@($sourceSetDrift); note='rg-visible tree includes build payloads; distinct from commander 272-file aggregation' })
$missingLinks = [Collections.Generic.List[string]]::new()
$markdownFiles = @(Get-ChildItem -LiteralPath $detailRoot -File -Filter '*.md')
foreach ($file in $markdownFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($match in [regex]::Matches($content, '\[[^\]]*\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value
        if ($target -match '^https?://') { continue }
        $relativeTarget = ($target -split '#')[0]
        if ($relativeTarget.Length -eq 0) { continue }
        $resolvedTarget = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $relativeTarget))
        if ([IO.Path]::GetFileName($resolvedTarget) -eq 'DETAILS_VALIDATION.json' -and $WriteReport) { continue }
        if (-not (Test-Path -LiteralPath $resolvedTarget)) { $missingLinks.Add($file.Name + ':' + $target) }
    }
}
Add-DesignCheck 'relative-local-links' ($missingLinks.Count -eq 0) $missingLinks.ToArray()
$documents = @($markdownFiles | ForEach-Object { [ordered]@{ path=$_.Name; bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
$failed = @($checks | Where-Object { -not $_.passed })
$report = [ordered]@{
    kind='planning-details-validation-only'; date='2026-10-01'
    status=if($failed.Count -eq 0){'PASS_CANDIDATE'}else{'CHANGES_REQUIRED'}
    scope='Documentation/specification consistency and synthetic expected-value arithmetic; no product build/runtime/GP/PS evidence'
    checks=$checks.ToArray(); documents=$documents
    designSha256=(Get-FileHash -LiteralPath $designPath -Algorithm SHA256).Hash
    validatorSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    runtimeEvidence=@(); failedChecks=@($failed | ForEach-Object { $_.name })
}
if ($WriteReport) {
    [IO.File]::WriteAllText((Join-Path $detailRoot 'DETAILS_VALIDATION.json'), ($report | ConvertTo-Json -Depth 15), [Text.UTF8Encoding]::new($false))
}
[ordered]@{ status=$report.status; checks=$checks.Count; failed=@($failed | ForEach-Object { $_.name }); documents=$documents.Count; sourceObserved=@($baseline.source).Count; reportWritten=[bool]$WriteReport } | ConvertTo-Json -Compress
if ($failed.Count -gt 0) { exit 1 }
