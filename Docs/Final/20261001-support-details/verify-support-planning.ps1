param([switch]$WriteReport)
$ErrorActionPreference = 'Stop'
$detailRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$projectRoot = [IO.Path]::GetFullPath((Join-Path $detailRoot '..\..\..'))
if ([IO.Path]::GetPathRoot($detailRoot) -ne 'D:\') { throw 'Support planning validation must remain on D drive.' }
$spec = Get-Content -LiteralPath (Join-Path $detailRoot 'SUPPORT_DESIGN_SPECIFICATION.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$prior = Get-Content -LiteralPath (Join-Path $projectRoot 'Docs\Final\20261001-implementation-details\DESIGN_SPECIFICATION.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$baseline = Get-Content -LiteralPath (Join-Path $detailRoot 'preservation-baseline.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$checks = [Collections.Generic.List[object]]::new()
function Add-PlanningCheck([string]$name, [bool]$passed, [object]$detail) {
    $checks.Add([ordered]@{ name=$name; passed=$passed; detail=$detail })
}
function Same-Set([object[]]$left, [object[]]$right) {
    return ($left.Count -eq $right.Count -and @(Compare-Object ($left | Sort-Object) ($right | Sort-Object)).Count -eq 0)
}
Add-PlanningCheck 'explicit-planning-only' ($spec.kind -eq 'support-implementation-planning-only' -and $spec.status -eq 'PASS_CANDIDATE_PLANNING_ONLY' -and -not $spec.isExecutableMcpSchema -and @($spec.runtimeEvidence).Count -eq 0) 'Document consistency only; no runtime acceptance.'
Add-PlanningCheck 'baseline-root-and-write-probe' ($baseline.probePassed -and [IO.Path]::GetFullPath($baseline.projectRoot) -eq $projectRoot) $projectRoot
$workIds = @($prior.workItems.id)
$contractIds = @($spec.contracts.id)
$referenceFailures = [Collections.Generic.List[string]]::new()
foreach ($contract in $spec.contracts) {
    if ($contract.workItemRef -notin $workIds -or $contract.status -ne 'PROPOSED_INTERNAL_CONTRACT' -or @($contract.runtimeEvidence).Count -ne 0) { $referenceFailures.Add($contract.id) }
}
Add-PlanningCheck 'eight-internal-contracts-and-work-references' ($contractIds.Count -eq 8 -and @($contractIds | Select-Object -Unique).Count -eq 8 -and $referenceFailures.Count -eq 0) $referenceFailures.ToArray()
$routeFailures = @($spec.modelRoutes | Where-Object { $_.workItemRef -notin $workIds -or $_.status -ne 'PLANNED' })
$form = $spec.modelRoutes | Where-Object family -eq 'DeterministicRecipeForm'
$local = $spec.modelRoutes | Where-Object family -eq 'ExistingLocalHttp'
Add-PlanningCheck 'five-routes-without-extra-model-or-install-claim' (@($spec.modelRoutes).Count -eq 5 -and @($spec.modelRoutes.id | Select-Object -Unique).Count -eq 5 -and $routeFailures.Count -eq 0 -and $form.countsAsModel -eq $false -and $local.autoInstallAllowed -eq $false) 'Four provider families plus one deterministic form route, all planned.'
Add-PlanningCheck 'canonical-v2-ledger-enums' ((Same-Set @($spec.maturityStates) @('DECLARED','CONTRACT_DEFINED','IMPLEMENTED','RUNTIME_VERIFIED','E2E_VERIFIED','INDEPENDENTLY_ACCEPTED')) -and (Same-Set @($spec.availabilityStates) @('AVAILABLE','CONDITIONAL','UNAVAILABLE','UNKNOWN')) -and 'availabilityReason' -in $spec.supportEvidenceFields -and 'sourceEvidence' -in $spec.supportEvidenceFields) 'Maturity and availability remain separate; reasons and source reviews are separate fields.'
$families = @($spec.formatFamilies.id)
$actions = @($spec.formatActions)
$inventory = @($spec.formatActionInventory)
$inventoryFailures = [Collections.Generic.List[string]]::new()
foreach ($row in $inventory) {
    if ($row.formatId -notin $families -or $row.action -notin $actions -or $row.actionDecision -ne 'REVIEW_REQUIRED' -or $row.isReleasePromise -ne $false -or $row.maturity -ne 'DECLARED' -or $row.environmentAvailability -ne 'UNKNOWN' -or @($row.evidenceRefs).Count -ne 0) { $inventoryFailures.Add($row.id) }
}
$pairKeys = @($inventory | ForEach-Object { $_.formatId + '|' + $_.action })
$expectedPairs = @(foreach ($family in $families) { foreach ($action in $actions) { $family + '|' + $action } })
Add-PlanningCheck 'format-cross-product-is-candidate-pool-only' ($families.Count -eq 21 -and @($families | Select-Object -Unique).Count -eq 21 -and (Same-Set $actions @('READ','WRITE_NEW','QUERY','EDIT_OWNED','ROUND_TRIP')) -and $inventory.Count -eq 105 -and @($inventory.id | Select-Object -Unique).Count -eq 105 -and (Same-Set $pairKeys $expectedPairs) -and $inventoryFailures.Count -eq 0) ([ordered]@{families=21; candidateRows=105; qualifiedRows=0; failures=$inventoryFailures.ToArray()})
$profileFailures = @($inventory | Where-Object { -not (Same-Set @($_.profileKeyRequirements) @('formatVersion','adapterId','adapterVersion','supportedDomainDigest')) })
$unqualifiedFamilies = @($spec.formatFamilies | Where-Object { $_.exactVersions -ne 'REQUIRES_EXPLICIT_PROFILE' -or @($_.runtimeEvidence).Count -ne 0 })
Add-PlanningCheck 'format-version-adapter-domain-key' ((Same-Set @($spec.formatSupportKey) @('formatId','formatVersion','action','adapterId','adapterVersion','supportedDomainDigest')) -and $profileFailures.Count -eq 0 -and $unqualifiedFamilies.Count -eq 0) 'Exact profiles and real evidence are required before a support claim.'
$caseCounts = [ordered]@{}
$cases = @()
foreach ($group in $spec.caseGroups.PSObject.Properties) {
    $caseCounts[$group.Name] = @($group.Value).Count
    $cases += @($group.Value)
}
$badCaseScopes = @($cases | Where-Object { $_.scope -ne 'PROPOSED_TEST_CASE_NOT_EXECUTED' -or @($_.runtimeEvidence).Count -ne 0 })
Add-PlanningCheck '55-proposed-case-specifications-not-executed' ($cases.Count -eq 55 -and @($cases.id | Select-Object -Unique).Count -eq 55 -and $caseCounts.model -eq 22 -and $caseCounts.pack -eq 14 -and $caseCounts.format -eq 9 -and $caseCounts.environment -eq 10 -and $badCaseScopes.Count -eq 0) ([ordered]@{groups=$caseCounts; businessCasesExecuted=0})
$badHostExpectations = @($spec.caseGroups.model | Where-Object { $_.expectedUnapprovedHostDispatches -ne 0 -or $_.expectedDuplicateHostDispatches -ne 0 })
$badImportExpectations = @($spec.caseGroups.pack | Where-Object { $_.importCanDispatchHost -ne $false })
Add-PlanningCheck 'proposed-host-and-import-boundaries' ($badHostExpectations.Count -eq 0 -and $badImportExpectations.Count -eq 0) 'These are written expectations, not observed effects.'
$adoption = $spec.proposalAdoptionRules
Add-PlanningCheck 'active-slot-adoption-contract' ($adoption.durableAdoptionAndSlotCloseAreAtomic -eq $true -and $adoption.modelSwitchInvalidatesOldGeneration -eq $true -and $adoption.differentProposalIdsCanReuseAdoptedSlot -eq $false -and $adoption.currentRevisionsAloneProveActiveRequest -eq $false -and 'logicalProposalSlotId' -in $adoption.requiredIdentity -and 'requestGeneration' -in $adoption.requiredIdentity -and 'activeAttempt' -in $adoption.requiredIdentity -and (Same-Set @($adoption.atomicallyRequires) @('ACTIVE_REQUEST','CURRENT_GENERATION_AND_ATTEMPT','CURRENT_REVISIONS','NOT_CANCELLED','BEFORE_DEADLINE','SLOT_NOT_ADOPTED'))) 'Written atomic adoption conditions include cancellation, generation, deadline and one slot adoption.'
$stream = $spec.streamRules
Add-PlanningCheck 'bounded-separated-stream-contract' ((Same-Set @($stream.bucketKey) @('requestGeneration','outputItemIdOrCallId')) -and (Same-Set @($stream.hardLimitDimensions) @('aggregateBytes','eventCount','typedPayloadDepth')) -and $stream.completeItemsAndValidResponseTerminalRequired -eq $true -and $stream.overflowCanProduceValidProposal -eq $false -and $stream.partialArgumentsDispatchHost -eq $false) 'Interleaved calls and overflow are specified; no streaming implementation was run.'
$budget = $spec.budgetRules
Add-PlanningCheck 'per-request-attempts-and-root-budget-separated' ($budget.attemptLimitScope -eq 'per-logicalLanguageRequest' -and $budget.proposedMaximumTotalAttemptsPerLogicalLanguageRequest -eq 3 -and $budget.sdkRetriesIncludedInTotal -eq $true -and $budget.schemaRepairIncludedInAttemptLimit -eq $true -and $budget.newLanguageRequestConsumesSameRootBudget -eq $true -and $budget.rootScope -match 'restarts' -and (Same-Set @($budget.rootLimits) @('totalLanguageRequests','totalCost','elapsedDeadline','hostResources')) -and $budget.validRetryAfterIsMinimum -eq $true -and $budget.deadlineIncludesRetries -eq $true -and $budget.modelFailureRetriesHost -eq $false) 'Three attempts per logical language request is a proposal, not an API guarantee.'
Add-PlanningCheck 'sent-unknown-usage-reservation-contract' ($budget.unknownCostIsNotZero -eq $true -and $budget.sentAttemptMissingUsageRetainsReservation -eq $true -and $budget.retryRequiresSeparateWorstCaseReservation -eq $true -and (Same-Set @($budget.lateUsageDedupKey) @('logicalLanguageRequestId','attempt')) -and $budget.localSettlementIdentityPersistedBeforeSend -eq $true -and $budget.providerRequestIdBoundOnlyWhenReceived -eq $true -and $budget.lateUsageMustProveOwnership -eq $true -and $budget.usageReconciliationDispatchesHost -eq $false) 'Missing usage preserves reservation; local identity prevents null provider IDs from merging concurrent attempts.'
$secrets = $spec.secretRules
Add-PlanningCheck 'credential-reference-only-contract' ($secrets.portableReferenceOnly -eq 'credentialRef' -and $secrets.credentialStoreRequiresExistingAuthorization -eq $true -and $secrets.defaultNewCDriveStoreAllowed -eq $false -and 'endpointQuery' -in $secrets.prohibitedSecretLocations -and 'debugTrace' -in $secrets.prohibitedSecretLocations) 'No credential storage or real-client writes performed.'
$pack = $spec.resourcePack
$overlap = @($pack.allowedContent | Where-Object { $_ -in $pack.prohibitedContent })
Add-PlanningCheck 'closed-pack-content-and-preserved-history' (@($pack.allowedContent).Count -eq 9 -and @($pack.prohibitedContent).Count -eq 9 -and $overlap.Count -eq 0 -and $pack.importDispatchesHost -eq $false -and $pack.oldTasksLockVersion -eq $true -and $pack.hashAloneProvesTrust -eq $false -and $pack.retirementDeletesHistory -eq $false) 'Declarative resources only; no import execution or history deletion.'
Add-PlanningCheck 'qualification-separated-from-import' ($pack.stateFlowMeaning -eq 'SUCCESS_PATH_NOT_AUTOMATIC_IMPORT_EXECUTION' -and $pack.stateReasonRequired -eq $true -and $pack.importChecksExistingQualificationEvidenceOnly -eq $true -and $pack.qualificationJobSeparateFromImport -eq $true -and $pack.packageSelfClaimAdvancesLocalState -eq $false -and $pack.importAndQualificationHaveSeparateIdsAndReceipts -eq $true -and (Same-Set @($pack.waitingAndRejectedStates) @('WAITING_REQUIRED_CAPABILITY','WAITING_QUALIFICATION','WAITING_ADOPTION','REJECTED')) -and (Same-Set @($pack.qualificationJobRequires) @('CURRENT_APPROVAL','OWNED_FIXTURE','COMMON_INVOKER_GATE','CONTENT_ORACLE','INDEPENDENT_EVIDENCE'))) 'Import cannot grant authority for the separate qualification job.'
$refinementFailures = [Collections.Generic.List[string]]::new()
foreach ($refinement in $spec.refinements) {
    foreach ($work in $refinement.workItemRefs) { if ($work -notin $workIds) { $refinementFailures.Add($refinement.id + ':' + $work) } }
    foreach ($contract in $refinement.contractRefs) { if ($contract -notin $contractIds) { $refinementFailures.Add($refinement.id + ':' + $contract) } }
    if ($refinement.status -ne 'REFINEMENT_NOT_NEW_DISPATCH' -or @($refinement.runtimeEvidence).Count -ne 0) { $refinementFailures.Add($refinement.id + ':scope') }
}
Add-PlanningCheck 'eight-refinements-resolve-without-dispatch' (@($spec.refinements).Count -eq 8 -and @($spec.refinements.id | Select-Object -Unique).Count -eq 8 -and $refinementFailures.Count -eq 0) $refinementFailures.ToArray()
$missingInvariants = @($spec.invariantRefs | Where-Object { $_ -notin $prior.invariants.id })
Add-PlanningCheck 'invariant-references-resolve' (@($spec.invariantRefs).Count -eq 14 -and $missingInvariants.Count -eq 0) $missingInvariants
Add-PlanningCheck 'four-task-readiness-states' ((Same-Set @($spec.taskReadinessStates) @('READY_FOR_ORIGINAL_CONTRACT','READY_WITHIN_ADOPTED_EQUIVALENCE','OPTIONAL_GAPS_ONLY','WAITING_REQUIRED_CAPABILITY'))) 'Mandatory gaps remain waiting; accepted equivalence is explicit.'
$markdownFiles = @(Get-ChildItem -LiteralPath $detailRoot -Filter '*.md' -File)
$markdown = ($markdownFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 }) -join [Environment]::NewLine
$markdownSpIds = @([regex]::Matches($markdown, '\bSP\d{2}\b') | ForEach-Object { $_.Value } | Select-Object -Unique)
Add-PlanningCheck 'four-markdown-documents-and-contract-ids' ($markdownFiles.Count -eq 4 -and (Same-Set $markdownSpIds $contractIds) -and $markdown -match '55' -and $markdown -notmatch '\bSOURCE_VERIFIED\b') 'Eight SP references align; canonical evidence terms are consistent.'
$linkFailures = [Collections.Generic.List[string]]::new()
foreach ($file in $markdownFiles) {
    $body = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($match in [regex]::Matches($body, '\[[^\]]+\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Trim('<','>')
        if ($target -match '^https?://' -or $target.StartsWith('#')) { continue }
        $target = ($target -split '#',2)[0]
        $resolved = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $target))
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            if (-not ($WriteReport -and $resolved -eq (Join-Path $detailRoot 'SUPPORT_VALIDATION.json'))) { $linkFailures.Add($file.Name + ':' + $target) }
        }
    }
}
Add-PlanningCheck 'local-navigation-links-resolve' ($linkFailures.Count -eq 0) $linkFailures.ToArray()
$preservationSummary = [ordered]@{}
foreach ($groupName in @('priorPlanningFiles','source','configAndScripts')) {
    $entries = @($baseline.$groupName)
    $changed = [Collections.Generic.List[string]]::new()
    foreach ($entry in $entries) {
        $fullPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $entry.path))
        if (-not $fullPath.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Preservation entry escapes project root.' }
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf) -or (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash -ne $entry.sha256) { $changed.Add($entry.path) }
    }
    $preservationSummary[$groupName] = [ordered]@{ count=$entries.Count; changed=$changed.ToArray() }
    Add-PlanningCheck ($groupName + '-byte-preserved') ($entries.Count -gt 0 -and $changed.Count -eq 0) $preservationSummary[$groupName]
}
Push-Location -LiteralPath $projectRoot
try {
    $sourcePaths = @(& rg --files Source)
    if ($LASTEXITCODE -ne 0) { throw 'Source inventory command failed.' }
} finally { Pop-Location }
$sourcePaths = @($sourcePaths | ForEach-Object { $_.Replace('/','\') })
$baselineSourcePaths = @($baseline.source | ForEach-Object { $_.path.Replace('/','\') })
Add-PlanningCheck 'source-inventory-preserved-independent-of-cwd' (Same-Set $sourcePaths $baselineSourcePaths) ([ordered]@{baselineCount=$baselineSourcePaths.Count; currentCount=$sourcePaths.Count; scope='rg-visible Source files, including existing build artifacts'})
$artifactFiles = @($markdownFiles.FullName) + @((Join-Path $detailRoot 'SUPPORT_DESIGN_SPECIFICATION.json'), $PSCommandPath)
$artifactDigests = @($artifactFiles | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($projectRoot,$_); sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash} })
$failures = @($checks | Where-Object { -not $_.passed })
$report = [ordered]@{
    kind='support-planning-consistency-validation-only'
    status=$(if ($failures.Count -eq 0) {'PASS_CANDIDATE'} else {'CHANGES_REQUESTED_CANDIDATE'})
    validatedAtUtc=[DateTime]::UtcNow.ToString('o')
    productRuntimeAccepted=$false
    modelRequestsExecuted=0
    gpOperationsExecuted=0
    psBusinessActionsExecuted=0
    businessCasesExecuted=0
    proposedCaseCounts=$caseCounts
    formatCandidateRows=$inventory.Count
    independentlyQualifiedFormatRows=0
    checkCount=$checks.Count
    passedChecks=$checks.Count-$failures.Count
    failures=@($failures | ForEach-Object { $_.name })
    checks=$checks.ToArray()
    preservation=$preservationSummary
    artifactDigests=$artifactDigests
}
if ($WriteReport) {
    $probePath = Join-Path $detailRoot ('.validator-write-probe-' + [Guid]::NewGuid().ToString('N'))
    try {
        [IO.File]::WriteAllText($probePath, 'D-drive-write-probe', [Text.UTF8Encoding]::new($false))
        if ([IO.File]::ReadAllText($probePath) -ne 'D-drive-write-probe') { throw 'Write probe failed.' }
    } finally {
        if (Test-Path -LiteralPath $probePath) { Remove-Item -LiteralPath $probePath -Force }
    }
    [IO.File]::WriteAllText((Join-Path $detailRoot 'SUPPORT_VALIDATION.json'), ($report | ConvertTo-Json -Depth 16) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
}
[pscustomobject]@{status=$report.status; checks=$checks.Count; passed=$report.passedChecks; proposedCases=$cases.Count; runtimeCasesExecuted=0; failures=$report.failures; preserved=$preservationSummary} | ConvertTo-Json -Depth 8
if ($failures.Count -gt 0) { exit 1 }
