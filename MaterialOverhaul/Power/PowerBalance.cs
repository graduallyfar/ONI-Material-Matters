using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MaterialOverhaul.Power
{
    /// <summary>电力0.0.2首轮测试初值；相对电导率是游戏参数，不代表原版属性或实测物性。</summary>
    public static class PowerBalance
    {
        public static readonly LineLossParameters Parameters = new LineLossParameters(
            pc: 500, l0: 100, powerReference: 1000, conductivityReference: 1,
            a0: .008, a1: .012, nu: .6, gamma: .5, alpha: .6, beta: .5,
            lambdaMin: .03, deltaLambda: 1.2, k: 1, a: .15);

        public static readonly IDictionary<string, double> Conductivities =
            new ReadOnlyDictionary<string, double>(new Dictionary<string, double>
            {
                { "Copper", 1 }, { "Gold", .75 }, { "Aluminum", .65 }, { "Electrum", .85 },
                { "Cobalt", .28 }, { "Iron", .18 }, { "Tungsten", .30 }, { "Niobium", .20 },
                { "Steel", .15 }, { "TempConductorSolid", .85 }, { "Lead", .08 }, { "SolidMercury", .04 },
                { "Nickel", .20 }, { "Iridium", .22 }, { "Zinc", .28 },
                { "Cuprite", .45 }, { "GoldAmalgam", .28 }, { "AluminumOre", .30 }, { "Cobaltite", .18 },
                { "IronOre", .12 }, { "FoolsGold", .10 }, { "Wolframite", .15 }, { "UraniumOre", .015 },
                { "DepletedUranium", .04 }, { "EnrichedUranium", .04 }, { "Cinnabar", .01 },
                { "NickelOre", .10 }, { "ZincOre", .15 }, { "Galena", .025 }
            });
    }
}
