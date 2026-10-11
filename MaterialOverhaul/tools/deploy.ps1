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
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$ModsDir,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $projectRoot
$buildDir = Join-Path $workspaceRoot '_build'
$projectFile = Join-Path $projectRoot 'MaterialOverhaul.csproj'
$modFolderName = 'MaterialOverhaul'

if (-not $SkipBuild) {
    Write-Host "构建 $projectFile ($Configuration) ..."
    dotnet build $projectFile -c $Configuration -v minimal
    if ($LASTEXITCODE -ne 0) { throw "构建失败，退出码 $LASTEXITCODE" }
}

if (-not $ModsDir) {
    $candidates = @(
        (Join-Path $env:USERPROFILE 'Documents\Klei\OxygenNotIncluded\mods'),
        'D:\Users\Documents\Klei\OxygenNotIncluded\mods'
    )
    $ModsDir = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $ModsDir) { throw '找不到 Klei 的 mods 目录，请用 -ModsDir 指定。' }

$target = Join-Path (Join-Path $ModsDir 'local') $modFolderName
New-Item -ItemType Directory -Path $target -Force | Out-Null

$dll = Join-Path $buildDir 'MaterialOverhaul.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw "找不到构建产物 $dll" }

Copy-Item -LiteralPath $dll -Destination $target -Force
$pdb = Join-Path $buildDir 'MaterialOverhaul.pdb'
if (Test-Path -LiteralPath $pdb) { Copy-Item -LiteralPath $pdb -Destination $target -Force }

foreach ($meta in @('mod.yaml', 'mod_info.yaml')) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $projectRoot 'mod') $meta) -Destination $target -Force
}

$leaked = Get-ChildItem -LiteralPath $target -Filter 'Assembly-CSharp*.dll' -ErrorAction SilentlyContinue
if ($leaked) { throw "部署目录出现原版程序集，已中止：$($leaked.FullName -join ', ')" }

Write-Host "已部署到 $target"
Get-ChildItem -LiteralPath $target | Select-Object Length, Name
