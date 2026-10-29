# Material Overhaul（材料革新）现状梳理

> 依据：共享文件夹 `ONI Mod/` 的只读快照（2026-10-20）。未修改任何文件。
> 快照里**没有** `docs/`、`tests/` 和 `.git`，所以设计文档、测试与提交历史无法核对；下面凡涉及这些的说法都来自 README / AGENTS.md / 代码注释，属于转述而非核实。

## 1. 框架与工程

| 项 | 现状 | 依据 |
| --- | --- | --- |
| 框架 | 原版 `KMod.UserMod2` + 游戏自带 **Harmony 2.4.2**，无 PLib | `MaterialOverhaul.csproj`、`Game/MaterialOverhaulMod.cs` |
| 目标 | `netstandard2.1`，游戏 build ≥ 744825，`supportedContent: ALL` | csproj、`mod/mod_info.yaml` |
| 规模 | 约 130 个 .cs、约 1.67 万行；Harmony 补丁 18 个文件 | 统计 |
| 本地化 | `mod/strings/zh.po`（约 225 条），`LocString.CreateLocStringKeys` 建键后手动加载 PO | `MaterialOverhaulMod.cs` |
| 构建模式 | 一套源码、四种 `SystemProfile`：Industrial / Power / Room / Combined，用 `<Compile Remove>` 真正剔除其他系统；**不带参数默认构建 Power** | csproj |
| 工具 | `tools/build-profiles.ps1`（打四个包 + IL 契约检查）、`deploy.ps1`、`check-room-assets.ps1` | tools/ |

## 2. 已实现的系统

| 系统 | 版本 | 代码 | 验证状态（按 README/日志文本） |
| --- | --- | --- | --- |
| 工业：温区、预热补热（20–4000 W）、订单排程、硬度磨损、自动化端口、材料过热修正 | 0.2.2 | 完整，29 栋建筑 / 60 个配方候选 | 原 6 栋游戏内跑过；第三批 23 栋只做了源码级回归，**未游戏验收** |
| 电力：最短路径线损、材料电导率、电池/变压器分账、电网信息卡 | 0.0.2 | 完整 | 构建与替身检查完成，**游戏与平衡未验收** |
| 房间：16 个新房间、2 项原版改造、平均装饰度/温度条件 | 无独立版本号 | 部分：构成与条件已编入，**士气/压力/生产/医疗/科研/航天效果未接入** | 未验收 |
| 辐射 | — | 无 | 仅设计 |

## 3. 能否编译

- **这里编不了**：云端容器没有 `dotnet`，也没有游戏程序集（`Assembly-CSharp.dll` 等只在你本机 `E:\SteamLibrary\...`）。
- **间接证据显示你本机能编过**：`_build/profiles/` 下四种模式的 DLL 都在（Combined 252 KB、Industrial 153 KB、Room 71 KB、Power 50 KB）；`_dist/validation/` 有 Combined 与 Industrial 的包和 ZIP。
- 最近一次部署（`_dist/deployment-backups/20261019-230628`）是 **Room 模式**，`sourceDirty: true`，即用未提交的源码打的包。

## 4. 发现的不一致

1. README 写房间系统"未实装 / 代码未开始"，但代码里房间系统已编入（16 个房间、30 处补丁），且最近部署的就是 Room 包。README 需要更新。
2. `mod.yaml` 的标题/描述是"电力独立验证"，而 csproj 默认也是 Power；如果直接 `dotnet build` 后手动拷贝，装进游戏的会是电力版，与 README 首屏写的"工业版 0.2.2"容易混淆（build-profiles.ps1 会按模式重写 mod.yaml，所以走脚本没问题）。
3. `_dist/validation/` 里只有 Combined、Industrial，没有 Power、Room 的包，但 Room 已经部署过；说明打包和部署不是同一次流程产物。

## 5. 最优先的下一步

1. **把已部署的版本做游戏内验收**：Industrial 第三批 23 栋、Power 0.0.2 都卡在"代码完成、未游戏验收"。建议按一次一个模式、专用测试存档，先 Industrial（改动面最大、离可发布最近），把结果记进开发记录。
2. **提交并收口房间系统的当前状态**：最近的 Room 包来自脏工作区，先把稳定部分提交到 `codex/room-system`（或你约定的分支），再决定是先接入剩余效果（士气、压力、生产效率等）还是先验收已有构成/条件。
3. **同步 README 与默认构建**：更新房间系统状态描述，并考虑把 csproj 默认 Profile 从 Power 改为 Combined 或强制显式指定，避免误装。
