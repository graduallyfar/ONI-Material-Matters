using System.Collections.Generic;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 房间判定纯逻辑（V3.0.0 P1）：平均环境、附加条件、冲突／覆盖与目录完整性。
    /// 用构造快照验证，不加载游戏程序集；不替代游戏内房间识别与界面验收。
    /// </summary>
    public static class RoomTests
    {
        public static void Run()
        {
            AverageEnvironment();
            SizeAndCounts();
            EnvironmentThresholds();
            RetainMargins();
            PrefabRequirements();
            CategoryRequirements();
            Arbitration();
            RetainArbitration();
            CatalogueIntegrity();
            Production();
            MaterialDecor();
            Traits();
        }

        private static RoomSnapshot Snapshot(int cells)
        {
            return new RoomSnapshot { CellCount = cells, AverageDecor = 0f, AverageTemperatureK = 293.15f };
        }

        private static void AverageEnvironment()
        {
            TestRunner.Section("平均环境：真空跳过、NaN 表示无采样");

            RoomCellSample[] cells =
            {
                new RoomCellSample { TemperatureK = 280f, Decor = 10f, HasElement = true },
                new RoomCellSample { TemperatureK = 300f, Decor = 20f, HasElement = true },
                new RoomCellSample { TemperatureK = 500f, Decor = 0f, HasElement = false }
            };
            TestRunner.CheckClose(290f, RoomEnvironment.AverageTemperature(cells), 0.001f, "平均温度只计有元素格");
            TestRunner.CheckClose(10f, RoomEnvironment.AverageDecor(cells), 0.001f, "平均装饰度计全部格子，真空格按 0");

            RoomCellSample[] vacuumOnly =
            {
                new RoomCellSample { TemperatureK = 300f, Decor = 0f, HasElement = false }
            };
            TestRunner.Check(float.IsNaN(RoomEnvironment.AverageTemperature(vacuumOnly)), "全真空 → 温度 NaN");
            TestRunner.CheckClose(0f, RoomEnvironment.AverageDecor(vacuumOnly), 0.001f, "全真空仍可算装饰度 0");
            TestRunner.Check(float.IsNaN(RoomEnvironment.AverageTemperature(new RoomCellSample[0])), "空格集合 → 温度 NaN");
            TestRunner.Check(float.IsNaN(RoomEnvironment.AverageDecor(null)), "null 集合 → 装饰度 NaN");
            TestRunner.Check(RoomEnvironment.ApproximatelyEqual(293.15f, 293.151f, 0.01f), "容差内视为未变化");
            TestRunner.Check(!RoomEnvironment.ApproximatelyEqual(293.15f, 293.20f, 0.01f), "超出容差视为变化");
            TestRunner.Check(RoomEnvironment.ApproximatelyEqual(float.NaN, float.NaN, 0.01f), "两个缺失值视为相同");
            TestRunner.Check(!RoomEnvironment.ApproximatelyEqual(float.NaN, 293.15f, 0.01f), "缺失与有值视为变化");
        }

        private static void SizeAndCounts()
        {
            TestRunner.Section("尺寸与数量边界（含端点）");

            RoomSnapshot snapshot = Snapshot(12);
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.MinimumSize(12), snapshot), "12 格满足下限 12");
            TestRunner.Check(!RoomEvaluator.IsSatisfied(RoomRequirement.MinimumSize(13), snapshot), "12 格不满足下限 13");
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.MaximumSize(12), snapshot), "12 格满足上限 12");

            snapshot.PlantCount = 3;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(RoomRequirement.MinimumPlants(4), snapshot), "3 株不满足 4 株");
            snapshot.PlantCount = 4;
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.MinimumPlants(4), snapshot), "4 株满足 4 株");
        }

        private static void EnvironmentThresholds()
        {
            TestRunner.Section("装饰度与温度阈值");

            RoomSnapshot snapshot = Snapshot(20);
            snapshot.AverageDecor = 80f;
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.MinimumDecor(80f), snapshot), "装饰度恰好 +80 满足（含）");
            snapshot.AverageDecor = 79.9f;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(RoomRequirement.MinimumDecor(80f), snapshot), "装饰度 +79.9 不满足 +80");
            snapshot.AverageDecor = float.NaN;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(RoomRequirement.MinimumDecor(80f), snapshot), "无装饰采样不自动通过");

            snapshot.AverageTemperatureK = 293.15f;
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.TemperatureRange(20f, 26f), snapshot), "20 ℃ 下限含端点");
            snapshot.AverageTemperatureK = 299.15f;
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.TemperatureRange(20f, 26f), snapshot), "26 ℃ 上限含端点");
            snapshot.AverageTemperatureK = 299.15f + 0.01f;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(RoomRequirement.TemperatureRange(20f, 26f), snapshot), "26.01 ℃ 超出上限");

            snapshot.AverageTemperatureK = 293.1399f;
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.TemperatureBelow(20f), snapshot), "严格低于 20 ℃ 通过");
            snapshot.AverageTemperatureK = 293.15f;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(RoomRequirement.TemperatureBelow(20f), snapshot), "恰好 20 ℃ 不满足「低于 20」");

            snapshot.AverageTemperatureK = float.NaN;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(RoomRequirement.TemperatureRange(20f, 26f), snapshot), "无温度采样 → 温区不通过");
            TestRunner.Check(!RoomEvaluator.IsSatisfied(RoomRequirement.TemperatureBelow(20f), snapshot), "无温度采样 → 低温条件不通过");
        }

        private static void RetainMargins()
        {
            TestRunner.Section("保持回差：已是该房间时放宽环境阈值");

            TestRunner.CheckClose(5f, RoomEvaluator.DecorRetainMargin, 0.0001f, "装饰度回差 5 点");
            TestRunner.CheckClose(1f, RoomEvaluator.TemperatureRetainMargin, 0.0001f, "温度回差两端各 1 ℃");

            RoomDefinition dance = Find("MO_DanceHall");
            RoomRequirement decor = RoomRequirement.MinimumDecor(80f);
            RoomSnapshot snapshot = Snapshot(20);

            snapshot.AverageDecor = 75f;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(dance, decor, snapshot, false), "新判定：装饰度 +75 不满足 +80");
            TestRunner.Check(RoomEvaluator.IsSatisfied(dance, decor, snapshot, true), "保持：装饰度 +75 仍满足 +80（含回差端点）");
            snapshot.AverageDecor = 74.9f;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(dance, decor, snapshot, true), "保持：装饰度 +74.9 超出回差");
            snapshot.AverageDecor = float.NaN;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(dance, decor, snapshot, true), "保持：无装饰采样仍不通过");

            RoomRequirement range = RoomRequirement.TemperatureRange(20f, 26f);
            snapshot.AverageTemperatureK = 299.15f + 0.9f;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(dance, range, snapshot, false), "新判定：26.9 ℃ 超出 20–26 ℃");
            TestRunner.Check(RoomEvaluator.IsSatisfied(dance, range, snapshot, true), "保持：26.9 ℃ 在上限回差内");
            snapshot.AverageTemperatureK = 299.15f + 1.1f;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(dance, range, snapshot, true), "保持：27.1 ℃ 超出上限回差");
            snapshot.AverageTemperatureK = 293.15f - 0.9f;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(dance, range, snapshot, false), "新判定：19.1 ℃ 低于 20–26 ℃");
            TestRunner.Check(RoomEvaluator.IsSatisfied(dance, range, snapshot, true), "保持：19.1 ℃ 在下限回差内");
            snapshot.AverageTemperatureK = 293.15f - 1.1f;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(dance, range, snapshot, true), "保持：18.9 ℃ 超出下限回差");

            RoomRequirement below = RoomRequirement.TemperatureBelow(20f);
            snapshot.AverageTemperatureK = 293.15f + 0.5f;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(dance, below, snapshot, false), "新判定：20.5 ℃ 不满足「低于 20」");
            TestRunner.Check(RoomEvaluator.IsSatisfied(dance, below, snapshot, true), "保持：20.5 ℃ 在「低于 20」回差内");
            snapshot.AverageTemperatureK = 293.15f + 1f;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(dance, below, snapshot, true), "保持：恰好 21 ℃ 仍按严格小于判定");
            snapshot.AverageTemperatureK = float.NaN;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(dance, below, snapshot, true), "保持：无温度采样仍不通过");

            snapshot.AverageTemperatureK = 293.15f;
            TestRunner.Check(
                RoomEvaluator.IsSatisfied(dance, below, snapshot, false) == RoomEvaluator.IsSatisfied(dance, below, snapshot),
                "三参数重载等同 retain=false");
        }

        private static void PrefabRequirements()
        {
            TestRunner.Section("设施与名单条件");

            RoomSnapshot snapshot = Snapshot(20);
            TestRunner.Check(!RoomEvaluator.IsSatisfied(RoomRequirement.RequirePrefab("Shower"), snapshot), "空房间不满足需要淋浴间");
            snapshot.PrefabIds.Add("Shower");
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.RequirePrefab("Shower"), snapshot), "有淋浴间满足需要淋浴间");

            snapshot.PrefabIds.Clear();
            snapshot.PrefabIds.Add("MedicalCot");
            TestRunner.Check(
                !RoomEvaluator.IsSatisfied(
                    RoomRequirement.RequireAllPrefabs("MedicalCot", "DoctorStation"),
                    snapshot),
                "只有分诊床不满足「全部医疗建筑」");

            snapshot.HasIndustrialMachinery = true;
            TestRunner.Check(!RoomEvaluator.IsSatisfied(RoomRequirement.NoIndustrialMachinery(), snapshot), "有工业机械不满足「无工业机械」");
            snapshot.HasIndustrialMachinery = false;
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.NoIndustrialMachinery(), snapshot), "无工业机械满足");

            snapshot.HasStorage = true;
            snapshot.HasLight = true;
            snapshot.HasToilet = true;
            snapshot.HasDiningTable = true;
            snapshot.HasDecoration = true;
            snapshot.HasOrnamentDisplayed = true;
            snapshot.HasMasterpiece = true;
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.RequireStorage(), snapshot), "存储设备");
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.RequireLight(), snapshot), "光照设备");
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.RequireToilet(), snapshot), "任意厕所");
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.RequireDiningTable(), snapshot), "任意餐桌");
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.RequireDecoration(), snapshot), "装饰建筑");
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.RequireOrnament(), snapshot), "藏品展示");
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.RequireMasterpiece(), snapshot), "杰出艺术品");
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.NoLifeBuilding(), snapshot), "没有生活建筑标志时满足「无生活建筑」");
        }

        private static void CategoryRequirements()
        {
            TestRunner.Section("高级娱乐／科学建筑互斥（排除专属设施本身）");

            RoomDefinition dance = RoomCatalogue.NewRooms[4];
            TestRunner.CheckEqual("MO_DanceHall", dance.Id, "舞厅定义位置稳定");

            RoomSnapshot snapshot = Snapshot(40);
            snapshot.PrefabIds.Add(RoomBuildingCatalogue.Phonobox);
            TestRunner.Check(
                RoomEvaluator.IsSatisfied(dance, RoomRequirement.NoOtherAdvancedRecreation(), snapshot),
                "只有点唱机器人时不算「其他高级娱乐建筑」");

            snapshot.PrefabIds.Add(RoomBuildingCatalogue.ArcadeMachine);
            TestRunner.Check(
                !RoomEvaluator.IsSatisfied(dance, RoomRequirement.NoOtherAdvancedRecreation(), snapshot),
                "再放游戏机台则不满足互斥");

            RoomDefinition mission = Find("MO_MissionControl");
            RoomSnapshot science = Snapshot(40);
            science.SciencePrefabIds.Add(RoomBuildingCatalogue.MissionControl);
            TestRunner.Check(
                RoomEvaluator.IsSatisfied(mission, RoomRequirement.NoOtherScienceBuilding(), science),
                "只有航天指挥站时不算「其他科学建筑」");
            science.SciencePrefabIds.Add("ResearchCenter");
            TestRunner.Check(
                !RoomEvaluator.IsSatisfied(mission, RoomRequirement.NoOtherScienceBuilding(), science),
                "存在其他科学建筑则不满足互斥");
        }

        private static void Arbitration()
        {
            TestRunner.Section("原版优先与明列覆盖");

            List<RoomDefinition> definitions = new List<RoomDefinition>(RoomCatalogue.NewRooms);
            RoomDefinition dance = Find("MO_DanceHall");

            RoomSnapshot snapshot = Snapshot(40);
            snapshot.PrefabIds.Add(RoomBuildingCatalogue.Phonobox);
            snapshot.PrefabIds.Add(RoomBuildingCatalogue.ParkSign);
            snapshot.HasDecoration = true;
            snapshot.AverageDecor = 120f;
            snapshot.AverageTemperatureK = 296.15f;

            TestRunner.Check(RoomEvaluator.IsSatisfied(dance, snapshot), "构造房间满足舞厅条件");
            RoomDefinition chosen = RoomEvaluator.Choose(definitions, snapshot, "RecRoom");
            TestRunner.CheckEqual("MO_DanceHall", chosen == null ? null : chosen.Id, "舞厅覆盖原版娱乐室");

            chosen = RoomEvaluator.Choose(definitions, snapshot, "Barracks");
            TestRunner.Check(chosen == null, "未列入覆盖关系的原版房间保持原版");

            chosen = RoomEvaluator.Choose(definitions, snapshot, "Neutral");
            TestRunner.CheckEqual("MO_DanceHall", chosen == null ? null : chosen.Id, "中立房间按满足条件升级");

            chosen = RoomEvaluator.Choose(definitions, snapshot, "MO_DanceHall");
            TestRunner.CheckEqual("MO_DanceHall", chosen == null ? null : chosen.Id, "已经生效的本 Mod 房间保持不变");

            snapshot.AverageDecor = 50f;
            chosen = RoomEvaluator.Choose(definitions, snapshot, "MO_DanceHall");
            TestRunner.Check(chosen == null, "条件不再满足时不由仲裁强留");
        }

        private static void RetainArbitration()
        {
            TestRunner.Section("保持回差：仲裁只对当前房间放宽");

            List<RoomDefinition> definitions = new List<RoomDefinition>(RoomCatalogue.NewRooms);
            RoomDefinition dance = Find("MO_DanceHall");

            // 舞厅要求平均装饰度 ≥ +100、温度 20–26 ℃。
            RoomSnapshot snapshot = Snapshot(40);
            snapshot.PrefabIds.Add(RoomBuildingCatalogue.Phonobox);
            snapshot.PrefabIds.Add(RoomBuildingCatalogue.ParkSign);
            snapshot.HasDecoration = true;
            snapshot.AverageDecor = 97f;
            snapshot.AverageTemperatureK = 296.15f;

            TestRunner.Check(!RoomEvaluator.IsSatisfied(dance, snapshot), "新判定：装饰度 +97 不满足舞厅");
            TestRunner.Check(RoomEvaluator.IsSatisfied(dance, snapshot, true), "保持：装饰度 +97 仍满足舞厅");
            TestRunner.Check(RoomEvaluator.Missing(dance, snapshot, true).Count == 0, "保持：没有缺失条件");
            TestRunner.CheckEqual(1, RoomEvaluator.Missing(dance, snapshot).Count, "新判定：只缺装饰度一项");

            RoomDefinition chosen = RoomEvaluator.Choose(definitions, snapshot, "MO_DanceHall");
            TestRunner.CheckEqual("MO_DanceHall", chosen == null ? null : chosen.Id, "当前是舞厅时按回差保持");

            chosen = RoomEvaluator.Choose(definitions, snapshot, "RecRoom");
            TestRunner.Check(chosen == null, "原版娱乐室不因回差升级为舞厅");

            chosen = RoomEvaluator.Choose(definitions, snapshot, "Neutral");
            TestRunner.Check(chosen == null, "中立房间不因回差升级为舞厅");

            snapshot.AverageDecor = 120f;
            snapshot.AverageTemperatureK = 299.15f + 0.5f;
            chosen = RoomEvaluator.Choose(definitions, snapshot, "MO_DanceHall");
            TestRunner.CheckEqual("MO_DanceHall", chosen == null ? null : chosen.Id, "温度 26.5 ℃ 时按回差保持舞厅");
            chosen = RoomEvaluator.Choose(definitions, snapshot, "RecRoom");
            TestRunner.Check(chosen == null, "温度 26.5 ℃ 时不新判为舞厅");

            snapshot.AverageTemperatureK = 296.15f;
            snapshot.AverageDecor = 94f;
            chosen = RoomEvaluator.Choose(definitions, snapshot, "MO_DanceHall");
            TestRunner.Check(chosen == null, "装饰度 +94 超出回差，不再保持");

            // 顶替关系：RoomTracker 放行的依据（CountsAsRoom 本身依赖游戏 Room 对象，见游戏验收）。
            TestRunner.Check(RoomEvaluator.Beats(Find(RoomCatalogue.MissionControlId), "Laboratory"), "任务控制中心顶替实验室");
            TestRunner.Check(RoomEvaluator.Beats(Find(RoomCatalogue.GeoTunerLabId), "Laboratory"), "地热调谐实验室顶替实验室");
            TestRunner.Check(RoomEvaluator.Beats(Find(RoomCatalogue.DataCenterId), "Laboratory"), "数据机房顶替实验室");
            TestRunner.Check(!RoomEvaluator.Beats(Find(RoomCatalogue.MissionControlId), "RecRoom"), "任务控制中心不顶替娱乐室");
            TestRunner.Check(!RoomEvaluator.Beats(dance, "Laboratory"), "舞厅不顶替实验室");
        }

        private static void CatalogueIntegrity()
        {
            TestRunner.Section("目录完整性");

            TestRunner.CheckEqual(16, RoomCatalogue.NewRoomCount, "新增房间 16 个");
            TestRunner.CheckEqual(3, RoomCatalogue.ModificationCount, "原版改造 3 项");

            HashSet<string> ids = new HashSet<string>();
            RoomDefinition[] definitions = RoomCatalogue.NewRooms;
            for (int i = 0; i < definitions.Length; i++)
            {
                RoomDefinition definition = definitions[i];
                TestRunner.Check(ids.Add(definition.Id), "房间 id 唯一：" + definition.Id);
                TestRunner.Check(definition.PrimaryPrefabIds != null && definition.PrimaryPrefabIds.Length > 0,
                    "房间有专属设施：" + definition.Id);
                TestRunner.Check(definition.Additional != null && definition.Additional.Length > 0,
                    "房间有附加条件：" + definition.Id);
                TestRunner.Check(!string.IsNullOrEmpty(definition.NameKey), "房间有名称键：" + definition.Id);
                TestRunner.Check(definition.Tier == RoomTier.Advanced || definition.Tier == RoomTier.Improved,
                    "新增房间不占用 T1：" + definition.Id);
            }

            TestRunner.Check(RoomCatalogue.ModificationFor("Private Bedroom") != null, "私人卧室有改造定义");
            TestRunner.Check(RoomCatalogue.ModificationFor("BanquetHall") != null, "宴会厅有改造定义");
            TestRunner.Check(RoomCatalogue.ModificationFor("Hospital") != null, "医院有改造定义");
            TestRunner.Check(RoomCatalogue.ModificationFor("Neutral") == null, "中立房间没有改造定义");
            TestRunner.Check(HasFallback("Private Bedroom", "Bedroom"), "私人卧室记录降级目标 Bedroom");
            TestRunner.Check(HasFallback("BanquetHall", "GreatHall"), "宴会厅记录降级目标 GreatHall");

            TestRunner.Check(RoomBuildingCatalogue.IsIndustrialBuilding("MetalRefinery"), "金属精炼器属工业建筑");
            TestRunner.Check(!RoomBuildingCatalogue.IsIndustrialBuilding("Generator"), "发电机不属工业建筑名单");
            TestRunner.Check(RoomBuildingCatalogue.IsAdvancedRecreation("Phonobox"), "点唱机器人属高级娱乐建筑");
            TestRunner.Check(RoomBuildingCatalogue.IsLifeBuilding("Bed"), "床属生活建筑");

            RoomDefinition medical = Find("MO_MedicalCenter");
            TestRunner.Check(HasRequirement(medical, RoomRequirementKind.RequireToilet),
                "医疗中心要求任意厕所");
            TestRunner.Check(HasRequirement(medical, RoomRequirementKind.RequireDiningTable),
                "医疗中心要求任意餐桌");

            Effects();
        }

        private static bool HasRequirement(RoomDefinition definition, RoomRequirementKind kind)
        {
            if (definition == null || definition.Additional == null)
            {
                return false;
            }

            for (int i = 0; i < definition.Additional.Length; i++)
            {
                if (definition.Additional[i].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Effects()
        {
            TestRunner.Section("已实装房间效果登记");

            string[] moraleRooms =
            {
                "MO_ArtificialPark", "MO_DanceHall", "MO_ArcadeHall", "MO_BeachParadise",
                "MO_Gallery", "MO_WindTunnel", "MO_Spa", "MO_TeaRoom"
            };
            for (int i = 0; i < moraleRooms.Length; i++)
            {
                RoomDefinition definition = Find(moraleRooms[i]);
                TestRunner.Check(definition != null && definition.EffectIds != null
                    && definition.EffectIds.Length == 1, moraleRooms[i] + " 登记了 1 项士气效果");
            }

            // 未接入效果的分支不得提前挂 Effect，避免界面显示尚未发放的加成。
            string[] noEffectRooms =
            {
                "MO_IndustrialArea", "MO_MissionControl",
                "MO_GeoTunerLab", "MO_Spaceport", "MO_CargoStation", "MO_DataCenter"
            };
            for (int i = 0; i < noEffectRooms.Length; i++)
            {
                RoomDefinition definition = Find(noEffectRooms[i]);
                TestRunner.Check(definition != null
                    && (definition.EffectIds == null || definition.EffectIds.Length == 0),
                    noEffectRooms[i] + " 未挂未实装效果");
            }

            TestRunner.CheckEqual(
                RoomEffectIds.ShowerRelief,
                Find("MO_ShowerRoom").EffectIds[0],
                "淋浴间登记压力缓解效果");

            RoomDefinition banquet = RoomCatalogue.ModificationFor("BanquetHall");
            TestRunner.Check(banquet != null && banquet.EffectIds != null
                && banquet.EffectIds.Length == 1
                && banquet.EffectIds[0] == RoomEffectIds.BanquetRelief,
                "宴会厅改造追加压力缓解效果");

            RoomDefinition hospitalMod = RoomCatalogue.ModificationFor("Hospital");
            TestRunner.Check(hospitalMod != null && hospitalMod.EffectIds != null
                && hospitalMod.EffectIds.Length == 1
                && hospitalMod.EffectIds[0] == RoomEffectIds.HospitalRecovery,
                "医院改造追加生命回复效果");
            TestRunner.Check(Find("MO_MedicalCenter").EffectIds[0] == RoomEffectIds.MedicalCenterRecovery,
                "医疗中心登记生命回复效果");

            HashSet<string> effectIds = new HashSet<string>();
            CheckEffectIds(RoomCatalogue.NewRooms, effectIds);
            CheckEffectIds(RoomCatalogue.Modifications, effectIds);

            // 每个挂出的效果都必须有数值定义；每个数值定义也必须真的被某个房间使用，
            // 避免出现「登记了但永远不会发放」或「房间引用了不存在的效果」。
            foreach (string id in effectIds)
            {
                TestRunner.Check(RoomEffectCatalogue.Get(id) != null, "效果有数值定义：" + id);
            }

            RoomEffectSpec[] specs = RoomEffectCatalogue.All;
            for (int i = 0; i < specs.Length; i++)
            {
                if (specs[i].AttachedToRoom)
                {
                    TestRunner.Check(effectIds.Contains(specs[i].Id), "效果定义被房间使用：" + specs[i].Id);
                }
                else
                {
                    TestRunner.Check(!effectIds.Contains(specs[i].Id),
                        "特殊触发效果不挂到房间类型：" + specs[i].Id);
                }
            }

            TestRunner.CheckEqual(
                RoomEffectIds.ArtificialParkMorale,
                Find("MO_ArtificialPark").EffectIds[0],
                "人造公园士气效果 id 稳定");

            EffectValues();
        }

        private static void EffectValues()
        {
            TestRunner.Section("效果数值与持续时间");

            RoomEffectSpec park = RoomEffectCatalogue.Get(RoomEffectIds.ArtificialParkMorale);
            TestRunner.Check(park != null && park.Morale == 2f && !park.HasStressReduction,
                "人造公园：士气 +2，不改压力");

            RoomEffectSpec shower = RoomEffectCatalogue.Get(RoomEffectIds.ShowerRelief);
            TestRunner.Check(shower != null && !shower.HasMorale && shower.StressReductionPercent == 10f,
                "淋浴间：压力缓解 −10%");

            RoomEffectSpec spa = RoomEffectCatalogue.Get(RoomEffectIds.SpaMorale);
            TestRunner.Check(spa != null && spa.Morale == 2f && spa.StressReductionPercent == 40f,
                "水疗室：士气 +2，压力缓解 −40%");

            RoomEffectSpec tea = RoomEffectCatalogue.Get(RoomEffectIds.TeaRoomMorale);
            TestRunner.Check(tea != null && tea.Morale == 2f && tea.StressReductionPercent == 30f,
                "茶歇室：士气 +2，压力缓解 −30%");

            RoomEffectSpec banquet = RoomEffectCatalogue.Get(RoomEffectIds.BanquetRelief);
            TestRunner.Check(banquet != null && !banquet.HasMorale && banquet.StressReductionPercent == 10f,
                "宴会厅：只追加压力 −10%，士气由原版效果负责");

            RoomEffectSpec gallery = RoomEffectCatalogue.Get(RoomEffectIds.GalleryMorale);
            TestRunner.Check(gallery != null && gallery.Morale == 2f, "展览馆：士气 +2");

            RoomEffectSpec hospital = RoomEffectCatalogue.Get(RoomEffectIds.HospitalRecovery);
            TestRunner.Check(hospital != null && hospital.HealthRecoveryPercent == 25f
                && !hospital.HasMorale && !hospital.HasStressReduction,
                "医院：生命回复 +25%，不附带士气或压力");

            RoomEffectSpec medicalCenter = RoomEffectCatalogue.Get(RoomEffectIds.MedicalCenterRecovery);
            TestRunner.Check(medicalCenter != null && medicalCenter.HealthRecoveryPercent == 50f,
                "医疗中心：生命回复 +50%");

            RoomEffectSpec caregiver = RoomEffectCatalogue.Get(RoomEffectIds.CaregiverBoost);
            TestRunner.Check(caregiver != null && caregiver.HealthRecoveryPercent == 25f
                && !caregiver.AttachedToRoom,
                "护理员特质：治疗期间再 +25%，不挂房间类型");

            RoomEffectSpec[] specs = RoomEffectCatalogue.All;
            for (int i = 0; i < specs.Length; i++)
            {
                // 治疗恢复效果按治疗状态存续，离开时移除；永久效果持续到被移除；二者持续时间均为 0。
                // 其余士气／压力效果使用 1 周期工作初值。
                float expectedDuration = specs[i].HasHealthRecovery || specs[i].Permanent
                    ? 0f
                    : RoomEffectCatalogue.DefaultDurationSeconds;
                TestRunner.Check(specs[i].DurationSeconds == expectedDuration,
                    specs[i].Id + " 持续时间符合效果类型");
                TestRunner.Check(specs[i].HasMorale || specs[i].HasStressReduction || specs[i].HasHealthRecovery
                    || specs[i].HasAttributeBonus,
                    specs[i].Id + " 至少有一项修正");
                TestRunner.Check(!string.IsNullOrEmpty(specs[i].StringToken),
                    specs[i].Id + " 有本地化键片段");
            }

            TestRunner.CheckEqual(600f, RoomEffectCatalogue.DefaultDurationSeconds, "工作初值持续 1 周期");

            RoomEffectSpec scar = RoomEffectCatalogue.Get(RoomEffectIds.RadiationScar);
            TestRunner.Check(scar != null && scar.Permanent && scar.IsBad && !scar.AttachedToRoom
                && scar.AttributeId == "Athletics" && scar.AttributeBonus == -6f
                && !scar.HasMorale && !scar.HasStressReduction && !scar.HasHealthRecovery,
                "辐射后遗症：永久负面效果，运动 −6，不挂房间类型");
        }

        private static void CheckEffectIds(RoomDefinition[] definitions, HashSet<string> effectIds)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                string[] ids = definitions[i].EffectIds;
                if (ids == null)
                {
                    continue;
                }

                for (int j = 0; j < ids.Length; j++)
                {
                    TestRunner.Check(ids[j].StartsWith("MO_Room", System.StringComparison.Ordinal),
                        "效果 id 使用 Mod 命名空间：" + ids[j]);
                    TestRunner.Check(effectIds.Add(ids[j]), "效果 id 唯一：" + ids[j]);
                }
            }
        }

        private static void MaterialDecor()
        {
            TestRunner.Section("材料装饰度配置（D07 工作口径）");

            TestRunner.CheckEqual(13, MaterialDecorCatalogue.ElementCount,
                "14 项设计材料中 13 项作为元素登记");
            TestRunner.Check(MaterialDecorCatalogue.Get("FeatherFabric") == null,
                "羽毛纤维是 prefab，不进入元素目录");

            HashSet<string> ids = new HashSet<string>();
            MaterialDecorSpec[] specs = MaterialDecorCatalogue.All;
            for (int i = 0; i < specs.Length; i++)
            {
                TestRunner.Check(!string.IsNullOrEmpty(specs[i].SimHash), "材料有 SimHashes 名");
                TestRunner.Check(ids.Add(specs[i].SimHash), "材料条目唯一：" + specs[i].SimHash);
            }

            TestRunner.CheckClose(25f, MaterialDecorCatalogue.Get("Ceramic").Percent, 0.0001f,
                "陶瓷改为 +25%");
            TestRunner.CheckClose(25f, MaterialDecorCatalogue.Get("Granite").Percent, 0.0001f,
                "花岗岩改为 +25%");
            TestRunner.CheckClose(-50f, MaterialDecorCatalogue.Get("DepletedUranium").Percent, 0.0001f,
                "贫铀 −50%");
            TestRunner.CheckClose(50f, MaterialDecorCatalogue.Get("PalmWood").Percent, 0.0001f,
                "粘胶木材 +50%");
            TestRunner.CheckClose(20f, MaterialDecorCatalogue.Get("TempConductorSolid").Percent, 0.0001f,
                "导热质 +20%");
            TestRunner.CheckClose(10f, MaterialDecorCatalogue.Get("FabricatedWood").Percent, 0.0001f,
                "交合板 +10%");
        }

        private static void Traits()
        {
            TestRunner.Section("娱乐偏好特质与专用效果");

            TestRunner.CheckEqual(6, RoomTraitCatalogue.Count, "六项娱乐偏好特质");
            HashSet<string> traitIds = new HashSet<string>();
            HashSet<string> effectIds = new HashSet<string>();
            RoomTraitSpec[] specs = RoomTraitCatalogue.All;
            for (int i = 0; i < specs.Length; i++)
            {
                TestRunner.Check(specs[i].TraitId.StartsWith("MO_", System.StringComparison.Ordinal),
                    "特质 id 使用 Mod 命名空间：" + specs[i].TraitId);
                TestRunner.Check(traitIds.Add(specs[i].TraitId), "特质 id 唯一：" + specs[i].TraitId);
                TestRunner.Check(effectIds.Add(specs[i].EffectId), "特质效果 id 唯一：" + specs[i].EffectId);
                TestRunner.Check(Find(specs[i].RoomId) != null, "特质指向已登记房间：" + specs[i].RoomId);
                TestRunner.Check(!string.IsNullOrEmpty(specs[i].StringToken), "特质有本地化键片段");
            }

            RoomEffectSpec superstar = RoomEffectCatalogue.Get(RoomEffectIds.TraitSuperstar);
            TestRunner.Check(superstar != null && superstar.Morale == 4f
                && superstar.StressReductionPercent == 20f
                && superstar.AttributeId == "Athletics"
                && superstar.AttributeBonus == 3f
                && !superstar.AttachedToRoom,
                "超级明星：士气 +4、压力 −20%、运动 +3，不挂房间类型");

            RoomEffectSpec rocket = RoomEffectCatalogue.Get(RoomEffectIds.TraitRocket);
            TestRunner.Check(rocket != null && rocket.AttributeId == "SpaceNavigation",
                "火箭专家：驾驶（SpaceNavigation）+3");

            for (int i = 0; i < specs.Length; i++)
            {
                RoomEffectSpec effect = RoomEffectCatalogue.Get(specs[i].EffectId);
                TestRunner.Check(effect != null, "特质效果有数值定义：" + specs[i].EffectId);
                if (effect == null)
                {
                    continue;
                }

                TestRunner.Check(effect.Morale == 4f && effect.StressReductionPercent == 20f,
                    specs[i].EffectId + " 士气 +4、压力缓解 −20%");
                TestRunner.Check(effect.HasAttributeBonus && effect.AttributeBonus == 3f,
                    specs[i].EffectId + " 有 +3 属性");
                TestRunner.Check(effect.DurationSeconds == RoomEffectCatalogue.DefaultDurationSeconds,
                    specs[i].EffectId + " 使用 1 周期工作初值");
            }
        }

        private static void Production()
        {
            TestRunner.Section("房间生产加成（工作口径）");

            TestRunner.CheckClose(1.2f, RoomProduction.IndustrialAreaMultiplier, 0.0001f, "工业区 +20%");
            TestRunner.CheckClose(1.2f, RoomProduction.DataCenterMultiplier, 0.0001f, "数据机房 +20%");

            TestRunner.CheckClose(
                1.2f,
                RoomProduction.Multiplier(RoomCatalogue.IndustrialAreaId, "MetalRefinery"),
                0.0001f,
                "工业区内工业建筑获得加成");
            TestRunner.CheckClose(
                1f,
                RoomProduction.Multiplier(RoomCatalogue.IndustrialAreaId, "Generator"),
                0.0001f,
                "工业区不作用于发电建筑");
            TestRunner.CheckClose(
                1f,
                RoomProduction.Multiplier("MO_DanceHall", "MetalRefinery"),
                0.0001f,
                "非工业区不获得生产加成");
            TestRunner.CheckClose(
                1.2f,
                RoomProduction.Multiplier(RoomCatalogue.DataCenterId, RoomBuildingCatalogue.DataMiner),
                0.0001f,
                "数据机房作用于数据挖掘中心");
            TestRunner.CheckClose(
                1f,
                RoomProduction.Multiplier(RoomCatalogue.DataCenterId, "MetalRefinery"),
                0.0001f,
                "数据机房不作用于其它工业建筑");
            TestRunner.CheckClose(
                1f,
                RoomProduction.Multiplier(null, "MetalRefinery"),
                0.0001f,
                "无房间类型时无加成");
            TestRunner.CheckClose(
                1f,
                RoomProduction.Multiplier(RoomCatalogue.IndustrialAreaId, null),
                0.0001f,
                "无 prefab 时无加成");

            TestRunner.CheckClose(1.2f, RoomProduction.MissionControlMultiplier, 0.0001f, "任务控制中心 +20%");
            TestRunner.CheckClose(1.2f, RoomProduction.GeoTunerLabMultiplier, 0.0001f, "地热调谐实验室 +20%");
            TestRunner.CheckClose(
                1.2f,
                RoomProduction.Multiplier(RoomCatalogue.MissionControlId, RoomBuildingCatalogue.MissionControl),
                0.0001f,
                "任务控制中心作用于航天指挥站");
            TestRunner.CheckClose(
                1.2f,
                RoomProduction.Multiplier(RoomCatalogue.MissionControlId, RoomBuildingCatalogue.MissionControlCluster),
                0.0001f,
                "任务控制中心作用于星际航天指挥站");
            TestRunner.CheckClose(
                1f,
                RoomProduction.Multiplier(RoomCatalogue.MissionControlId, RoomBuildingCatalogue.GeoTuner),
                0.0001f,
                "任务控制中心不作用于地质调谐仪");
            TestRunner.CheckClose(
                1f,
                RoomProduction.Multiplier(RoomCatalogue.MissionControlId, "MetalRefinery"),
                0.0001f,
                "任务控制中心不作用于工业建筑");
            TestRunner.CheckClose(
                1.2f,
                RoomProduction.Multiplier(RoomCatalogue.GeoTunerLabId, RoomBuildingCatalogue.GeoTuner),
                0.0001f,
                "地热调谐实验室作用于地质调谐仪");
            TestRunner.CheckClose(
                1f,
                RoomProduction.Multiplier(RoomCatalogue.GeoTunerLabId, RoomBuildingCatalogue.MissionControl),
                0.0001f,
                "地热调谐实验室不作用于航天指挥站");
            TestRunner.CheckClose(
                1f,
                RoomProduction.Multiplier(RoomCatalogue.GeoTunerLabId, "MetalRefinery"),
                0.0001f,
                "地热调谐实验室不作用于工业建筑");
            TestRunner.CheckClose(
                1f,
                RoomProduction.Multiplier(RoomCatalogue.DataCenterId, RoomBuildingCatalogue.GeoTuner),
                0.0001f,
                "数据机房不作用于地质调谐仪");

            TestRunner.CheckClose(1.2f, RoomProduction.MultiplierForRoom(RoomCatalogue.MissionControlId), 0.0001f,
                "任务控制中心展示倍率 1.2");
            TestRunner.CheckClose(1.2f, RoomProduction.MultiplierForRoom(RoomCatalogue.GeoTunerLabId), 0.0001f,
                "地热调谐实验室展示倍率 1.2");
            TestRunner.CheckClose(1f, RoomProduction.MultiplierForRoom("MO_DanceHall"), 0.0001f,
                "非生产房间展示倍率 1");
        }

        private static bool HasFallback(string existingRoomId, string fallbackRoomId)
        {
            RoomDefinition definition = RoomCatalogue.ModificationFor(existingRoomId);
            if (definition == null || definition.FallbackRoomIds == null)
            {
                return false;
            }

            for (int i = 0; i < definition.FallbackRoomIds.Length; i++)
            {
                if (definition.FallbackRoomIds[i] == fallbackRoomId)
                {
                    return true;
                }
            }

            return false;
        }

        private static RoomDefinition Find(string id)
        {
            RoomDefinition[] definitions = RoomCatalogue.NewRooms;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].Id == id)
                {
                    return definitions[i];
                }
            }

            return null;
        }
    }
}
