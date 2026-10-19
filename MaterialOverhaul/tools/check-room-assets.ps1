<#
.SYNOPSIS
    核对房间配置里引用的原版 prefab／房间 id 是否存在于目标游戏程序集。

.DESCRIPTION
    只读 Assembly-CSharp.dll 的 UTF-16 字符串，不加载游戏、不修改任何文件。
    检查来源：
      - RoomBuildingCatalogue.cs 的 const 字符串；
      - RoomCatalogue.cs 中除本 Mod 房间 id、本地化键、DLC id、分类名以外的字符串；
      - RoomCatalogue.cs 中 Beats／ExistingRoomId 引用的原版房间 id。
    找不到的 id 会逐项报告并以非零退出码结束。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File MaterialOverhaul\tools\check-room-assets.ps1 `
        -GameDll 'E:\SteamLibrary\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed\Assembly-CSharp.dll'
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameDll
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $GameDll)) { throw "找不到游戏程序集：$GameDll" }

$projectRoot = Split-Path -Parent $PSScriptRoot          # MaterialOverhaul
$workspaceRoot = Split-Path -Parent $projectRoot         # 仓库根
$roomsDir = Join-Path $projectRoot 'Rooms'
$cataloguePath = Join-Path $roomsDir 'RoomBuildingCatalogue.cs'
$roomCataloguePath = Join-Path $roomsDir 'RoomCatalogue.cs'
foreach ($path in @($cataloguePath, $roomCataloguePath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "找不到房间配置：$path" }
}

# 目标程序集里的 UTF-16LE 字符串足以覆盖用户字符串堆与方法体字符串常量。
$bytes = [System.IO.File]::ReadAllBytes($GameDll)
# 元数据堆在文件中的起始奇偶性不确定，两个偏移都解码后搜索，覆盖所有 UTF-16LE 字符串。
$textEven = [System.Text.Encoding]::Unicode.GetString($bytes, 0, $bytes.Length - 1)
$textOdd = [System.Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1)

# 不是原版 prefab／房间 id 的字符串。
$ignored = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($name in @(
    'Bathroom', 'Park', 'Industrial', 'Hospital', 'Recreation', 'Science', 'None', 'Neutral',
    'Bedroom', 'GreatHall', 'MessHall', 'RecRoom', 'Laboratory', 'NatureReserve', 'BanquetHall',
    'Private Bedroom', 'Default'
)) { [void]$ignored.Add($name) }

function Test-IsIgnored([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $true }
    if ($value.StartsWith('MO_')) { return $true }
    if ($value.StartsWith('STRINGS.')) { return $true }
    if ($value.StartsWith('DLC')) { return $true }
    if ($value.StartsWith('EXPANSION')) { return $true }
    if ($value -match '[\s{}]') { return $false }
    return $ignored.Contains($value)
}

$ids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

# 1) RoomBuildingCatalogue 的 const 字符串。
$catalogue = Get-Content -LiteralPath $cataloguePath -Raw -Encoding utf8
foreach ($match in [regex]::Matches($catalogue, 'public const string \w+ = "([^"]+)"')) {
    [void]$ids.Add($match.Groups[1].Value)
}

# 2) RoomCatalogue 的字符串常量（过滤本地化键、房间 id 等）。
$roomCatalogue = Get-Content -LiteralPath $roomCataloguePath -Raw -Encoding utf8
foreach ($match in [regex]::Matches($roomCatalogue, '"([A-Za-z][A-Za-z0-9_ ]*)"')) {
    $value = $match.Groups[1].Value
    if (-not (Test-IsIgnored $value)) { [void]$ids.Add($value) }
}

# 3) Beats／ExistingRoomId 引用的原版房间 id。
foreach ($match in [regex]::Matches($roomCatalogue, '(?:Beats = new\[\] \{|ExistingRoomId = )([^\r\n]+)')) {
    foreach ($inner in [regex]::Matches($match.Groups[1].Value, '"([^"]+)"')) {
        [void]$ids.Add($inner.Groups[1].Value)
    }
}

$missing = [System.Collections.Generic.List[string]]::new()
foreach ($id in ($ids | Sort-Object)) {
    if ($textEven.IndexOf($id, [System.StringComparison]::Ordinal) -lt 0 -and
        $textOdd.IndexOf($id, [System.StringComparison]::Ordinal) -lt 0) {
        $missing.Add($id)
    }
}

if ($missing.Count -gt 0) {
    Write-Host "未在目标程序集中找到以下 id：" -ForegroundColor Red
    foreach ($id in $missing) { Write-Host "  - $id" -ForegroundColor Red }
    exit 1
}

Write-Host "房间资源核对通过：$($ids.Count) 个 prefab／房间 id 均存在于目标程序集。"
exit 0
