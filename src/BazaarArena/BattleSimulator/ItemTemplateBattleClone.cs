using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>战斗实例模板克隆：按指定 tier 扁平化 _intsByTier，并尽可能共享只读结构以降低 BuildSide 分配。</summary>
internal static class ItemTemplateBattleClone
{
    /// <summary>
    /// 生成战斗内可写的模板实例：
    /// - Name/Desc/MinTier/Size/Hero 拷贝
    /// - Tags/Abilities/Auras 共享引用（运行期只读）
    /// - _intsByTier 扁平化为单值（每个 key 存为长度 1 的列表）
    /// - 仅对 source.OverridableAttributes 声明的 key 应用 overrides
    /// </summary>
    public static ItemTemplate Create(ItemTemplate source, ItemTier tier, IReadOnlyDictionary<string, int>? overrides)
    {
        var battle = new ItemTemplate
        {
            Name = source.Name,
            Desc = source.Desc,
            MinTier = source.MinTier,
            Size = source.Size,
            Hero = source.Hero,
            Tags = source.Tags,
            Abilities = source.Abilities,
            Auras = source.Auras,
            // OverridableAttributes/协同先验等对战内不需要；保留在 source 侧即可
        };

        foreach (var kv in source.GetIntsByTierView())
        {
            string key = kv.Key;
            // 若 source 已是“扁平化模板”（每个 key 仅一个值），则直接取 list[0]，避免 tier 映射与 GetInt 分支。
            // 否则按 tier 取值扁平化为单值。
            int v = kv.Value.Count == 1 ? kv.Value[0] : source.GetInt(key, tier, defaultValue: 0);
            battle.SetInt(key, v);
        }

        if (overrides != null && source.OverridableAttributes != null)
        {
            foreach (var kv in overrides)
            {
                if (source.OverridableAttributes.ContainsKey(kv.Key))
                    battle.SetInt(kv.Key, kv.Value);
            }
        }

        return battle;
    }
}

