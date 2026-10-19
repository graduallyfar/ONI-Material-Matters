using System;
using Klei.AI;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 按 <see cref="MaterialDecorCatalogue"/> 写入元素装饰度乘算修饰。
    ///
    /// 在 <c>LegacyModMain.ConfigElements</c> 之后调用：原版已先建立元素对象及其装饰度修饰，
    /// 这里只替换同名乘算项，不重复追加；重复调用是幂等的。非元素项目（羽毛纤维）跳过并记录。
    /// </summary>
    public static class MaterialDecorRegistration
    {
        private const string DecorAttribute = "Decor";

        public static void Apply()
        {
            int applied = 0;
            MaterialDecorSpec[] specs = MaterialDecorCatalogue.All;
            for (int i = 0; i < specs.Length; i++)
            {
                MaterialDecorSpec spec = specs[i];
                SimHashes hash;
                if (!Enum.TryParse(spec.SimHash, out hash))
                {
                    ModLog.WarnOnce("room|decor|hash|" + spec.SimHash,
                        "装饰度配置：找不到 SimHashes." + spec.SimHash + "，本次跳过。");
                    continue;
                }

                Element element = ElementLoader.FindElementByHash(hash);
                if (element == null)
                {
                    ModLog.WarnOnce("room|decor|element|" + spec.SimHash,
                        "装饰度配置：找不到元素 " + spec.SimHash + "，本次跳过。");
                    continue;
                }

                // 先移除原版或上次运行留下的 Decor 乘算项，保证最终倍率等于设计表。
                element.attributeModifiers.RemoveAll(
                    modifier => modifier.AttributeId == DecorAttribute && modifier.IsMultiplier);
                element.attributeModifiers.Add(new AttributeModifier(
                    DecorAttribute,
                    spec.Percent / 100f,
                    element.name,
                    is_multiplier: true));
                applied++;
            }

            ModLog.Info("材料装饰度：已按设计表写入 " + applied + " 项元素装饰乘率"
                + "（工作口径：替换为表中百分比，D07 未确认；羽毛纤维为 prefab，未处理）。");
        }
    }
}
