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
            PrefabRequirements();
            CategoryRequirements();
            Arbitration();
            CatalogueIntegrity();
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
            snapshot.HasDecoration = true;
            snapshot.HasOrnamentDisplayed = true;
            snapshot.HasMasterpiece = true;
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.RequireStorage(), snapshot), "存储设备");
            TestRunner.Check(RoomEvaluator.IsSatisfied(RoomRequirement.RequireLight(), snapshot), "光照设备");
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

        private static void CatalogueIntegrity()
        {
            TestRunner.Section("目录完整性");

            TestRunner.CheckEqual(16, RoomCatalogue.NewRoomCount, "新增房间 16 个");
            TestRunner.CheckEqual(2, RoomCatalogue.ModificationCount, "原版改造 2 项");

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
            TestRunner.Check(RoomCatalogue.ModificationFor("Neutral") == null, "中立房间没有改造定义");

            TestRunner.Check(RoomBuildingCatalogue.IsIndustrialBuilding("MetalRefinery"), "金属精炼器属工业建筑");
            TestRunner.Check(!RoomBuildingCatalogue.IsIndustrialBuilding("Generator"), "发电机不属工业建筑名单");
            TestRunner.Check(RoomBuildingCatalogue.IsAdvancedRecreation("Phonobox"), "点唱机器人属高级娱乐建筑");
            TestRunner.Check(RoomBuildingCatalogue.IsLifeBuilding("Bed"), "床属生活建筑");

            Effects();
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
                "MO_IndustrialArea", "MO_MedicalCenter", "MO_MissionControl",
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

            HashSet<string> effectIds = new HashSet<string>();
            CheckEffectIds(RoomCatalogue.NewRooms, effectIds);
            CheckEffectIds(RoomCatalogue.Modifications, effectIds);

            TestRunner.CheckEqual(
                RoomEffectIds.ArtificialParkMorale,
                Find("MO_ArtificialPark").EffectIds[0],
                "人造公园士气效果 id 稳定");
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
