#!/usr/bin/env bash
# 云端（Linux）开发环境准备：安装 .NET SDK 与 PowerShell，并把 GameDir 指向私有仓库 ONI-ref 里的游戏程序集。
# 完成后即可照 AGENTS.md 第6节运行 build-profiles.ps1 与源码级测试；部署和进游戏验证仍只能在用户电脑上进行。
# 用法：bash MaterialOverhaul/tools/cloud-setup.sh [ONI-ref 检出目录]
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
managed_rel="game/OxygenNotIncluded_Data/Managed/Assembly-CSharp.dll"

# ---- 找到 ONI-ref：参数 > 环境变量 ONI_REF > /home/user 下的检出 > 现场克隆 ----
ref="${1:-${ONI_REF:-}}"
if [ -z "$ref" ]; then
  for d in /home/user/*/; do
    if [ -f "$d$managed_rel" ]; then ref="${d%/}"; break; fi
  done
fi
if [ -z "$ref" ]; then
  ref=/home/user/ONI-ref
  git clone --depth 1 https://github.com/graduallyfar/ONI-ref "$ref"
fi
if [ ! -f "$ref/$managed_rel" ]; then
  echo "在 '$ref' 找不到 $managed_rel，请确认 ONI-ref 已检出。" >&2
  exit 1
fi

# ---- .NET SDK（tests 需要 net10.0；主工程 netstandard2.1 也用它构建）----
if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  apt-get update -q
  DEBIAN_FRONTEND=noninteractive apt-get install -y -q dotnet-sdk-10.0
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# ---- PowerShell（build-profiles.ps1 需要）----
export PATH="$HOME/.dotnet/tools:$PATH"
if ! command -v pwsh >/dev/null; then
  dotnet tool install -g PowerShell
fi

# ---- local.props（不提交）----
cat > "$repo_root/MaterialOverhaul/local.props" <<EOF
<Project>
  <PropertyGroup>
    <GameDir>$ref/game</GameDir>
  </PropertyGroup>
</Project>
EOF

echo "云端环境就绪：GameDir=$ref/game"
echo "使用前执行：export PATH=\"\$HOME/.dotnet/tools:\$PATH\""
