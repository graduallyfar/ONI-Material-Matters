<#
.SYNOPSIS
    构建 MaterialOverhaul 并部署到本机缺氧的本地 Mod 目录。

.DESCRIPTION
    只写入 <Klei 数据目录>\mods\local\MaterialOverhaul：
      - 不触碰 Steam 订阅目录（mods\Steam）
      - 不触碰任何存档
      - 不把游戏原版程序集复制进部署目录（脚本会检查并拒绝）
    本地 mods 根目录缺省自动探测，也可用 -ModsDir 显式指定。
#>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Industrial', 'Power', 'Room', 'Combined', 'Radiation')]
    [string]$Profile,
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [string]$ModsDir,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$Profile = @{ industrial = 'Industrial'; power = 'Power'; room = 'Room'; combined = 'Combined'; radiation = 'Radiation' }[$Profile]

$projectRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $projectRoot
$buildDir = Join-Path $workspaceRoot "_dist\validation\$Profile\MaterialOverhaul"
$modFolderName = 'MaterialOverhaul'

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build-profiles.ps1') -Profile $Profile -Configuration $Configuration
}

# 即使跳过构建，也要检查包清单、DLL内嵌身份、功能补丁和所有部署文件哈希。
$checkerProject = Join-Path $workspaceRoot 'tests\MaterialOverhaul.ProfileChecks\MaterialOverhaul.ProfileChecks.csproj'
& dotnet run --project $checkerProject -c Release -- $Profile $buildDir
if ($LASTEXITCODE -ne 0) { throw '验证包检查失败，部署未执行。' }

if (-not $ModsDir) {
    $candidates = @(
        (Join-Path $env:USERPROFILE 'Documents\Klei\OxygenNotIncluded\mods'),
        'D:\Users\Documents\Klei\OxygenNotIncluded\mods'
    )
    $ModsDir = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $ModsDir) { throw '找不到 Klei 的 mods 目录，请用 -ModsDir 指定。' }

$target = Join-Path (Join-Path $ModsDir 'local') $modFolderName
if (Test-Path -LiteralPath $target) {
    $extraDlls = @(Get-ChildItem -LiteralPath $target -Filter '*.dll' -File -Recurse | Where-Object { $_.Name -ne 'MaterialOverhaul.dll' })
    if ($extraDlls.Count -gt 0) { throw '目标目录含额外DLL，请先明确其用途，避免混合加载。' }
    # 仅备份本Mod已知文件；不移动或删除存档、未知配置或其他Mod。
    $backup = Join-Path $workspaceRoot ('_dist\deployment-backups\' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '\MaterialOverhaul')
    foreach ($name in @('MaterialOverhaul.dll', 'MaterialOverhaul.pdb', 'mod.yaml', 'mod_info.yaml', 'strings\zh.po', 'build_profile.json')) {
        $oldFile = Join-Path $target $name
        if (Test-Path -LiteralPath $oldFile) {
            $backupFile = Join-Path $backup $name
            New-Item -ItemType Directory -Path (Split-Path -Parent $backupFile) -Force | Out-Null
            Copy-Item -LiteralPath $oldFile -Destination $backupFile -Force
        }
    }
    Write-Host "上一部署备份：$backup"
}
New-Item -ItemType Directory -Path $target -Force | Out-Null

$dll = Join-Path $buildDir 'MaterialOverhaul.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw "找不到构建产物 $dll" }

Copy-Item -LiteralPath $dll -Destination $target -Force
$pdb = Join-Path $buildDir 'MaterialOverhaul.pdb'
if (Test-Path -LiteralPath $pdb) { Copy-Item -LiteralPath $pdb -Destination $target -Force }

foreach ($meta in @('mod.yaml', 'mod_info.yaml')) {
    Copy-Item -LiteralPath (Join-Path $buildDir $meta) -Destination $target -Force
}

# 本地化：原版只扫描 mod 目录下的 strings\*.po（Mod.LoadStrings 只接受 .po 扩展名），
# 其它目录名（例如 translations）与 .json/.hjson 都不会被读取，会导致界面显示 MISSING.STRINGS.*。
$strings = Join-Path $buildDir 'strings'
if (Test-Path -LiteralPath $strings) {
    $destStrings = Join-Path $target 'strings'
    New-Item -ItemType Directory -Path $destStrings -Force | Out-Null
    Copy-Item -Path (Join-Path $strings '*') -Destination $destStrings -Recurse -Force
}

Copy-Item -LiteralPath (Join-Path $buildDir 'build_profile.json') -Destination $target -Force
$manifest = Get-Content -LiteralPath (Join-Path $target 'build_profile.json') -Raw | ConvertFrom-Json
foreach ($file in $manifest.files.PSObject.Properties) {
    if ((Get-FileHash -LiteralPath (Join-Path $target $file.Name) -Algorithm SHA256).Hash -ne $file.Value) { throw "部署文件不一致：$($file.Name)" }
}

$leaked = Get-ChildItem -LiteralPath $target -Filter 'Assembly-CSharp*.dll' -ErrorAction SilentlyContinue
if ($leaked) { throw "部署目录出现原版程序集，已中止：$($leaked.FullName -join ', ')" }

# 按版本存档本次部署的文件：放在主检出的 deployed\ 下（从工作树部署也存到同一处），不提交。
# 整合版以版本号命名，独立验证包加系统名（工业与房间同为 0.2.2）；同一版本再次部署时覆盖。
$mainRoot = $workspaceRoot
$commonDir = (& git -C $workspaceRoot rev-parse --path-format=absolute --git-common-dir 2>$null)
if ($LASTEXITCODE -eq 0 -and $commonDir) { $mainRoot = Split-Path -Parent $commonDir.Trim() }
$archiveName = if ($Profile -eq 'Combined') { $manifest.version } else { "$Profile-$($manifest.version)" }
$archive = Join-Path (Join-Path (Join-Path $mainRoot 'deployed') $archiveName) $modFolderName
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Recurse -Force }
New-Item -ItemType Directory -Path $archive -Force | Out-Null
foreach ($name in @('MaterialOverhaul.dll', 'MaterialOverhaul.pdb', 'mod.yaml', 'mod_info.yaml', 'strings\zh.po', 'build_profile.json')) {
    $file = Join-Path $target $name
    if (Test-Path -LiteralPath $file) {
        $archiveFile = Join-Path $archive $name
        New-Item -ItemType Directory -Path (Split-Path -Parent $archiveFile) -Force | Out-Null
        Copy-Item -LiteralPath $file -Destination $archiveFile -Force
    }
}
Write-Host "本次部署已存档：$archive"

Write-Host "已部署 $Profile 到 $target"
Get-ChildItem -LiteralPath $target | Select-Object Length, Name
