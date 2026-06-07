$ErrorActionPreference = 'Stop'
$root = 'd:\Unity_3D\UTS\Assets\Scripts'
$outPath = 'd:\Unity_3D\UTS\docs\_script-descriptions.json'
$overridePath = 'd:\Unity_3D\UTS\docs\_description-overrides.json'

$overrides = @{}
(Get-Content $overridePath -Raw -Encoding UTF8 | ConvertFrom-Json).PSObject.Properties | ForEach-Object {
  $overrides[$_.Name] = $_.Value
}

function Get-ClassSummaryFromHead([string[]]$head) {
  for ($i = 0; $i -lt $head.Count; $i++) {
    if ($head[$i] -notmatch '^\s*public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*(class|interface|struct|enum)\s+(\w+)') { continue }
    $j = $i - 1
    $sumLines = New-Object System.Collections.Generic.List[string]
    while ($j -ge 0 -and $head[$j] -match '^\s*///') {
      $sumLines.Insert(0, $head[$j])
      $j--
    }
    if ($sumLines.Count -eq 0) { return $null }
    $block = $sumLines -join [Environment]::NewLine
    if ($block -match '<summary>\s*(.*?)\s*</summary>') {
      $s = $Matches[1] -replace '(?m)^\s*///\s*', '' -replace '\s+', ' '
      $s = $s -replace '<see cref="([^"]+)"[^>]*/>', '$1'
      return $s.Trim()
    }
    return $null
  }
  return $null
}

function Get-Desc($fullPath, $rel) {
  if ($overrides.ContainsKey($rel)) { return $overrides[$rel] }
  $head = Get-Content $fullPath -Encoding UTF8 -TotalCount 120
  $s = Get-ClassSummaryFromHead $head
  if ($s) { return $s }
  $content = $head -join [Environment]::NewLine
  if ($content -match 'menuName:\s*"([^"]+)"') {
    return "ScriptableObject ($($Matches[1])) - du lieu cau hinh."
  }
  if ($rel -match 'SO\.cs$') { return 'ScriptableObject - stats/cost/config.' }
  if ($rel -match 'Handlers/.*Handler\.cs$') { return 'Hotkey handler - phim tat toi hanh dong game/UI.' }
  if ($rel -match 'Command\.cs$') { return 'Lenh SO - CanHandle/Handle (click hoac AI).' }
  if ($rel -match '^Events/.*Event\.cs$') { return 'Event payload - Bus<T>.Raise(owner).' }
  if ($rel -match '^Behavior/.*Action\.cs$') { return 'Behavior action node - buoc BT.' }
  if ($rel -match 'Condition\.cs$') { return 'Behavior condition - re nhanh BT.' }
  if ($rel -match 'EventChannel\.cs$') { return 'Behavior event channel - noi graph C#.' }
  if ($rel -match 'Utility\.cs$') { return 'Static utility - logic dung chung.' }
  if ($rel -match 'Planner\.cs$') { return 'AI planner - sinh intent.' }
  if ($rel -match 'Manager\.cs$') { return 'AI manager - dieu phoi planner.' }
  $name = [System.IO.Path]::GetFileNameWithoutExtension($rel)
  return "Type $name - xem file."
}

$map = @{}
Get-ChildItem $root -Filter *.cs -Recurse -File | ForEach-Object {
  $rel = $_.FullName.Substring($root.Length + 1).Replace('\', '/')
  $map[$rel] = Get-Desc $_.FullName $rel
}
$map | ConvertTo-Json -Depth 1 | Set-Content $outPath -Encoding UTF8
Write-Host "OK $($map.Count)"
