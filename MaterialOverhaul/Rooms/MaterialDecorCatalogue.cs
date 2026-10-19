using System;

namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 材料装饰度配置（房间系统设计 §4.2.1）。
    ///
    /// 工作口径（D07 未确认）：把元素原有的 <c>Decor</c> 乘算修饰替换为表中百分比，
    /// 即陶瓷／花岗岩从原版 20% 改为 25%，新增材料追加对应乘数；负值按乘数原样登记。
    /// 表中「羽毛纤维」对应工业产品 prefab <c>FeatherFabric</c>，不是元素，暂不进入本目录；
    /// 预制件是否参与装饰度需 D07 确认。
    /// </summary>
    public static class MaterialDecorCatalogue
    {
        private static readonly MaterialDecorSpec[] AllValue =
        {
            new MaterialDecorSpec { SimHash = "Ceramic", Percent = 25f },
            new MaterialDecorSpec { SimHash = "Granite", Percent = 25f },
            new MaterialDecorSpec { SimHash = "AluminumOre", Percent = 10f },
            new MaterialDecorSpec { SimHash = "FoolsGold", Percent = 10f },
            new MaterialDecorSpec { SimHash = "Steel", Percent = 20f },
            new MaterialDecorSpec { SimHash = "TempConductorSolid", Percent = 20f },
            new MaterialDecorSpec { SimHash = "Aluminum", Percent = 20f },
            new MaterialDecorSpec { SimHash = "DepletedUranium", Percent = -50f },
            new MaterialDecorSpec { SimHash = "Graphite", Percent = 20f },
            new MaterialDecorSpec { SimHash = "Glass", Percent = 20f },
            new MaterialDecorSpec { SimHash = "Pearl", Percent = 10f },
            new MaterialDecorSpec { SimHash = "PalmWood", Percent = 50f },
            new MaterialDecorSpec { SimHash = "FabricatedWood", Percent = 10f }
        };

        /// <summary>设计表中 14 项里有 1 项是 prefab，元素配置为 13 项。</summary>
        public static int ElementCount
        {
            get { return AllValue.Length; }
        }

        public static MaterialDecorSpec[] All
        {
            get { return AllValue; }
        }

        public static MaterialDecorSpec Get(string simHash)
        {
            if (string.IsNullOrEmpty(simHash))
            {
                return null;
            }

            for (int i = 0; i < AllValue.Length; i++)
            {
                if (string.Equals(AllValue[i].SimHash, simHash, StringComparison.Ordinal))
                {
                    return AllValue[i];
                }
            }

            return null;
        }
    }
}
