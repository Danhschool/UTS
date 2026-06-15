# Gắn NetworkIdentity + RtsUtsNetworkEntity lên prefab gameplay (không cần mở batchmode).
# Chạy khi Unity đang mở — sau đó Unity sẽ reimport prefab.

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$niGuid = "9b91ecbcc199f4492b9a91e820070131"
$entityGuid = "33b57de20edc31046a3f95c630f5e6dc"
$gameplayMarkers = @(
    "GameDevTV.RTS.Units.BaseBuilding",
    "GameDevTV.RTS.Units.BaseMilitaryUnit",
    "GameDevTV.RTS.Units.Archer",
    "GameDevTV.RTS.Units.Worker",
    "GameDevTV.RTS.Units.Grenadier"
)

function New-UnityFileId {
    $rng = [System.Random]::new()
    $high = [uint32]$rng.Next(1, [int32]::MaxValue)
    $low = [uint32]$rng.Next(1, [int32]::MaxValue)
    $value = ([int64]$high -shl 32) -bor $low
    if ($value -gt 0) { return -$value }
    return $value
}

function Get-RootGameObjectId([string]$content) {
    $pattern = '(?ms)^--- !u!4 &(\d+)\r?\nTransform:(?<body>.*?)^  m_Father: \{fileID: 0\}'
    $matches = [regex]::Matches($content, $pattern)
    for ($i = 0; $i -lt $matches.Count; $i++) {
        $body = $matches[$i].Groups["body"].Value
        $goMatch = [regex]::Match($body, 'm_GameObject: \{fileID: (\d+)\}')
        if ($goMatch.Success) { return $goMatch.Groups[1].Value }
    }
    return $null
}

function Add-NetworkComponents([string]$path) {
    $content = [IO.File]::ReadAllText($path)
    if ($content.Contains($niGuid)) {
        Write-Host "Skip (has NI): $path"
        return $false
    }

    $isGameplay = $false
    foreach ($marker in $gameplayMarkers) {
        if ($content.Contains($marker)) {
            $isGameplay = $true
            break
        }
    }
    if (-not $isGameplay) {
        return $false
    }

    if ($path -match "ghost" -or $path -match "\\UI\\" -or $path -match "\\Tree\\" -or $path -match "\\Rock\\" -or $path -match "\\Animal\\") {
        return $false
    }

    $goId = Get-RootGameObjectId $content
    if (-not $goId) {
        Write-Warning "No root GO: $path"
        return $false
    }

    $niId = New-UnityFileId
    $entityId = New-UnityFileId
    while ([string]$niId -eq [string]$entityId) { $entityId = New-UnityFileId }

    $goPattern = "(?ms)^(--- !u!1 &$goId\r?\nGameObject:.*?^  m_Component:\r?\n)(.*?)(^  m_Layer:)"
    $goMatch = [regex]::Match($content, $goPattern)
    if (-not $goMatch.Success) {
        Write-Warning "Cannot patch GameObject $goId : $path"
        return $false
    }

    $componentLines = $goMatch.Groups[2].Value.TrimEnd()
    $newComponents = $componentLines + "`r`n  - component: {fileID: $niId}`r`n  - component: {fileID: $entityId}`r`n"
    $content = $content.Replace($goMatch.Groups[0].Value, $goMatch.Groups[1].Value + $newComponents + $goMatch.Groups[3].Value)

    $blocks = @"
--- !u!114 &$niId
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $goId}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $niGuid, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Mirror::Mirror.NetworkIdentity
  sceneId: 0
  _assetId: 0
  serverOnly: 0
  visibility: 0
  hasSpawned: 0
--- !u!114 &$entityId
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $goId}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $entityGuid, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::GameDevTV.RTS.Netplay.RtsUtsNetworkEntity
  syncDirection: 0
  syncMode: 0
  syncInterval: 0
"@

    $content = $content.TrimEnd() + "`r`n" + $blocks + "`r`n"
    [IO.File]::WriteAllText($path, $content)
    Write-Host "Patched: $path"
    return $true
}

$scanRoots = @(
    (Join-Path $projectRoot "Assets\Prefab\Unit"),
    (Join-Path $projectRoot "Assets\Prefab\Buildings")
)

$patched = 0
foreach ($root in $scanRoots) {
    if (-not (Test-Path $root)) { continue }
    Get-ChildItem -Path $root -Filter *.prefab -Recurse | ForEach-Object {
        if (Add-NetworkComponents $_.FullName) { $script:patched++ }
    }
}

Write-Host "Done. Patched $patched prefab(s). In Unity: Assets -> Refresh."
