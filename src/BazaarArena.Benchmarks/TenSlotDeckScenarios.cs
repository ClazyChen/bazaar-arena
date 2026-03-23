using BazaarArena.Core;

namespace BazaarArena.Benchmarks;

/// <summary>
/// 玩家等级 4+ 时槽位上限为 10；用于单局 Run 性能分析的参考卡组（全铜 tier 0）。
/// </summary>
internal static class TenSlotDeckScenarios
{
    /// <summary>与物品测试一致，保证 10 槽可用。</summary>
    internal const int PlayerLevel = 5;

    /// <summary>
    /// 参考卡组1：共 9 个槽位条目；其中中型「鲨齿爪」占 2 槽，与其余小型合计 10 槽（与 DeckManager 校验一致）。
    /// </summary>
    internal static Deck ReferenceDeck1()
    {
        string[] names =
        [
            "淬锋钢", "迷你弯刀", "鲨齿爪", "手斧", "靴里剑", "宠物石", "流星索", "左轮手枪", "刺刀",
        ];
        return MakeDeck(names);
    }

    /// <summary>参考卡组2：淬锋钢、海螺壳、独角鲸、迷幻蝠鲼、水母、雪怪蟹、毒须鲶、水草、珊瑚、鱼饵。</summary>
    internal static Deck ReferenceDeck2()
    {
        string[] names =
        [
            "淬锋钢", "海螺壳", "独角鲸", "迷幻蝠鲼", "水母", "雪怪蟹", "毒须鲶", "水草", "珊瑚", "鱼饵",
        ];
        return MakeDeck(names);
    }

    private static Deck MakeDeck(string[] itemNames)
    {
        var d = new Deck { PlayerLevel = PlayerLevel };
        foreach (var n in itemNames)
            d.Slots.Add(new DeckSlotEntry { ItemName = n, Tier = 0 });
        return d;
    }
}
