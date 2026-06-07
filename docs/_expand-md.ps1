$ErrorActionPreference = 'Stop'
$mdPath = 'd:\Unity_3D\UTS\docs\Assets-Scripts-By-Module.md'
$jsonPath = 'd:\Unity_3D\UTS\docs\_script-descriptions.json'
$desc = Get-Content $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$map = @{}
$desc.PSObject.Properties | ForEach-Object { $map[$_.Name] = $_.Value }

$lines = [System.Collections.Generic.List[string]]::new()
foreach ($raw in [System.IO.File]::ReadAllLines($mdPath, [System.Text.UTF8Encoding]::new($false))) {
    $lines.Add($raw)
}

$out = New-Object System.Collections.Generic.List[string]
$i = 0
while ($i -lt $lines.Count) {
    if ($lines[$i] -eq '```') {
        $block = [System.Collections.Generic.List[string]]::new()
        $i++
        while ($i -lt $lines.Count -and $lines[$i] -ne '```') {
            $block.Add($lines[$i])
            $i++
        }
        $out.Add('```')
        $isFileList = ($block | Where-Object { $_ -match '\.cs' }).Count -gt 0 -and ($block | Where-Object { $_ -match '^\s*-\s+\*\*' }).Count -eq 0
        if ($isFileList) {
            foreach ($bl in $block) {
                $path = $bl.Trim()
                if ([string]::IsNullOrWhiteSpace($path)) { continue }
                $d = $map[$path]
                if (-not $d) { $d = 'Script C# - xem implementation.' }
                $d = $d -replace '\|', '\|'
                $out.Add('- **`' + $path + '`** - ' + $d)
            }
        } else {
            foreach ($bl in $block) { $out.Add($bl) }
        }
        $out.Add('```')
        if ($i -lt $lines.Count) { $i++ }
        continue
    }
    $out.Add($lines[$i])
    $i++
}

[System.IO.File]::WriteAllLines($mdPath, $out, [System.Text.UTF8Encoding]::new($false))
Write-Host "Done. Output lines: $($out.Count)"
