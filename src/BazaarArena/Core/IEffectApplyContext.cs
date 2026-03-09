namespace BazaarArena.Core;

/// <summary>效果应用上下文接口：由模拟器实现，供 EffectDefinition.Apply 委托调用。不依赖具体战斗类型。</summary>
public interface IEffectApplyContext
{
    /// <summary>当前效果数值（已乘暴击倍率，若适用）；仅当效果指定了 ValueKey（如 WeaponDamageBonus）时由模拟器填入，否则委托应使用 GetResolvedValue 按 key 取值。</summary>
    int Value { get; }

    /// <summary>施放者物品是否带吸血（LifeSteal &gt; 0）。</summary>
    bool HasLifeSteal { get; }

    /// <summary>本轮能力是否掷出暴击；可暴击效果在 LogEffect 时传 showCrit: IsCrit，不可暴击效果传 false。</summary>
    bool IsCrit { get; }

    /// <summary>从施放者物品模板按 key 取基础值，若 applyCritMultiplier 则乘暴击倍率；用于只读效果在委托内按常量 key（如 nameof(ItemTemplate.Damage)）取值，避免依赖 EffectDefinition.ValueKey。</summary>
    int GetResolvedValue(string key, bool applyCritMultiplier);

    /// <summary>从施放者物品模板读取整数字段（如 FreezeTargetCount、SlowTargetCount）；key 建议用 nameof(ItemTemplate.XXX) 避免魔法字符串。</summary>
    int GetCasterItemInt(string key, int defaultValue);

    /// <summary>对敌方造成伤害；isBurn 为灼烧结算。返回实际扣减的生命值（用于吸血等）。</summary>
    int ApplyDamageToOpp(int value, bool isBurn);

    /// <summary>治疗己方指定生命值（不超过上限）。</summary>
    void HealCaster(int amount);

    /// <summary>为敌方叠加灼烧值。</summary>
    void AddBurnToOpp(int value);

    /// <summary>为敌方叠加剧毒值。</summary>
    void AddPoisonToOpp(int value);

    /// <summary>为己方增加护盾。</summary>
    void AddShieldToCaster(int value);

    /// <summary>治疗己方并清除 5% 灼烧/剧毒；返回实际治疗量。</summary>
    int HealCasterWithDebuffClear(int requestedHeal);

    /// <summary>为己方增加生命再生。</summary>
    void AddRegenToCaster(int value);

    /// <summary>为施放者物品充能（毫秒）；fullAndShouldCast 表示是否已满且应加入施放队列。</summary>
    void ChargeCasterItem(int chargeMs, out bool fullAndShouldCast);

    /// <summary>对敌人物品施加减益：冻结 freezeMs 毫秒，选取 targetCount 个目标（有冷却优先）。</summary>
    void ApplyFreeze(int freezeMs, int targetCount);

    /// <summary>对敌人物品施加减益：减速 slowMs 毫秒，选取 targetCount 个目标（有冷却优先）。</summary>
    void ApplySlow(int slowMs, int targetCount);

    /// <summary>己方所有武器物品的 Damage 增加 value。</summary>
    void AddWeaponDamageBonusToCasterSide(int value);

    /// <summary>遍历对方护盾物品（按导入快照判断），将每件物品的 Shield 属性减少 reduceBy，最多减到 0（限本场战斗）。</summary>
    void ReduceOpponentShieldItemsShield(int reduceBy);

    /// <summary>记录效果日志。showCrit 为 true 时显示「（暴击）」；仅对实际参与暴击的效果传 true（如伤害/灼烧/治疗等），冻结/减速等不可暴击效果传 false。</summary>
    void LogEffect(string effectName, int value, string? extraSuffix = null, bool showCrit = false);
}
