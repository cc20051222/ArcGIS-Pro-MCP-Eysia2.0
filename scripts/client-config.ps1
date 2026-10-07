# ArcGIS Pro MCP client catalog validator/configurator.
# Default action is read-only. Apply/Restore are explicit and restricted to supplied paths.
[CmdletBinding()]
param(
  [ValidateSet('Plan','Validate','Apply','Restore')][string]$Action='Validate',
  [string]$CatalogPath='',
  [string]$ConfigRoot='',
  [string]$Client='',
  [string]$TemplateRoot='',
  [switch]$InjectFailureAfterWrite,
  [switch]$ForceRestore,
  [switch]$Json
)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$catalogFile=if($CatalogPath){[IO.Path]::GetFullPath($CatalogPath)}else{Join-Path $repoRoot 'Config\client-catalog.json'}
$root=if($ConfigRoot){[IO.Path]::GetFullPath($ConfigRoot)}else{$repoRoot}
$templateRoot=if($TemplateRoot){[IO.Path]::GetFullPath($TemplateRoot)}else{$repoRoot}
$prohibited='(?i)(token|password|secret|apikey|authorization|header|login|provider|model)'
$begin='# MANAGED BY ARC GIS PRO MCP: BEGIN arcgis-pro-mcp'
$end='# MANAGED BY ARC GIS PRO MCP: END arcgis-pro-mcp'

