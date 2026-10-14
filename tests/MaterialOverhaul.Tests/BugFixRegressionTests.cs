using System;
using System.Reflection;
using MaterialOverhaul.Game;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Patches;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    public static class BugFixRegressionTests
    {
        public static void Run()
        {
            QueueClicks();
            HeatingSafety();
        }

        private static MaterialOverhaulFab Setup(out ComplexFabricator machine, out ComplexRecipe recipe)
        {
            recipe = new ComplexRecipe { id = "Aluminum" };
            machine = new ComplexFabricator { RecipeList = new[] { recipe } };
            machine.Counts[recipe.id] = 0;
            return new MaterialOverhaulFab(machine, r => ProductionDecision.Passthrough);
        }

        private static void QueueClicks()
        {
            TestRunner.Section("实际数量补丁：点击、嵌套刷新与订单身份");
            MaterialOverhaulFab mod = Setup(out ComplexFabricator machine, out ComplexRecipe a);
            ComplexRecipe b = new ComplexRecipe { id = "Copper" };
            machine.RecipeList = new[] { a, b };
            machine.Counts[b.id] = 0;
            machine.RefreshCallback = mod.RefreshNextOrder;
            machine.EditQueue("IncrementRecipeQueueCount", a);
            TestRunner.CheckEqual(1, machine.Counts[a.id], "刷新先导入时首次＋仍为 1");
            TestRunner.CheckEqual(1, mod.Orders.TotalFor(a.id).Batches, "首次＋仅记一批");
            int first = mod.Orders.Orders[0].Id;
            machine.EditQueue("IncrementRecipeQueueCount", b);
            TestRunner.CheckEqual(1, machine.Counts[b.id], "已有其它配方时新配方＋仍为 1（原缺陷触发条件）");
            machine.EditQueue("IncrementRecipeQueueCount", a);
            TestRunner.CheckEqual(2, machine.Counts[a.id], "连续＋仅增加 1");
            TestRunner.CheckEqual(3, mod.Orders.Count, "A/B/A 保留三笔独立订单");
            TestRunner.CheckEqual(b.id, mod.Orders.Orders[1].RecipeId, "保持点击顺序");
            TestRunner.CheckEqual(first, mod.Orders.Orders[0].Id, "不重建已有订单身份");
            machine.EditQueue("DecrementRecipeQueueCount", a);
            TestRunner.CheckEqual(1, machine.Counts[a.id], "−仅减少 1");
            TestRunner.CheckEqual(first, mod.Orders.Orders[0].Id, "−从最后一笔回退");
            machine.EditQueue("SetRecipeQueueCount", a, 7);
            TestRunner.CheckEqual(7, machine.Counts[a.id], "输入框准确设置数量");
            machine.EditQueue("SetRecipeQueueCount", a, 99);
            machine.EditQueue("IncrementRecipeQueueCount", a);
            TestRunner.CheckEqual(-1, machine.Counts[a.id], "99 后＋转无限");
            machine.EditQueue("DecrementRecipeQueueCount", a, respectInfinite: true);
            TestRunner.CheckEqual(-1, machine.Counts[a.id], "尊重无限时−不改变订单");
            machine.EditQueue("DecrementRecipeQueueCount", a);
            TestRunner.CheckEqual(99, machine.Counts[a.id], "面板无限−回到 99");
            machine.EditQueue("SetRecipeQueueCount", a, -1);
            machine.EditQueue("IncrementRecipeQueueCount", a);
            TestRunner.CheckEqual(0, machine.Counts[a.id], "无限＋归零");
            machine.EditQueue("DecrementRecipeQueueCount", a);
            TestRunner.CheckEqual(0, machine.Counts[a.id], "0 后−不凭空创建无限订单");
            TestRunner.Check(!mod.Orders.ContainsRecipe(a.id), "0 后−不残留迁入的临时无限订单");
            machine.EditQueue("IncrementRecipeQueueCount", a);
            TestRunner.CheckEqual(1, machine.Counts[a.id], "0 后−再＋仍准确得到 1");

            mod = Setup(out machine, out a);
            machine.Counts[a.id] = 4; // 旧存档数量，在点击前导入。
            machine.EditQueue("IncrementRecipeQueueCount", a);
            TestRunner.CheckEqual(5, machine.Counts[a.id], "旧数量 4 加一次准确得到 5");
            TestRunner.CheckEqual(2, mod.Orders.Count, "迁移旧数量与新点击为独立两笔");
            machine.Mod = null;
            machine.EditQueue("IncrementRecipeQueueCount", a);
            TestRunner.CheckEqual(6, machine.Counts[a.id], "没有 Mod 组件时保持原版");
        }

        private static void Damage(MaterialOverhaulFab mod, int loss, bool mechanical = false)
        {
            Type patch = typeof(Patch_BuildingHP_OnDoBuildingDamage);
            object[] args = { mod.HP, 0 };
            patch.GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
            if (mechanical) mod.MarkMechanicalDamage();
            mod.HP.HitPoints = Math.Max(0, mod.HP.HitPoints - loss);
            patch.GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mod.HP, args[1] });
        }

        private static void HeatingSafety()
        {
            TestRunner.Section("实际过热伤害补丁与加热安全源码：受损、读档、维修");
            foreach (HeatingIntent intent in new[] { HeatingIntent.Preheat, HeatingIntent.TopUp, HeatingIntent.Sustained })
            {
                PowerDemand guarded = PowerDemand.Evaluate(new PowerDemandContext
                {
                    HasLockedBatch = true, PowerAvailable = true, HasWorker = true,
                    TemperatureAllowsProduction = true, AutomationAllowsProduction = true,
                    OverheatDamaged = true, CommandedHeating = intent
                });
                TestRunner.CheckClose(0f, guarded.RequestedHeatingWatts, 0.001f, "耗电层拒绝受损加热意图：" + intent);
                TestRunner.CheckEqual(HeatingIntent.None, guarded.AppliedHeating, "耗电层不给受损注热许可：" + intent);
                TestRunner.Check(guarded.ProductionRunning, "耗电层保留生产许可：" + intent);
            }
            MaterialOverhaulFab mod = Setup(out ComplexFabricator machine, out ComplexRecipe a);
            machine.CurrentWorkingOrder = a;
            mod.Forced = true;
            mod.IsAboveOverheatThreshold = true;
            mod.RefreshSafety();
            TestRunner.Check(mod.SafetyPower.UsedHeatingWatts > 0, "越过阈值尚未扣血，仍可强制加热");
            Damage(mod, 0);
            TestRunner.Check(!mod.HasOverheatDamage, "未实际扣血的事件不触发保护");
            Damage(mod, 1, mechanical: true);
            TestRunner.Check(!mod.HasOverheatDamage, "高温下机械磨损不误记过热");
            Damage(mod, 1);
            TestRunner.Check(mod.HasOverheatDamage && !mod.HP.IsBroken, "轻微过热受损即锁存，不等血量耗尽");
            TestRunner.CheckClose(0f, mod.SafetyPower.RequestedHeatingWatts, 0.001f, "伤害事件内立即撤掉加热需求");
            TestRunner.CheckClose(0f, mod.SafetyPower.UsedHeatingWatts, 0.001f, "伤害事件内立即停止加热耗电");
            TestRunner.CheckEqual(HeatingIntent.None, mod.SafetyPower.AppliedHeating, "不再注热");
            TestRunner.Check(mod.SafetyPower.ProductionRunning, "尚未损坏停机，原有强制生产许可保留");
            TestRunner.CheckEqual(DamageCause.MechanicalAndOverheat, DamageCauseEvaluator.Evaluate(mod.SavedDamage), "强制过热仍显示双重原因");
            mod.Forced = false;
            mod.IsAboveOverheatThreshold = false;
            foreach (HeatingIntent intent in new[] { HeatingIntent.Preheat, HeatingIntent.TopUp, HeatingIntent.Sustained })
            {
                mod.SafetyIntent = intent;
                mod.Forced = intent == HeatingIntent.Sustained;
                mod.RefreshSafety();
                TestRunner.CheckEqual(HeatingIntent.None, mod.SafetyPower.AppliedHeating, "降温与切开关不解除保护：" + intent);
            }
            DamageCauseState saved = mod.SavedDamage;
            MaterialOverhaulFab loaded = Setup(out ComplexFabricator loadedMachine, out ComplexRecipe loadedRecipe);
            loadedMachine.CurrentWorkingOrder = loadedRecipe;
            loaded.HP.HitPoints = 98;
            loaded.Forced = true;
            loaded.RestoreDamage(saved);
            loaded.RefreshSafety();
            TestRunner.CheckEqual(HeatingIntent.None, loaded.SafetyPower.AppliedHeating, "读入现有损坏字段后保持停热");
            loaded.HP.HitPoints = 99;
            loaded.RefreshSafety();
            TestRunner.Check(loaded.HasOverheatDamage, "部分维修不解除");
            loaded.HP.HitPoints = 100;
            loaded.RefreshSafety();
            TestRunner.Check(!loaded.HasOverheatDamage && loaded.SavedDamage.IsClean, "完全维修解除并保存");
            TestRunner.CheckEqual(HeatingIntent.Sustained, loaded.SafetyPower.AppliedHeating, "维修完成按当前开关恢复加热");
            loaded.HP.HitPoints = 0;
            loaded.RefreshSafety();
            TestRunner.CheckClose(0f, loaded.SafetyPower.RequestedTotalWatts, 0.001f, "生命值耗尽仍停产停热");

            mod = Setup(out machine, out a);
            machine.CurrentWorkingOrder = a;
            mod.HP.HitPoints = 99;
            mod.RestoreDamage(new DamageCauseState { MechanicalLatched = true });
            mod.SafetyIntent = HeatingIntent.TopUp;
            mod.RefreshSafety();
            TestRunner.CheckEqual(HeatingIntent.TopUp, mod.SafetyPower.AppliedHeating, "纯机械轻微受损不触发过热保护");
            mod.RestoreDamage(new DamageCauseState { ForcedOverheatLatched = true });
            mod.RefreshSafety();
            TestRunner.CheckEqual(HeatingIntent.None, mod.SafetyPower.AppliedHeating, "旧档仅有强制过热标记同样停热");
            TestRunner.Check(ReferenceEquals(a, machine.CurrentWorkingOrder), "安全保护保留当前批次");
            TestRunner.CheckClose(0.42f, machine.OrderProgress, 0.001f, "安全保护保留进度");
            TestRunner.CheckClose(100f, machine.BatchMaterialMass, 0.001f, "安全保护保留已投料");
        }
    }
}
