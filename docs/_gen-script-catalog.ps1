$ErrorActionPreference = 'Stop'
$root = 'd:\Unity_3D\UTS\Assets\Scripts'
$outPath = 'd:\Unity_3D\UTS\docs\Script-Catalog.md'
$overridePath = 'd:\Unity_3D\UTS\docs\_description-overrides.json'
$moduleMdPath = 'd:\Unity_3D\UTS\docs\Assets-Scripts-By-Module.md'

$overrides = @{}
if (Test-Path $overridePath) {
    (Get-Content $overridePath -Raw -Encoding UTF8 | ConvertFrom-Json).PSObject.Properties | ForEach-Object {
        $overrides[$_.Name] = $_.Value
    }
}

$moduleContext = @{}
if (Test-Path $moduleMdPath) {
    $md = Get-Content $moduleMdPath -Raw -Encoding UTF8
    [regex]::Matches($md, '- \*\*`([^`]+\.cs)`\*\* - (.+?)(?=\r?\n)') | ForEach-Object {
        $path = $_.Groups[1].Value
        $desc = $_.Groups[2].Value.Trim()
        if ($desc -match 'Trong [^:]+:\s*(.+)') { $desc = $Matches[1].Trim() }
        if (-not $moduleContext.ContainsKey($path) -or $desc.Length -gt $moduleContext[$path].Length) {
            $moduleContext[$path] = $desc
        }
    }
}

function Get-SummaryFromText([string]$text) {
    if ($text -match '(?s)((?:[ \t]*///[^\n]*\n)+)(?:\s*\[[^\]]*\]\s*)*\s*public\s+(?:readonly\s+|sealed\s+|abstract\s+|static\s+|partial\s+)*(?:class|interface|struct|enum)\s+\w+') {
        $block = $Matches[1]
        if ($block -match '(?s)<summary>\s*(.*?)\s*</summary>') {
            $s = $Matches[1] -replace '(?m)^\s*///\s*', '' -replace '\s+', ' '
            $s = $s -replace '<see cref="([^"]+)"[^>]*/>', '$1'
            return $s.Trim()
        }
    }
    return $null
}

function Get-HeuristicDesc([string]$rel) {
    if ($rel -match 'SO\.cs$') { return 'ScriptableObject - du lieu cau hinh (stats, cost, icon).' }
    if ($rel -match 'Handlers/.*Handler\.cs$') { return 'Hotkey handler - phim tat toi hanh dong game/UI.' }
    if ($rel -match 'Command\.cs$') { return 'Lenh SO - CanHandle/Handle (click hoac AI).' }
    if ($rel -match '^Events/.*Event\.cs$') { return 'Event payload - Bus<T>.Raise(owner).' }
    if ($rel -match '^Behavior/.*Action\.cs$') { return 'Behavior action node - buoc BT.' }
    if ($rel -match 'Condition\.cs$') { return 'Behavior condition - re nhanh BT.' }
    if ($rel -match 'EventChannel\.cs$') { return 'Behavior event channel - noi graph C#.' }
    if ($rel -match 'Utility\.cs$') { return 'Static utility - logic dung chung.' }
    if ($rel -match 'Planner\.cs$') { return 'AI planner - sinh intent.' }
    if ($rel -match 'Manager\.cs$') { return 'AI manager - dieu phoi planner.' }
    if ($rel -match '^UI/.*UI\.cs$') { return 'Component UI - bind du lieu len canvas.' }
    if ($rel -match 'Binder\.cs$') { return 'Binder UI - populate scroll/list.' }
    if ($rel -match 'Controller\.cs$') { return 'Controller scene/UI - dieu phoi tuong tac.' }
    if ($rel -match '^I[A-Z].*\.cs$') { return 'Interface - hop dong API gameplay.' }
    if ($rel -match 'Editor/.*\.cs$') { return 'Editor tool - chi chay trong Unity Editor.' }
    $n = [IO.Path]::GetFileNameWithoutExtension($rel)
    return "Type $n - xem file."
}

function Get-Desc([string]$fullPath, [string]$rel) {
    if ($overrides.ContainsKey($rel)) { return $overrides[$rel] }
    if ($moduleContext.ContainsKey($rel)) { return $moduleContext[$rel] }
    $text = [IO.File]::ReadAllText($fullPath, [Text.UTF8Encoding]::new($false))
    if ($text.Length -gt 16000) { $text = $text.Substring(0, 16000) }
    $s = Get-SummaryFromText $text
    if ($s) { return $s }
    return Get-HeuristicDesc $rel
}

$files = Get-ChildItem $root -Filter *.cs -Recurse -File | Sort-Object FullName
$today = Get-Date -Format 'yyyy-MM-dd'
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('# Script Catalog - Assets/Scripts')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("> Cap nhat: $today")
[void]$sb.AppendLine("> Pham vi: $($files.Count) file .cs trong Assets/Scripts/")
[void]$sb.AppendLine('> Dinh dang: moi script 1-2 dong mo ta chuc nang chinh.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Danh sach theo thu muc (A-Z). Tra cuu: Ctrl+F ten file.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Xem them module + luong doc: Assets-Scripts-By-Module.md')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('---')
[void]$sb.AppendLine('')

$currentFolder = $null
foreach ($f in $files) {
    $rel = $f.FullName.Substring($root.Length + 1).Replace('\', '/')
    $folder = Split-Path $rel -Parent
    if ($folder -ne $currentFolder) {
        if ($null -ne $currentFolder) { [void]$sb.AppendLine('') }
        $currentFolder = $folder
        if ([string]::IsNullOrEmpty($folder)) {
            [void]$sb.AppendLine('## (root)')
        } else {
            [void]$sb.AppendLine("## $folder")
        }
        [void]$sb.AppendLine('')
    }
    $desc = Get-Desc $f.FullName $rel
    $name = Split-Path $rel -Leaf
    [void]$sb.AppendLine("- **$name** ($rel) - $desc")
}

[void]$sb.AppendLine('')
[void]$sb.AppendLine('---')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Sinh tu dong: docs/_gen-script-catalog.ps1')

[System.IO.File]::WriteAllText($outPath, $sb.ToString(), [Text.UTF8Encoding]::new($false))
Write-Host "OK $($files.Count) -> $outPath"
