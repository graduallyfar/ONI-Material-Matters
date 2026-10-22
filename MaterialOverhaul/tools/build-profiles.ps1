<# 构建系统包；默认生成工业、电力、房间、辐射四个独立验证包与 V1.0.1 整合包，不部署、不改存档。 #>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Industrial', 'Power', 'Room', 'Combined', 'Radiation')]
    [string[]]$Profile = @('Industrial', 'Power', 'Room', 'Radiation', 'Combined'),
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$canonicalProfiles = @{ industrial = 'Industrial'; power = 'Power'; room = 'Room'; combined = 'Combined'; radiation = 'Radiation' }
$Profile = @($Profile | ForEach-Object { $canonicalProfiles[$_] })
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$projectRoot = Join-Path $workspaceRoot 'MaterialOverhaul'
$projectFile = Join-Path $projectRoot 'MaterialOverhaul.csproj'
$checkerProject = Join-Path $workspaceRoot 'tests\MaterialOverhaul.ProfileChecks\MaterialOverhaul.ProfileChecks.csproj'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$commit = (& git -C $workspaceRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw '无法读取源码提交。' }
$changes = @(& git -C $workspaceRoot status --porcelain --untracked-files=normal | Where-Object { $_ -notmatch '^\?\? \.obsidian/' })
$sourceDirty = $changes.Count -gt 0
$poText = Get-Content -LiteralPath (Join-Path $projectRoot 'mod\strings\zh.po') -Raw -Encoding utf8
$poBlocks = [regex]::Split($poText.Trim(), '\r?\n\s*\r?\n')
$checkArgs = @()

if ($Profile -contains 'Power' -or $Profile -contains 'Room' -or $Profile -contains 'Combined') {
    $managedDir = (& dotnet msbuild $projectFile -getProperty:ManagedDir).Trim()
    if ($LASTEXITCODE -ne 0) { throw '无法读取游戏依赖路径。' }
    $gameAssembly = Join-Path $managedDir 'Assembly-CSharp.dll'
    if ($Profile -contains 'Power' -or $Profile -contains 'Combined') {
        & dotnet run --project $checkerProject -c $Configuration -- --game $gameAssembly
        if ($LASTEXITCODE -ne 0) { throw '目标电力结算IL契约不匹配。' }
    }
    if ($Profile -contains 'Room' -or $Profile -contains 'Combined') {
        & dotnet run --project $checkerProject -c $Configuration -- --room-game $gameAssembly
        if ($LASTEXITCODE -ne 0) { throw '目标房间补丁或反射字段契约不匹配。' }
        & (Join-Path $PSScriptRoot 'check-room-assets.ps1') -GameDll $gameAssembly
        if ($LASTEXITCODE -ne 0) { throw '房间配置引用的 prefab／房间 id 与目标程序集不匹配。' }
    }
}

