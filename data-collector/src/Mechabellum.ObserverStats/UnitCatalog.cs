namespace Mechabellum.ObserverStats;

public static class UnitCatalog
{
    private static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [1] = "Fortress / 堡垒",
        [2] = "Marksman / 神射手",
        [3] = "Vulcan / 火神",
        [4] = "Melting Point / 熔点",
        [5] = "Rhino / 犀牛",
        [6] = "Wasp / 黄蜂",
        [7] = "Mustang / 野马",
        [8] = "Steel Ball / 钢球",
        [9] = "Fang / 尖牙",
        [10] = "Crawler / 爬虫",
        [11] = "Overlord / 霸王",
        [12] = "Stormcaller / 风暴召唤者",
        [13] = "Sledgehammer / 铁锤",
        [14] = "Hacker / 黑客",
        [15] = "Arclight / 弧光",
        [16] = "Phoenix / 凤凰",
        [17] = "War Factory / 战争工厂",
        [18] = "Wraith / 鬼魅",
        [19] = "Scorpion / 蝎子",
        [20] = "Fire Badger / 火焰獾",
        [21] = "Sabertooth / 剑齿虎",
        [22] = "Typhoon / 台风",
        [23] = "Sandworm / 沙虫",
        [24] = "Tarantula / 狼蛛",
        [25] = "Phantom Ray / 幻影射线",
        [26] = "Farseer / 先知",
        [27] = "Raiden / 雷电",
        [28] = "Hound / 猎犬",
        [29] = "Abyss / 深渊",
        [30] = "Void Eye / 虚空之眼",
        [31] = "Vortex / 涡旋",
        [2002] = "Mountain / 山岳"
    };

    public static string GetName(int unitId) =>
        Names.TryGetValue(unitId, out var name) ? name : $"Unknown / 未知单位 ({unitId})";
}
