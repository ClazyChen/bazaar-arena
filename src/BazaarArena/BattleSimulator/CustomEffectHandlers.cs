using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>自定义效果（EffectKind.Other）的派发与实现，按 CustomEffectId 执行具体逻辑。</summary>
public static class CustomEffectHandlers
{
    /// <summary>自定义效果处理委托。</summary>
    public delegate void Handler(
        int sideIndex,
        int itemIndex,
        BattleItemState casterItem,
        AbilityDefinition ability,
        EffectDefinition effect,
        BattleSide side0,
        BattleSide side1,
        int timeMs,
        IBattleLogSink logSink);

    private static readonly Dictionary<string, Handler> Handlers = new()
    {
        ["WeaponDamageBonus"] = (sideIndex, itemIndex, casterItem, ability, effect, side0, side1, timeMs, logSink) =>
        {
            var side = sideIndex == 0 ? side0 : side1;
            int value = effect.ResolveValue(casterItem.Template, casterItem.Tier, "Custom_0");
            foreach (var wi in side.Items)
            {
                if (wi.Destroyed) continue;
                if (wi.Template.Tags.Contains("武器"))
                    wi.Template.Damage = wi.Template.Damage.Add(value);
            }
            logSink.OnEffect(sideIndex, itemIndex, casterItem.Template.Name, "武器伤害提升", value, timeMs);
        },
    };

    /// <summary>根据 CustomEffectId 查找并执行处理；未找到则返回 false。</summary>
    public static bool TryExecute(
        string customEffectId,
        int sideIndex,
        int itemIndex,
        BattleItemState casterItem,
        AbilityDefinition ability,
        EffectDefinition effect,
        BattleSide side0,
        BattleSide side1,
        int timeMs,
        IBattleLogSink logSink)
    {
        if (string.IsNullOrEmpty(customEffectId) || !Handlers.TryGetValue(customEffectId, out var handler))
            return false;
        handler(sideIndex, itemIndex, casterItem, ability, effect, side0, side1, timeMs, logSink);
        return true;
    }
}
