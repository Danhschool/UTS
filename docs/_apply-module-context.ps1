$ErrorActionPreference = 'Stop'
$root = 'd:\Unity_3D\UTS\Assets\Scripts'
$mdPath = 'd:\Unity_3D\UTS\docs\Assets-Scripts-By-Module.md'
$overridePath = 'd:\Unity_3D\UTS\docs\_description-overrides.json'
$labelsPath = 'd:\Unity_3D\UTS\docs\_module-context-labels.json'
$moduleDescPath = 'd:\Unity_3D\UTS\docs\_module-file-descriptions.json'

$labels = (Get-Content $labelsPath -Raw -Encoding UTF8 | ConvertFrom-Json).modules
$moduleDesc = @{}
if (Test-Path $moduleDescPath) {
  (Get-Content $moduleDescPath -Raw -Encoding UTF8 | ConvertFrom-Json).PSObject.Properties | ForEach-Object {
    $moduleDesc[$_.Name] = $_.Value
  }
}
$overrides = @{}
if (Test-Path $overridePath) {
  (Get-Content $overridePath -Raw -Encoding UTF8 | ConvertFrom-Json).PSObject.Properties | ForEach-Object {
    $overrides[$_.Name] = $_.Value
  }
}

function Get-SummaryFromFile([string]$fullPath) {
  $text = [IO.File]::ReadAllText($fullPath, [Text.UTF8Encoding]::new($false))
  if ($text.Length -gt 12000) { $text = $text.Substring(0, 12000) }
  if ($text -match '(?s)((?:[ \t]*///[^\n]*\n)+)(?:\s*\[[^\]]*\]\s*)*\s*public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*(?:class|interface|struct|enum)\s+\w+') {
    $block = $Matches[1]
    if ($block -match '(?s)<summary>\s*(.*?)\s*</summary>') {
      $s = $Matches[1] -replace '(?m)^\s*///\s*', '' -replace '\s+', ' '
      $s = $s -replace '<see cref="([^"]+)"[^>]*/>', '$1'
      return $s.Trim()
    }
  }
  return $null
}

function Get-BaseDesc([string]$rel) {
  if ($overrides.ContainsKey($rel)) { return $overrides[$rel] }
  $full = Join-Path $root ($rel -replace '/', '\')
  if (-not (Test-Path $full)) { return "File $rel." }
  $s = Get-SummaryFromFile $full
  if ($s) { return $s }
  if ($rel -match 'SO\.cs$') { return 'ScriptableObject cau hinh du lieu.' }
  if ($rel -match '^Behavior/.*Action\.cs$') { return 'Node hanh dong Unity Behavior Graph.' }
  if ($rel -match 'EventChannel\.cs$') { return 'Kenh event Behavior Graph.' }
  if ($rel -match '^Events/.*Event\.cs$') { return 'Payload Bus<T> theo Owner.' }
  if ($rel -match 'Command\.cs$') { return 'SO lenh CanHandle/Handle.' }
  if ($rel -match 'Handler\.cs$') { return 'Handler phim tat.' }
  if ($rel -match 'Utility\.cs$') { return 'Static utility.' }
  if ($rel -match 'Planner\.cs$') { return 'AI planner.' }
  if ($rel -match 'Manager\.cs$') { return 'AI manager.' }
  $n = [IO.Path]::GetFileNameWithoutExtension($rel)
  return "Type $n."
}

function Get-LabelKey([string]$modId, [string]$subTitle, [string]$rel) {
  $sub = ($subTitle -replace '^###\s*', '').ToLowerInvariant()
  if ($modId -eq 'PHU') {
    if ($sub -match 'events') { return 'PHU_Events' }
    if ($sub -match 'utilities') { return 'PHU_Utils' }
    if ($sub -match 'units') { return 'PHU_Units' }
    return 'PHU'
  }
  if ($modId -eq 'M1') {
    if ($rel -match 'Pregame/') { return 'M1_Pregame' }
    if ($rel -match 'Startup/') { return 'M1_Startup' }
    return 'M1'
  }
  if ($modId -eq 'M2') {
    if ($sub -match 'commands') { return 'M2_Commands' }
    if ($sub -match 'player') {
      if ($rel -match 'Fog|fog|Vision|Hideable') { return 'M2_Fog' }
      return 'M2_Player'
    }
    return 'M2'
  }
  if ($modId -eq 'M3') {
    if ($sub -match 'player') { return 'M3_Player' }
    if ($sub -match 'commands') { return 'M3_Commands' }
    if ($sub -match 'environment') { return 'M3_Env' }
    if ($sub -match 'events') { return 'M3_Events' }
    if ($sub -match 'utilities') { return 'M3_Utils' }
    if ($sub -match 'audio') { return 'M3_Audio' }
    if ($sub -match 'units') { return 'M3_Units' }
    return 'M3'
  }
  if ($modId -eq 'M4') {
    if ($sub -match 'commands') { return 'M4_Commands' }
    if ($sub -match 'events') { return 'M4_Events' }
    if ($sub -match 'utilities') { return 'M4_Utils' }
    if ($sub -match 'ui') { return 'M4_UI' }
    if ($sub -match 'units') { return 'M4_Units' }
    return 'M4'
  }
  if ($modId -eq 'M5') {
    if ($sub -match 'commands') { return 'M5_Commands' }
    if ($sub -match 'events') { return 'M5_Events' }
    if ($sub -match 'utilities') { return 'M5_Utils' }
    if ($sub -match 'audio') { return 'M5_Audio' }
    if ($sub -match 'units') { return 'M5_Units' }
    return 'M5'
  }
  return $modId
}

function Format-Entry([string]$modId, [string]$subTitle, [string]$path) {
  $key = Get-LabelKey $modId $subTitle $path
  $ctx = $labels.$key
  if (-not $ctx) { $ctx = $labels.$modId }
  if (-not $ctx) { $ctx = "Trong $modId" }
  $modKey = "$modId|$path"
  if ($moduleDesc.ContainsKey($modKey)) {
    $base = $moduleDesc[$modKey]
  } else {
    $base = Get-BaseDesc $path
  }
  $d = "$ctx`: $base" -replace '\|', '\|'
  return '- **`' + $path + '`** - ' + $d
}

$lines = [IO.File]::ReadAllLines($mdPath, [Text.UTF8Encoding]::new($false))
$out = New-Object 'System.Collections.Generic.List[string]'
$modId = ''
$subTitle = ''

foreach ($line in $lines) {
  if ($line -match '^## (M\d+)') {
    $modId = $Matches[1]
    $subTitle = ''
    $out.Add($line)
    continue
  }
  if ($line -match '^## Ph') {
    $modId = 'PHU'
    $subTitle = ''
    $out.Add($line)
    continue
  }
  if ($line -match '^### ') {
    $subTitle = $line
    $out.Add($line)
    continue
  }
  if ($line -match '^\-\s+\*\*`([^`]+)`\*\*' -and $modId) {
    $out.Add((Format-Entry $modId $subTitle $Matches[1]))
    continue
  }
  $out.Add($line)
}

[IO.File]::WriteAllLines($mdPath, $out, [Text.UTF8Encoding]::new($false))
Write-Host 'OK' $out.Count
