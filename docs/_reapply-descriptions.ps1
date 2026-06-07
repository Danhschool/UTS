$ErrorActionPreference = 'Stop'
$mdPath = 'd:\Unity_3D\UTS\docs\Assets-Scripts-By-Module.md'
$jsonPath = 'd:\Unity_3D\UTS\docs\_script-descriptions.json'
$map = @{}
(Get-Content $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json).PSObject.Properties | ForEach-Object {
  $map[$_.Name] = $_.Value
}

$lines = [System.IO.File]::ReadAllLines($mdPath, [System.Text.UTF8Encoding]::new($false))
$out = New-Object System.Collections.Generic.List[string]
foreach ($line in $lines) {
  if ($line -match '^\-\s+\*\*`([^`]+)`\*\*\s+\-\s+(.*)$') {
    $path = $Matches[1]
    $d = $map[$path]
    if (-not $d) { $d = $Matches[2] }
    $d = $d -replace '\|', '\|'
    $out.Add('- **`' + $path + '`** - ' + $d)
  } else {
    $out.Add($line)
  }
}
[System.IO.File]::WriteAllLines($mdPath, $out, [System.Text.UTF8Encoding]::new($false))
Write-Host "Reapplied descriptions. Lines: $($out.Count)"