foreach ($mode in @($Profile | Select-Object -Unique)) {
    # 读取MSBuild求值后的版本，避免Power专属版本影响工业包。
    $version = (& dotnet msbuild $projectFile "-p:SystemProfile=$mode" -getProperty:Version).Trim()
    if ($LASTEXITCODE -ne 0 -or $version -notmatch '^\d+\.\d+\.\d+$') { throw "$mode 版本读取失败。" }
    $settings = switch ($mode) {
        'Industrial' { @{ Title = '材料革新 · 工业独立验证'; Description = '仅启用工业温控、加热、订单、磨损及材料改造；电力输配电与房间沿用原版。'; Industrial = $true; Power = $false; Room = $false; Radiation = $false } }
        'Power' { @{ Title = '材料革新 · 电力独立验证'; Description = '电力0.0.2：最短线路、材料损耗、最近供电、电池与变压器分账及电网信息卡；工业保持原版。使用首轮测试初值，游戏和平衡待验收。'; Industrial = $false; Power = $true; Room = $false; Radiation = $false } }
        'Room' { @{ Title = '材料革新 · 房间独立验证'; Description = '房间系统：房间分级、平均装饰度与平均温度条件、新增房间构成；工业与电力保持原版。效果接入与游戏验收按开发记录逐步补齐。'; Industrial = $false; Power = $false; Room = $true; Radiation = $false } }
        'Combined' { @{ Title = '材料革新 V1.0.1 · 工业／电力／房间／辐射整合'; Description = 'V1.0.1 整合版：工业温控、加热、订单、磨损与材料改造；电力最短线路损耗、电池与变压器分账；房间分级、平均装饰度／温度条件与房间效果；辐射感生放射性（建筑活化与放射性污染物）。数值为工作初值，游戏验收与平衡待执行。'; Industrial = $true; Power = $true; Room = $true; Radiation = $true } }
        'Radiation' { @{ Title = '材料革新 · 辐射独立验证'; Description = '辐射0.0.1：感生放射性第一版。建筑受强辐射后累积活化度并自身放射；液体、气体、天然格带上放射性污染物（优先于其他病菌）。工业、电力、房间保持原版。数值为工作初值，游戏验收待执行。'; Industrial = $false; Power = $false; Room = $false; Radiation = $true } }
    }
    & dotnet build $projectFile -c $Configuration "-p:SystemProfile=$mode" -v minimal
    if ($LASTEXITCODE -ne 0) { throw "$mode 构建失败。" }
    $buildDir = Join-Path $workspaceRoot "_build\profiles\$mode"
    $packageDir = Join-Path $workspaceRoot "_dist\validation\$mode\MaterialOverhaul"
    $stringsDir = Join-Path $packageDir 'strings'
    New-Item -ItemType Directory -Path $stringsDir -Force | Out-Null
    foreach ($name in @('MaterialOverhaul.dll', 'MaterialOverhaul.pdb')) {
        Copy-Item -LiteralPath (Join-Path $buildDir $name) -Destination $packageDir -Force
    }
    [System.IO.File]::WriteAllText((Join-Path $packageDir 'mod.yaml'),
        "title: `"$($settings.Title)`"`ndescription: `"$($settings.Description)`"`nstaticID: `"MaterialOverhaul`"`n", $utf8)
    $modInfo = Get-Content -LiteralPath (Join-Path $projectRoot 'mod\mod_info.yaml') -Raw
    $modInfo = [regex]::Replace($modInfo, '(?m)^version:.*$', "version: `"$version`"")
    [System.IO.File]::WriteAllText((Join-Path $packageDir 'mod_info.yaml'), $modInfo, $utf8)
    $selectedBlocks = foreach ($block in $poBlocks) {
        $context = [regex]::Match($block, '(?m)^msgctxt "([^"\r\n]+)"\r?$')
        if (!$context.Success) { $block; continue }
        $key = $context.Groups[1].Value
        $isPower = $key.StartsWith('STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.POWER_', [StringComparison]::Ordinal)
        $isRoom = $key.StartsWith('STRINGS.ROOMS.MATERIALOVERHAUL.', [StringComparison]::Ordinal)
        $isRadiation = $key.StartsWith('STRINGS.RADIATION.MATERIALOVERHAUL.', [StringComparison]::Ordinal)
        if ($mode -eq 'Combined' -or ($mode -eq 'Power' -and $isPower) -or ($mode -eq 'Room' -and $isRoom) -or ($mode -eq 'Industrial' -and !$isPower -and !$isRoom -and !$isRadiation) -or ($mode -eq 'Radiation' -and $isRadiation)) { $block }
    }
    [System.IO.File]::WriteAllText((Join-Path $stringsDir 'zh.po'), ($selectedBlocks -join "`n`n") + "`n", $utf8)
    $fileHashes = [ordered]@{}
    foreach ($name in @('MaterialOverhaul.dll', 'MaterialOverhaul.pdb', 'mod.yaml', 'mod_info.yaml', 'strings/zh.po')) {
        $fileHashes[$name] = (Get-FileHash -LiteralPath (Join-Path $packageDir $name) -Algorithm SHA256).Hash
    }
    $manifest = [ordered]@{
        schemaVersion = 1; profile = $mode; version = [string]$version; configuration = $Configuration
        industrialEnabled = $settings.Industrial; powerEnabled = $settings.Power; roomEnabled = $settings.Room; radiationEnabled = $settings.Radiation
        sourceCommit = $commit; sourceDirty = $sourceDirty; files = $fileHashes
    }
    [System.IO.File]::WriteAllText((Join-Path $packageDir 'build_profile.json'), ($manifest | ConvertTo-Json -Depth 5) + "`n", $utf8)
    $checkArgs += @($mode, $packageDir)
}

# 对实际生成的DLL、补丁类型、文本字段与包哈希作验证；不加载游戏程序集。
& dotnet run --project $checkerProject -c $Configuration -- @checkArgs
if ($LASTEXITCODE -ne 0) { throw '隔离验证未通过，不生成分发压缩包。' }
for ($i = 0; $i -lt $checkArgs.Count; $i += 2) {
    $mode = $checkArgs[$i]; $packageDir = $checkArgs[$i + 1]
    $zip = Join-Path $workspaceRoot "_dist\validation\$mode.zip"
    Compress-Archive -LiteralPath $packageDir -DestinationPath $zip -Force
    Write-Host "验证包：$packageDir"
    Write-Host "压缩包：$zip"
}
