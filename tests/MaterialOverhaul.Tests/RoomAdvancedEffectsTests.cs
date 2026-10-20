using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 科研、航天、货运、医疗中心与宴会厅房间效果的纯计算（V3.0.0 §4.9）。
    /// 只验证参数与换算规则；补丁装配、实际结算与界面需游戏内验收（G21–G28）。
    /// </summary>
    public static class RoomAdvancedEffectsTests
    {
        public static void Run()
        {
            Parameters();
            ScienceScaling();
            MissionControl();
            GeoTunerLab();
            Spaceport();
            CargoStation();
            MedicalCenter();
        }

        private static void Parameters()
        {
            TestRunner.Section("剩余房间效果参数（工作初值）");

            TestRunner.CheckClose(20f, RoomAdvancedEffects.ScienceThreshold, 0.0001f, "科学阈值 20");
            TestRunner.CheckClose(2f, RoomAdvancedEffects.ScienceBonusFactor, 0.0001f, "科学翻倍系数 2");
            TestRunner.CheckClose(0.2f, RoomAdvancedEffects.MissionControlSpeedBonus, 0.0001f, "任务控制室新增速度 20%");
            TestRunner.CheckEqual(2, RoomAdvancedEffects.MissionControlRangeBonus, "任务控制室新增范围 2 格");
            TestRunner.CheckEqual(2, RoomAdvancedEffects.VanillaMissionControlRange, "原版指挥站范围 2 格");
            TestRunner.CheckClose(0.1f, RoomAdvancedEffects.GeoTunerLabMassBonus, 0.0001f, "地质研究室新增产量 10%");
            TestRunner.CheckEqual(1, RoomAdvancedEffects.GeoTunerLabExtraTuners, "地质研究室新增 1 台调谐仪");
            TestRunner.CheckEqual(5, RoomAdvancedEffects.VanillaGeotunerLimit, "原版每喷泉 5 台");
            TestRunner.CheckClose(0.1f, RoomAdvancedEffects.SpaceportSpeedBonus, 0.0001f, "航天发射场速度 +10%");
            TestRunner.CheckClose(0.9f, RoomAdvancedEffects.SpaceportFuelFactor, 0.0001f, "航天发射场燃料 ×0.9");
            TestRunner.CheckClose(1.2f, RoomAdvancedEffects.CargoPayloadSpeedFactor, 0.0001f, "载荷速度 ×1.2");
            TestRunner.CheckEqual(2, RoomAdvancedEffects.CargoExtraLaunches, "每轮新增 2 次发射");
            TestRunner.CheckEqual(6, RoomAdvancedEffects.VanillaLaunchesPerCooldown, "原版每轮 6 次");
            TestRunner.CheckClose(1.5f, RoomAdvancedEffects.CargoCooldownSpeedFactor, 0.0001f, "冷却 1.5 倍速");
            TestRunner.CheckClose(6f, RoomAdvancedEffects.BanquetMorale, 0.0001f, "宴会厅士气 +6");
            TestRunner.CheckEqual("RoomBanquetHall", RoomAdvancedEffects.BanquetEffectId, "宴会厅原版效果 id");
            TestRunner.CheckClose(900f, RoomAdvancedEffects.RadiationDeadlyDose, 0.0001f, "致命剂量 900");
        }

        private static void ScienceScaling()
        {
            TestRunner.Section("操作者科学大于 20 时新增量翻倍");

            TestRunner.Check(!RoomAdvancedEffects.ScienceDoubles(20f), "科学恰好 20 不翻倍（严格大于）");
            TestRunner.Check(RoomAdvancedEffects.ScienceDoubles(20.01f), "科学 20.01 翻倍");
            TestRunner.Check(!RoomAdvancedEffects.ScienceDoubles(float.NaN), "无法读取科学值时不翻倍");
            TestRunner.CheckClose(0.2f, RoomAdvancedEffects.ScaleBonus(0.2f, 10f), 0.0001f, "未翻倍保持原新增量");
            TestRunner.CheckClose(0.4f, RoomAdvancedEffects.ScaleBonus(0.2f, 25f), 0.0001f, "翻倍后新增量 ×2");
            TestRunner.CheckEqual(2, RoomAdvancedEffects.ScaleBonus(2, 20f), "整数新增量在 20 时不翻倍");
            TestRunner.CheckEqual(4, RoomAdvancedEffects.ScaleBonus(2, 21f), "整数新增量翻倍");
        }

        private static void MissionControl()
        {
            TestRunner.Section("任务控制室：火箭速度与范围新增量");

            TestRunner.CheckClose(0.2f, RoomAdvancedEffects.MissionControlSpeed(RoomCatalogue.MissionControlId, 10f), 0.0001f,
                "房间内速度新增 20%");
            TestRunner.CheckClose(0.4f, RoomAdvancedEffects.MissionControlSpeed(RoomCatalogue.MissionControlId, 21f), 0.0001f,
                "科学大于 20 时速度新增 40%");
            TestRunner.CheckClose(0f, RoomAdvancedEffects.MissionControlSpeed("Laboratory", 30f), 0.0001f,
                "原版实验室内无新增速度");
            TestRunner.CheckClose(0f, RoomAdvancedEffects.MissionControlSpeed(null, 30f), 0.0001f, "不在房间内无新增速度");
            TestRunner.CheckEqual(4, RoomAdvancedEffects.MissionControlRange(RoomCatalogue.MissionControlId, 10f), "房间内范围 4 格");
            TestRunner.CheckEqual(6, RoomAdvancedEffects.MissionControlRange(RoomCatalogue.MissionControlId, 21f),
                "科学大于 20 时范围 6 格");
            TestRunner.CheckEqual(2, RoomAdvancedEffects.MissionControlRange(RoomCatalogue.GeoTunerLabId, 21f),
                "其他房间保持原版 2 格");
            TestRunner.CheckEqual(4, RoomAdvancedEffects.MissionControlRange(RoomCatalogue.MissionControlId, float.NaN),
                "未记录操作者科学值时按未翻倍计 4 格");
        }

        private static void GeoTunerLab()
        {
            TestRunner.Section("地质研究室：调谐产量与调谐仪上限");

            TestRunner.CheckClose(0.1f, RoomAdvancedEffects.GeoTunerMassBonus(RoomCatalogue.GeoTunerLabId, 10f), 0.0001f,
                "房间内喷发量新增 10%");
            TestRunner.CheckClose(0.2f, RoomAdvancedEffects.GeoTunerMassBonus(RoomCatalogue.GeoTunerLabId, 21f), 0.0001f,
                "科学大于 20 时新增 20%");
            TestRunner.CheckClose(0f, RoomAdvancedEffects.GeoTunerMassBonus("Laboratory", 21f), 0.0001f, "实验室内无新增产量");
            TestRunner.CheckEqual(6, RoomAdvancedEffects.GeotunerLimit(RoomCatalogue.GeoTunerLabId, 10f), "房间内上限 6 台");
            TestRunner.CheckEqual(7, RoomAdvancedEffects.GeotunerLimit(RoomCatalogue.GeoTunerLabId, 21f), "科学大于 20 时上限 7 台");
            TestRunner.CheckEqual(5, RoomAdvancedEffects.GeotunerLimit(null, 21f), "房间外上限 5 台");
            TestRunner.Check(RoomAdvancedEffects.CanAddGeotuner(5, 6), "已有 5 台、上限 6 时可再加一台");
            TestRunner.Check(!RoomAdvancedEffects.CanAddGeotuner(6, 6), "已有 6 台、上限 6 时拒绝");
            TestRunner.Check(!RoomAdvancedEffects.CanAddGeotuner(5, 5), "原版：已有 5 台时拒绝");
            TestRunner.Check(RoomAdvancedEffects.CanAddGeotuner(4, 5), "原版：已有 4 台时可加");
        }

        private static void Spaceport()
        {
            TestRunner.Section("航天发射场：燃料换算");

            TestRunner.Check(RoomAdvancedEffects.IsSpaceport(RoomCatalogue.SpaceportId), "识别航天发射场");
            TestRunner.Check(!RoomAdvancedEffects.IsSpaceport(RoomCatalogue.CargoStationId), "货运站不是发射场");

            // 原版 BurnFromTank：消耗 = 尝试航程 × 每格燃料；返回 = 实际消耗 ÷ 每格燃料。
            const float perDistance = 0.5f;
            const float attempt = 600f;
            float scaledAttempt = RoomAdvancedEffects.FuelAttempt(attempt, RoomAdvancedEffects.SpaceportFuelFactor);
            float burned = scaledAttempt * perDistance;
            TestRunner.CheckClose(attempt * perDistance * 0.9f, burned, 0.001f, "每格燃料按 90% 扣除");
            float travelled = RoomAdvancedEffects.FuelTravelled(burned / perDistance, RoomAdvancedEffects.SpaceportFuelFactor);
            TestRunner.CheckClose(attempt, travelled, 0.001f, "燃料充足时仍完成整格航程");

            // 库存只够原版一半时：截断后折算的航程同样按 90% 燃料计算。
            float available = attempt * perDistance * 0.45f;
            float limitedBurn = System.Math.Min(available, burned);
            float limitedTravel = RoomAdvancedEffects.FuelTravelled(limitedBurn / perDistance, RoomAdvancedEffects.SpaceportFuelFactor);
            TestRunner.CheckClose(available / (perDistance * 0.9f), limitedTravel, 0.001f, "燃料不足时按 90% 燃料折算航程");
            TestRunner.CheckClose(attempt, RoomAdvancedEffects.FuelAttempt(attempt, 1f), 0.0001f, "系数 1 时不改变航程");
            TestRunner.CheckClose(attempt, RoomAdvancedEffects.FuelTravelled(attempt, 0f), 0.0001f, "非法系数时保持原值");
        }

        private static void CargoStation()
        {
            TestRunner.Section("星际货运站：连发计数与冷却");

            TestRunner.Check(RoomAdvancedEffects.IsCargoStation(RoomCatalogue.CargoStationId), "识别星际货运站");

            // 模拟一轮：原版每次发射后计数 +1，达到 6 进入冷却；房间规则在发射前计数为 5 时回拨，最多 2 次。
            int count = 0;
            int used = 0;
            int shots = 0;
            while (shots < 20)
            {
                int adjusted = RoomAdvancedEffects.CargoAdjustedCount(count, used, out bool consumed);
                if (consumed)
                {
                    used++;
                }

                count = adjusted + 1;
                shots++;
                if (count >= RoomAdvancedEffects.VanillaLaunchesPerCooldown)
                {
                    break;
                }
            }

            TestRunner.CheckEqual(8, shots, "每轮冷却前共发射 8 次");
            TestRunner.CheckEqual(2, used, "本轮回拨恰好 2 次");

            int untouched = RoomAdvancedEffects.CargoAdjustedCount(3, 0, out bool early);
            TestRunner.Check(!early && untouched == 3, "未到第 6 次时不回拨");
            int exhausted = RoomAdvancedEffects.CargoAdjustedCount(5, 2, out bool afterLimit);
            TestRunner.Check(!afterLimit && exhausted == 5, "回拨次数用完后照常进入冷却");

            TestRunner.CheckClose(0.1f, RoomAdvancedEffects.CargoExtraCooldown(0.2f), 0.0001f, "每 0.2 秒额外递减 0.1 秒");
            TestRunner.CheckClose(0f, RoomAdvancedEffects.CargoExtraCooldown(-1f), 0.0001f, "非正 dt 不递减");
            float seconds = 30f / RoomAdvancedEffects.CargoCooldownSpeedFactor;
            TestRunner.CheckClose(20f, seconds, 0.0001f, "30 秒冷却缩短为 20 秒");
        }

        private static void MedicalCenter()
        {
            TestRunner.Section("医疗中心：辐射拯救者阈值");

            TestRunner.Check(RoomAdvancedEffects.IsMedicalCenter(RoomCatalogue.MedicalCenterId), "识别医疗中心");
            TestRunner.Check(!RoomAdvancedEffects.IsMedicalCenter("Hospital"), "原版医院不提供辐射拯救者");
            TestRunner.Check(!RoomAdvancedEffects.IsDeadlyRadiation(899.9f, 1f), "899.9 rad 不处理");
            TestRunner.Check(RoomAdvancedEffects.IsDeadlyRadiation(900f, 1f), "900 rad 达到致命阈值（含）");
            TestRunner.Check(!RoomAdvancedEffects.IsDeadlyRadiation(1000f, 2f), "难度系数 2 时 1000 rad 未达 1800");
            TestRunner.Check(RoomAdvancedEffects.IsDeadlyRadiation(1800f, 2f), "难度系数 2 时 1800 rad 致命");
            TestRunner.Check(RoomAdvancedEffects.IsDeadlyRadiation(900f, 0f), "异常难度系数按 1 处理");
            TestRunner.Check(!RoomAdvancedEffects.IsDeadlyRadiation(float.NaN, 1f), "缺失剂量不处理");
        }
    }
}
