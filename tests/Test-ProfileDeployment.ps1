<# 在工作区_build内验证部署切换与拒绝路径；不访问真实Mod目录或存档。先生成三个验证包。 #>
#Requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $workspaceRoot ('_build\profile-deployment-tests\' + [Guid]::NewGuid().ToString('N'))
$mods = Join-Path $fixture 'mods'
$target = Join-Path $mods 'local\MaterialOverhaul'
$deploy = Join-Path $workspaceRoot 'MaterialOverhaul\tools\deploy.ps1'
$checker = Join-Path $workspaceRoot 'tests\MaterialOverhaul.ProfileChecks\MaterialOverhaul.ProfileChecks.csproj'
$checks = 0
function Assert([bool]$condition, [string]$message) {
    $script:checks++
    if (!$condition) { throw $message }
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $target 'keep.txt'), 'existing configuration')
$mandatory = @((Get-Command $deploy).Parameters['Profile'].Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] -and $_.Mandatory })
Assert ($mandatory.Count -eq 1) '部署必须明确提供Profile。'
$failed = $false
try { & $deploy -Profile 'Unknown' -ModsDir $mods -SkipBuild } catch { $failed = $true }
Assert $failed '未知部署模式必须拒绝。'
$invalidBuild = & dotnet build (Join-Path $workspaceRoot 'MaterialOverhaul\MaterialOverhaul.csproj') -c Release -p:SystemProfile=Unknown 2>&1
Assert ($LASTEXITCODE -ne 0) '未知MSBuild模式必须拒绝。'

$powerPackage = Join-Path $workspaceRoot '_dist\validation\Power\MaterialOverhaul'
$wrongMode = & dotnet run --no-build --project $checker -c Release -- Industrial $powerPackage 2>&1
Assert ($LASTEXITCODE -ne 0) 'Power包不得作为Industrial部署。'
$corrupt = Join-Path $fixture 'corrupt-package'
Copy-Item -LiteralPath $powerPackage -Destination $corrupt -Recurse
Add-Content -LiteralPath (Join-Path $corrupt 'strings\zh.po') -Value 'tampered'
$invalidHash = & dotnet run --no-build --project $checker -c Release -- Power $corrupt 2>&1
Assert ($LASTEXITCODE -ne 0) '被修改的验证包必须拒绝。'
$wrongVersion = Join-Path $fixture 'wrong-version-package'
Copy-Item -LiteralPath $powerPackage -Destination $wrongVersion -Recurse
$versionManifestPath = Join-Path $wrongVersion 'build_profile.json'
$versionManifest = Get-Content -LiteralPath $versionManifestPath -Raw | ConvertFrom-Json
$versionManifest.version = '0.2.2'
[IO.File]::WriteAllText($versionManifestPath, ($versionManifest | ConvertTo-Json -Depth 5))
$invalidVersion = & dotnet run --no-build --project $checker -c Release -- Power $wrongVersion 2>&1
Assert ($LASTEXITCODE -ne 0) 'Power包不得混用工业版本号，即使文件哈希都正确。'

foreach ($mode in @('Power', 'Industrial', 'Combined', 'Power')) {
    & $deploy -Profile $mode -ModsDir $mods -SkipBuild | Out-Null
    $manifest = Get-Content -LiteralPath (Join-Path $target 'build_profile.json') -Raw | ConvertFrom-Json
    Assert ($manifest.profile -eq $mode) "$mode 身份应与请求一致。"
    $expectedVersion = if ($mode -eq 'Power') { '0.0.2' } elseif ($mode -eq 'Combined') { '1.0.3' } else { '0.2.2' }
    Assert ($manifest.version -eq $expectedVersion) "$mode 应使用对应系统版本号。"
    Assert ($manifest.industrialEnabled -eq ($mode -ne 'Power')) "$mode 工业启用状态不符。"
    Assert ($manifest.powerEnabled -eq ($mode -ne 'Industrial')) "$mode 电力启用状态不符。"
    $keys = @(Select-String -LiteralPath (Join-Path $target 'strings\zh.po') -Pattern '^msgctxt ')
    $expected = if ($mode -eq 'Power') { 14 } elseif ($mode -eq 'Industrial') { 89 } elseif ($mode -eq 'Room') { 167 } else { 294 }
    Assert ($keys.Count -eq $expected) "$mode 本地化文件未完整替换。"
    Assert ((Get-Content -LiteralPath (Join-Path $target 'keep.txt') -Raw) -eq 'existing configuration') '部署不应覆盖无关配置。'
    foreach ($file in $manifest.files.PSObject.Properties) {
        Assert ((Get-FileHash -LiteralPath (Join-Path $target $file.Name) -Algorithm SHA256).Hash -eq $file.Value) "$mode 部署哈希不符。"
    }
}
$before = (Get-FileHash -LiteralPath (Join-Path $target 'MaterialOverhaul.dll') -Algorithm SHA256).Hash
[IO.File]::WriteAllText((Join-Path $target 'Unexpected.dll'), 'foreign assembly')
$failed = $false
try { & $deploy -Profile Industrial -ModsDir $mods -SkipBuild | Out-Null } catch { $failed = $true }
Assert $failed '目标目录混入其他DLL时必须在复制前拒绝。'
Assert ((Get-FileHash -LiteralPath (Join-Path $target 'MaterialOverhaul.dll') -Algorithm SHA256).Hash -eq $before) '拒绝部署不应改写原DLL。'
Write-Host "部署切换与拒绝路径 $checks 项检查通过；临时测试目录：$fixture"
