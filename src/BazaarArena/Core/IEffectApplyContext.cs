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

    /// <summary>施放者物品在己方一侧的下标（用于如「右侧物品」即 ItemIndex+1）。</summary>
    int ItemIndex { get; }

    /// <summary>从施放者物品模板按 key 取值（缺省时用 defaultValue），若 applyCritMultiplier 则乘暴击倍率。用于数值与目标数等，key 建议用 nameof(ItemTemplate.XXX)。</summary>
    int GetResolvedValue(string key, bool applyCritMultiplier = false, int defaultValue = 0);

    /// <summary>当前能力的目标选择条件（用于冻结/减速/充能/加速等多目标效果）；由模拟器从 AbilityDefinition.TargetCondition 注入。</summary>
    Condition? TargetCondition { get; }

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

    /// <summary>对目标施加冻结 freezeMs 毫秒。目标池：有冷却时间的物品且满足 targetCondition（默认 DifferentSide）；不放回随机选取至多 targetCount 个；触发次数按实际目标数。</summary>
    void ApplyFreeze(int freezeMs, int targetCount, Condition? targetCondition = null);

    /// <summary>对目标施加减速 slowMs 毫秒。目标池：有冷却时间且满足 targetCondition（默认 DifferentSide）；不放回随机选取至多 targetCount 个。</summary>
    void ApplySlow(int slowMs, int targetCount, Condition? targetCondition = null);

    /// <summary>对目标施加充能 chargeMs 毫秒。目标池：己方有冷却时间且满足 targetCondition（默认 SameSide）；不放回随机选取至多 targetCount 个。</summary>
    void ApplyCharge(int chargeMs, int targetCount, Condition? targetCondition = null);

    /// <summary>对目标施加加速 hasteMs 毫秒。目标池：己方有冷却时间且满足 targetCondition（默认 SameSide）；不放回随机选取至多 targetCount 个。</summary>
    void ApplyHaste(int hasteMs, int targetCount, Condition? targetCondition = null);

    /// <summary>己方所有武器物品的 Damage 增加 value。</summary>
    void AddWeaponDamageBonusToCasterSide(int value);

    /// <summary>己方指定位置物品若为武器则 Damage 增加 value（限本场战斗），并记录日志「伤害提高 →[目标]」。</summary>
    void AddWeaponDamageBonusToCasterSideItem(int value, int targetItemIndexOnCasterSide);

    /// <summary>遍历对方护盾物品（按导入快照判断），将每件物品的 Shield 属性减少 reduceBy，最多减到 0（限本场战斗）。</summary>
    void ReduceOpponentShieldItemsShield(int reduceBy);

    /// <summary>记录效果日志。showCrit 为 true 时显示「（暴击）」；仅对实际参与暴击的效果传 true（如伤害/灼烧/治疗等），冻结/减速等不可暴击效果传 false。</summary>
    void LogEffect(string effectName, int value, string? extraSuffix = null, bool showCrit = false);
}
