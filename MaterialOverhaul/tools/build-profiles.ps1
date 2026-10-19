<# 构建独立系统包；默认生成工业、电力、整合三包，不部署、不改存档。 #>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Industrial', 'Power', 'Combined')]
    [string[]]$Profile = @('Industrial', 'Power', 'Combined'),
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$canonicalProfiles = @{ industrial = 'Industrial'; power = 'Power'; combined = 'Combined' }
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

if ($Profile -contains 'Power' -or $Profile -contains 'Combined') {
    $managedDir = (& dotnet msbuild $projectFile -getProperty:ManagedDir).Trim()
    if ($LASTEXITCODE -ne 0) { throw '无法读取游戏依赖路径。' }
    & dotnet run --project $checkerProject -c $Configuration -- --game (Join-Path $managedDir 'Assembly-CSharp.dll')
    if ($LASTEXITCODE -ne 0) { throw '目标电力结算IL契约不匹配。' }
}

foreach ($mode in @($Profile | Select-Object -Unique)) {
    # 读取MSBuild求值后的版本，避免Power专属版本影响工业包。
    $version = (& dotnet msbuild $projectFile "-p:SystemProfile=$mode" -getProperty:Version).Trim()
    if ($LASTEXITCODE -ne 0 -or $version -notmatch '^\d+\.\d+\.\d+$') { throw "$mode 版本读取失败。" }
    $settings = switch ($mode) {
        'Industrial' { @{ Title = '材料革新 · 工业独立验证'; Description = '仅启用工业温控、加热、订单、磨损及材料改造；电力输配电沿用原版。'; Industrial = $true; Power = $false } }
        'Power' { @{ Title = '材料革新 · 电力独立验证'; Description = '电力0.0.2：最短线路、材料损耗、最近供电、电池与变压器分账及电网信息卡；工业保持原版。使用首轮测试初值，游戏和平衡待验收。'; Industrial = $false; Power = $true } }
        'Combined' { @{ Title = '材料革新 · 完整整合验证'; Description = '工业与电力整合验证候选；包含温控、订单、磨损与电力损耗。游戏交互与平衡待验收。'; Industrial = $true; Power = $true } }
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
        $isPower = $context.Groups[1].Value.StartsWith('STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.POWER_', [StringComparison]::Ordinal)
        if ($mode -eq 'Combined' -or ($mode -eq 'Power' -and $isPower) -or ($mode -eq 'Industrial' -and !$isPower)) { $block }
    }
    [System.IO.File]::WriteAllText((Join-Path $stringsDir 'zh.po'), ($selectedBlocks -join "`n`n") + "`n", $utf8)
    $fileHashes = [ordered]@{}
    foreach ($name in @('MaterialOverhaul.dll', 'MaterialOverhaul.pdb', 'mod.yaml', 'mod_info.yaml', 'strings/zh.po')) {
        $fileHashes[$name] = (Get-FileHash -LiteralPath (Join-Path $packageDir $name) -Algorithm SHA256).Hash
    }
    $manifest = [ordered]@{
        schemaVersion = 1; profile = $mode; version = [string]$version; configuration = $Configuration
        industrialEnabled = $settings.Industrial; powerEnabled = $settings.Power
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