function Read-Json($p){
  if(-not(Test-Path -LiteralPath $p -PathType Leaf)){throw "CONFIG_NOT_FOUND: $p"}
  Get-Content -LiteralPath $p -Raw -Encoding UTF8|ConvertFrom-Json
}
function Get-Sha256Hex([string]$p){
  $stream=[IO.File]::OpenRead($p)
  $hasher=[Security.Cryptography.SHA256]::Create()
  try{([BitConverter]::ToString($hasher.ComputeHash($stream))).Replace('-','')}
  finally{$hasher.Dispose();$stream.Dispose()}
}
function Get-Hash($p){if(Test-Path -LiteralPath $p -PathType Leaf){Get-Sha256Hex $p}else{$null}}
function AtomicWrite($p,$value){
  $dir=[IO.Path]::GetDirectoryName($p)
  if([string]::IsNullOrWhiteSpace($dir)){throw 'CONFIG_PATH_INVALID'}
  if(-not(Test-Path -LiteralPath $dir -PathType Container)){New-Item -ItemType Directory -Path $dir -Force|Out-Null}
  $tmp=Join-Path $dir ('.tmp-'+[guid]::NewGuid().ToString('N'))
  try{
    if($value -is [byte[]]){[IO.File]::WriteAllBytes($tmp,$value)}
    else{[IO.File]::WriteAllText($tmp,[string]$value,[Text.UTF8Encoding]::new($false))}
    Move-Item -LiteralPath $tmp -Destination $p -Force
    if(-not(Test-Path -LiteralPath $p -PathType Leaf)){throw 'ATOMIC_OUTPUT_MISSING'}
  }finally{if(Test-Path -LiteralPath $tmp){Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue}}
}
function Resolve-ClientPath($c){
  $p=[string]$c.configPath
  if($c.pathStrategy -eq 'userProfileRelative'){
    $base=if($ConfigRoot){$root}else{[Environment]::GetFolderPath('UserProfile')}
    return [IO.Path]::GetFullPath((Join-Path $base $p))
  }
  if([IO.Path]::IsPathRooted($p)){return $p}
  return [IO.Path]::GetFullPath((Join-Path $root $p))
}
function Get-ManagedBlock($text){
  $m=[regex]::Match($text,[regex]::Escape($begin)+'[\s\S]*?'+[regex]::Escape($end))
  if($m.Success){return $m.Value}
  return $null
}
function Get-TargetFragment($c,$text){
  $managed=Get-ManagedBlock $text
  if($managed){return $managed}
  if($c.format -eq 'toml'){
    $m=[regex]::Match($text,'(?ms)^\[mcp_servers\.arcgis-pro-mcp\]\s*$.*?(?=^\[|\z)')
    if($m.Success){return $m.Value}
  }
  if($c.format -eq 'yaml-patch'){
    $m=[regex]::Match($text,'(?ms)^-\s+insert:\s*$.*?(?=^-\s+\w|\z)')
    if($m.Success -and $m.Value -match [regex]::Escape([string]$catalog.server.name)){return $m.Value}
  }
  return $null
}
function Assert-ManagedBlockShape($c,$text){
  if($c.format -eq 'json'){return}
  $starts=([regex]::Matches($text,[regex]::Escape($begin))).Count
  $ends=([regex]::Matches($text,[regex]::Escape($end))).Count
  if($starts -gt 1 -or $ends -gt 1){throw "MANAGED_BLOCK_DUPLICATE: $($c.id) contains duplicate managed blocks."}
}
function Validate-Content($c,$p,$text){
  if($c.format -eq 'json'){
    try{ $o=$text|ConvertFrom-Json }catch{throw "CONFIG_SYNTAX_INVALID: $($c.id) JSON: $($_.Exception.Message)"}
    if(-not $o.PSObject.Properties['mcpServers']){throw "CONFIG_ENDPOINT_INVALID: $($c.id) missing mcpServers."}
    $prop=$o.mcpServers.PSObject.Properties|Where-Object Name -eq $catalog.server.name
    if(-not $prop){throw "CONFIG_ENDPOINT_INVALID: $($c.id) missing canonical server entry."}
    $url=$prop.Value.PSObject.Properties['url']
    if(-not $url -or [string]$url.Value -ne [string]$catalog.server.endpoint){throw "CONFIG_ENDPOINT_INVALID: $($c.id) canonical url must equal the loopback endpoint exactly."}
    $targetText=([string]$prop.Name)+':'+($prop.Value|ConvertTo-Json -Depth 12)
  }else{
    Assert-ManagedBlockShape $c $text
    $targetText=Get-TargetFragment $c $text
    if(-not $targetText){throw "CONFIG_ENDPOINT_INVALID: $($c.id) missing canonical managed/adjacent target entry."}
  }
  if($c.format -eq 'toml'){
    if($targetText -notmatch '(?m)^\[mcp_servers\.arcgis-pro-mcp\]\s*$' -or $targetText -notmatch ('(?m)^\s*url\s*=\s*"'+[regex]::Escape([string]$catalog.server.endpoint)+'"\s*$')){throw "CONFIG_ENDPOINT_INVALID: $($c.id) canonical TOML section/url is not exact."}
  }elseif($c.format -eq 'yaml-patch'){
    if($targetText -notmatch ('(?m)^\s*serverName:\s*'+[regex]::Escape([string]$catalog.server.name)+'\s*$') -or $targetText -notmatch ('(?m)^\s*url:\s*'+[regex]::Escape([string]$catalog.server.endpoint)+'\s*$')){throw "CONFIG_ENDPOINT_INVALID: $($c.id) canonical YAML serverName/url is not exact."}
  }elseif($targetText -notmatch [regex]::Escape([string]$catalog.server.name) -or $targetText -notmatch [regex]::Escape([string]$catalog.server.endpoint)){throw "CONFIG_ENDPOINT_INVALID: $($c.id) must contain the canonical server and loopback endpoint."}
  if($targetText -match $prohibited){throw "SECRET_FIELD_REFUSED: $($c.id) canonical entry contains a prohibited credential/provider field."}
  [pscustomobject]@{id=$c.id;status='PASS';path=$p;format=$c.format;applyMode=$c.applyMode;serverName=$catalog.server.name;endpoint=$catalog.server.endpoint;secretFieldsDetected=$false}
}
function Restore-Original($p,$exists,$bytes){
  if($exists){AtomicWrite $p $bytes}
  elseif(Test-Path -LiteralPath $p -PathType Leaf){Remove-Item -LiteralPath $p -Force}
}
function Read-Existing($p){if(Test-Path -LiteralPath $p -PathType Leaf){return ,([IO.File]::ReadAllBytes($p))}else{return $null}}
function Write-Metadata($meta,$source,$beforeExists,$beforeHash,$lastAppliedHash,$transactionId,$state){
  $m=[ordered]@{schema='arcgis-pro-mcp-client-backup-v1';sourcePath=$source;beforeExists=$beforeExists;baselineHash=$beforeHash;lastAppliedHash=$lastAppliedHash;transactionId=$transactionId;updatedAtUtc=[DateTime]::UtcNow.ToString('o');state=$state}
  AtomicWrite $meta ([Text.Encoding]::UTF8.GetBytes(($m|ConvertTo-Json -Depth 8)))
}
function Assert-BackupMetadata($meta,$p){
  if(-not(Test-Path -LiteralPath $meta -PathType Leaf)){throw "BACKUP_METADATA_MISSING: $meta"}
  $m=Read-Json $meta
  foreach($n in @('sourcePath','beforeExists','baselineHash','lastAppliedHash','transactionId')){if(-not $m.PSObject.Properties[$n]){throw "BACKUP_METADATA_INVALID: $meta missing $n"}}
  if([IO.Path]::GetFullPath([string]$m.sourcePath) -ne [IO.Path]::GetFullPath($p)){throw "BACKUP_METADATA_INVALID: source path mismatch."}
  return $m
}
try{
  $catalog=Read-Json $catalogFile
  if($catalog.schema -ne 'arcgis-pro-mcp-client-catalog-v1'){throw 'CATALOG_SCHEMA_INVALID'}
  if($catalog.server.canonicalProductionToolCount -ne 239 -or $catalog.server.helperToolsExcludedFromProduction -notcontains 'mcp_auth'){throw 'CATALOG_TOOL_NORMALIZATION_INVALID'}
  if($Action -in @('Apply','Restore') -and [string]::IsNullOrWhiteSpace($Client)){throw 'MUTATION_REQUIRES_EXPLICIT_CLIENT: Apply and Restore require exactly one -Client.'}
  $clients=@($catalog.clients)
  if($Client){$clients=@($clients|Where-Object id -eq $Client);if($clients.Count -ne 1){throw "CLIENT_NOT_IN_CATALOG: $Client"}}
  $reports=@()
  foreach($c in $clients){
    $p=Resolve-ClientPath $c
    if($Action -eq 'Plan'){$reports+= [pscustomobject]@{id=$c.id;status='PLAN_ONLY';path=$p;applyMode=$c.applyMode;template=$c.template};continue}
    if($c.applyMode -eq 'validate-template-only'){
      $templatePath=Join-Path $templateRoot $c.template
      $template=Get-Content -LiteralPath $templatePath -Raw -Encoding UTF8
      $reports+=Validate-Content $c $templatePath $template
      $reports[-1].status='TEMPLATE_VALID'
      if($Action -eq 'Apply'){throw "APPLY_UNSUPPORTED: $($c.id) is template-only."}
      continue
    }
    if($Action -eq 'Apply'){
      $beforeExists=Test-Path -LiteralPath $p -PathType Leaf
      $beforeBytes=Read-Existing $p
      $beforeHash=Get-Hash $p
      $bak=$p+'.arcgis-pro-mcp.bak';$meta=$bak+'.json';$transactionId=[guid]::NewGuid().ToString('N');$writeStarted=$false
      $baselineExists=$beforeExists;$baselineHash=$beforeHash
      try{
        if(Test-Path -LiteralPath $meta -PathType Leaf){
          $old=Assert-BackupMetadata $meta $p
          $oldLast=[string]$old.lastAppliedHash
          if($oldLast -and [string]$beforeHash -ne $oldLast){throw "STALE_BACKUP_REFUSED: $($c.id) current bytes differ from the last applied transaction."}
          if(-not $oldLast){
            if([bool]$old.beforeExists){if(-not $beforeExists -or [string]$beforeHash -ne [string]$old.baselineHash){throw "STALE_BACKUP_REFUSED: $($c.id) current bytes differ from the untouched baseline."}}
            elseif($beforeExists){throw "STALE_BACKUP_REFUSED: $($c.id) a file appeared after the untouched absent baseline."}
          }
          if(-not $beforeExists -and [bool]$old.beforeExists){throw "STALE_BACKUP_REFUSED: $($c.id) baseline existence mismatch."}
          $baselineExists=[bool]$old.beforeExists;$baselineHash=[string]$old.baselineHash
        }elseif(Test-Path -LiteralPath $bak -PathType Leaf){throw "BACKUP_METADATA_MISSING: $($c.id)"}
        if($beforeExists -and -not(Test-Path -LiteralPath $bak -PathType Leaf)){
          Copy-Item -LiteralPath $p -Destination $bak -Force
          Write-Metadata $meta $p $true $beforeHash $null $transactionId 'baseline'
        }elseif(-not $beforeExists -and -not(Test-Path -LiteralPath $meta -PathType Leaf)){
          Write-Metadata $meta $p $false $null $null $transactionId 'baseline'
        }
        $template=Get-Content -LiteralPath (Join-Path $templateRoot $c.template) -Raw -Encoding UTF8
        if($c.format -eq 'json'){
          $obj=if($beforeExists){$text=[Text.Encoding]::UTF8.GetString($beforeBytes);$text|ConvertFrom-Json}else{[pscustomobject]@{}}
          if(-not $obj.PSObject.Properties['mcpServers']){$obj|Add-Member NoteProperty mcpServers ([pscustomobject]@{})}
          $existing=$obj.mcpServers.PSObject.Properties|Where-Object Name -eq $catalog.server.name
          if($existing){$existingText=$existing.Value|ConvertTo-Json -Depth 12;if($existingText -match $prohibited){throw "SECRET_FIELD_REFUSED: $($c.id) canonical entry contains a prohibited credential/provider field."}}
          $entry=[pscustomobject]@{url=$catalog.server.endpoint}
          $obj.mcpServers|Add-Member NoteProperty $catalog.server.name $entry -Force
          $candidate=$obj|ConvertTo-Json -Depth 12
        }else{
          $text=if($beforeExists){[Text.Encoding]::UTF8.GetString($beforeBytes)}else{''};$block=$template.Trim()
          Assert-ManagedBlockShape $c $text
          if($text -match [regex]::Escape($begin)+'[\s\S]*?'+[regex]::Escape($end)){$candidate=[regex]::Replace($text,[regex]::Escape($begin)+'[\s\S]*?'+[regex]::Escape($end),[System.Text.RegularExpressions.MatchEvaluator]{param($m)$block})}
          elseif($text -match [regex]::Escape([string]$catalog.server.name)+'|'+[regex]::Escape([string]$catalog.server.endpoint)){throw "UNMANAGED_EXISTING_ENTRY: $($c.id) contains a canonical entry without a complete managed block; migrate manually or use Validate-only."}
          else{$candidate=$text.TrimEnd()+[Environment]::NewLine+$block+[Environment]::NewLine}
        }
        $null=Validate-Content $c $p $candidate
        AtomicWrite $p ([Text.Encoding]::UTF8.GetBytes($candidate));$writeStarted=$true
        $afterHash=Get-Hash $p;$null=Validate-Content $c $p ([Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($p)))
        Write-Metadata $meta $p $baselineExists $baselineHash $afterHash $transactionId 'applied'
        if($InjectFailureAfterWrite){throw 'INJECTED_POST_WRITE_FAILURE'}
        $reports+= [pscustomobject]@{id=$c.id;status='APPLIED';path=$p;backup=$bak;beforeExists=$beforeExists;beforeHash=$beforeHash;afterHash=$afterHash;backupHash=Get-Hash $bak;recovery='not-needed'}
      }catch{
        if($writeStarted){
          Restore-Original $p $beforeExists $beforeBytes
          if(Test-Path -LiteralPath $meta -PathType Leaf){Write-Metadata $meta $p $baselineExists $baselineHash (Get-Hash $p) $transactionId 'recovered'}
        }
        throw
      }
    }elseif($Action -eq 'Restore'){
      $bak=$p+'.arcgis-pro-mcp.bak';$meta=$bak+'.json';$m=Assert-BackupMetadata $meta $p;$currentHash=Get-Hash $p
      if(-not $ForceRestore -and [string]$currentHash -ne [string]$m.lastAppliedHash){throw "STALE_BACKUP_REFUSED: $($c.id) current bytes differ from the last applied transaction; use -ForceRestore only after review."}
      if([bool]$m.beforeExists){
        if(-not(Test-Path -LiteralPath $bak -PathType Leaf)){throw "BACKUP_NOT_FOUND: $bak"}
        $original=[IO.File]::ReadAllBytes($bak);AtomicWrite $p $original;$after=Get-Hash $p
        if([string]$after -ne [string]$m.baselineHash){throw 'RESTORE_VERIFY_FAILED'}
        Write-Metadata $meta $p $true $m.baselineHash $m.baselineHash $m.transactionId 'restored'
        $reports+=[pscustomobject]@{id=$c.id;status='RESTORED';path=$p;restoredHash=$after;backupHash=Get-Hash $bak}
      }else{
        if(Test-Path -LiteralPath $p -PathType Leaf){Remove-Item -LiteralPath $p -Force}
        if(Test-Path -LiteralPath $p -PathType Leaf){throw 'RESTORE_VERIFY_FAILED'}
        Write-Metadata $meta $p $false $null $null $m.transactionId 'restored'
        $reports+=[pscustomobject]@{id=$c.id;status='RESTORED';path=$p;restoredHash=$null;backupHash=$null}
      }
    }else{
      if(-not(Test-Path -LiteralPath $p -PathType Leaf)){$reports+=[pscustomobject]@{id=$c.id;status='NOT_INSTALLED';path=$p;applyMode=$c.applyMode};continue}
      $reports+=Validate-Content $c $p ([Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($p)))
    }
  }
  $out=[ordered]@{schema='arcgis-pro-mcp-client-config-report-v1';catalogSchema=$catalog.schema;catalogVersion=$catalog.version;action=$Action;server=$catalog.server;clients=@($reports);readOnly=($Action -in @('Plan','Validate'));freshConnectionEvidence='CARRIED_FORWARD_ONLY'}
  if($Json){$out|ConvertTo-Json -Depth 12}else{Write-Host ('Client config '+$Action+': PASS');$reports|ForEach-Object{Write-Host ($_.id+': '+$_.status+' '+$_.path)}}
  exit 0
}catch{$e=[ordered]@{schema='arcgis-pro-mcp-client-config-report-v1';action=$Action;overallStatus='ERROR';error=$_.Exception.Message};if($Json){$e|ConvertTo-Json -Depth 8}else{Write-Error $e.error};exit 1}
