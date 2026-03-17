using System.Collections.Concurrent;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;

namespace BazaarArena.QualityDeckFinder;

/// <summary>
/// 机制标签派生器：从模板的 Abilities/Tags 推断“机制层”的标签，用于协同记忆与候选生成。
/// 注意：该标签用于搜索启发式，不要求 100% 精确；宁可保守，也不要误判太多导致噪声放大。
/// </summary>
public static class MechanicTagger
{
    // 用稳定字符串作为 key，便于持久化与调参。
    public static class Mechanic
    {
        public const string Damage = "Damage";
        public const string Shield = "Shield";
        public const string Heal = "Heal";
        public const string Regen = "Regen";
        public const string Burn = "Burn";
        public const string Poison = "Poison";

        public const string Charge = "Charge";
        public const string Freeze = "Freeze";
        public const string Slow = "Slow";
        public const string Haste = "Haste";
        public const string Destroy = "Destroy";
        public const string Repair = "Repair";
        public const string Reload = "Reload";

        public const string UseItemTrigger = "UseItemTrigger";
        public const string AmmoTrigger = "AmmoTrigger";
        public const string BattleStartTrigger = "BattleStartTrigger";
    }

    private static readonly ConcurrentDictionary<string, string[]> Cache = new(StringComparer.Ordinal);

    public static IReadOnlyList<string> GetMechanics(string itemName, IItemTemplateResolver db)
    {
        if (string.IsNullOrEmpty(itemName)) return [];
        return Cache.GetOrAdd(itemName, _ => Derive(itemName, db));
    }

    public static void ClearCache() => Cache.Clear();

    private static string[] Derive(string itemName, IItemTemplateResolver db)
    {
        var t = db.GetTemplate(itemName);
        if (t == null) return [];

        var set = new HashSet<string>(StringComparer.Ordinal);

        // 基于模板标签的少量补充（这里仍然是“机制”而非卡牌类型，如武器/工具等不加入）。
        if (t.Tags != null && t.Tags.Contains(Tag.Ammo))
            set.Add(Mechanic.Reload); // 弹药物品通常与装填/消耗机制相关（保守：只给一个宽标签）

        if (t.Abilities == null || t.Abilities.Count == 0)
            return set.ToArray();

        foreach (var a in t.Abilities)
        {
            // 触发类型
            if (a.TriggerName == Trigger.UseItem) set.Add(Mechanic.UseItemTrigger);
            else if (a.TriggerName == Trigger.Ammo) set.Add(Mechanic.AmmoTrigger);
            else if (a.TriggerName == Trigger.BattleStart) set.Add(Mechanic.BattleStartTrigger);

            // 基于 Apply 委托推断（最可靠）
            if (a.Apply == Effect.DamageApply) set.Add(Mechanic.Damage);
            else if (a.Apply == Effect.ShieldApply) set.Add(Mechanic.Shield);
            else if (a.Apply == Effect.HealApply) set.Add(Mechanic.Heal);
            else if (a.Apply == Effect.RegenApply) set.Add(Mechanic.Regen);
            else if (a.Apply == Effect.BurnApply) set.Add(Mechanic.Burn);
            else if (a.Apply == Effect.PoisonApply || a.Apply == Effect.PoisonSelfApply) set.Add(Mechanic.Poison);
            else if (a.Apply == Effect.ChargeApply) set.Add(Mechanic.Charge);
            else if (a.Apply == Effect.FreezeApply) set.Add(Mechanic.Freeze);
            else if (a.Apply == Effect.SlowApply) set.Add(Mechanic.Slow);
            else if (a.Apply == Effect.HasteApply) set.Add(Mechanic.Haste);
            else if (a.Apply == Effect.DestroyApply) set.Add(Mechanic.Destroy);
            else if (a.Apply == Effect.RepairApply) set.Add(Mechanic.Repair);
            else if (a.Apply == Effect.ReloadApply) set.Add(Mechanic.Reload);
        }

        return set.ToArray();
    }
}

