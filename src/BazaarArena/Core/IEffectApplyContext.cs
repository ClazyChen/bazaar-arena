using BazaarArena.BattleSimulator;

namespace BazaarArena.Core;

/// <summary>效果应用上下文接口：由模拟器实现，供 AbilityDefinition.Apply 委托调用。不依赖具体战斗类型。</summary>
public interface IEffectApplyContext
{
    /// <summary>当前效果数值（已乘暴击倍率，若适用）；仅当效果指定了 ValueKey（如 AddAttribute）时由模拟器填入，否则委托应使用 GetResolvedValue 按 key 取值。</summary>
    int Value { get; }

    /// <summary>施放者物品是否带吸血（LifeSteal &gt; 0）。</summary>
    bool HasLifeSteal { get; }

    /// <summary>施放者物品当前是否处于飞行状态。</summary>
    bool IsCasterInFlight { get; }

    /// <summary>本轮能力是否掷出暴击；可暴击效果在 LogEffect 时传 showCrit: IsCrit，不可暴击效果传 false。</summary>
    bool IsCrit { get; }

    /// <summary>施放者物品（能力持有者）；槽位等可用 CasterItem.ItemIndex / CasterItem.SideIndex。</summary>
    BattleItemState CasterItem { get; }

    /// <summary>从施放者物品模板按 key 取值（缺省时用 defaultValue），若 applyCritMultiplier 则乘暴击倍率。用于数值与目标数等，key 建议用 Key.Damage、Key.Shield 等。</summary>
    int GetResolvedValue(string key, bool applyCritMultiplier = false, int defaultValue = 0);

    /// <summary>当前能力的目标选择条件（用于冻结/减速/充能/加速/摧毁等多目标效果）；由模拟器从 AbilityDefinition.TargetCondition 注入。</summary>
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

    /// <summary>对目标施加冻结 freezeMs 毫秒。目标由 targetCondition 决定（null 时默认敌方、未摧毁且有冷却）；不放回随机选取至多 targetCount 个；触发次数按实际目标数。</summary>
    void ApplyFreeze(int freezeMs, int targetCount, Condition? targetCondition = null);

    /// <summary>对目标施加减速 slowMs 毫秒。目标由 targetCondition 决定（null 时默认敌方、未摧毁且有冷却）；不放回随机选取至多 targetCount 个。</summary>
    void ApplySlow(int slowMs, int targetCount, Condition? targetCondition = null);

    /// <summary>对目标施加充能 chargeMs 毫秒。目标由 targetCondition 决定（null 时默认己方、未摧毁且有冷却）；不放回随机选取至多 targetCount 个。</summary>
    void ApplyCharge(int chargeMs, int targetCount, Condition? targetCondition = null);

    /// <summary>对目标施加加速 hasteMs 毫秒。目标由 targetCondition 决定（null 时默认己方、未摧毁且有冷却）；不放回随机选取至多 targetCount 个。</summary>
    void ApplyHaste(int hasteMs, int targetCount, Condition? targetCondition = null);

    /// <summary>修复已摧毁物品：目标由 targetCondition 与已摧毁组合（null 时默认己方）；不放回随机选取至多 targetCount 个，将其设为未摧毁并重置冷却已过时间。</summary>
    void ApplyRepair(int targetCount, Condition? targetCondition = null);

    /// <summary>对己方满足 targetCondition 的物品增加指定属性（限本场战斗）。attributeName 为模板属性名（如 Damage、Poison、Key.InFlight），value 为增加量；目标由 targetCondition 筛选（Source=施放者）。InFlight 时设为 value != 0。</summary>
    void AddAttributeToCasterSide(string attributeName, int value, Condition? targetCondition);

    /// <summary>对己方满足 targetCondition 的物品将指定属性设为 value（限本场战斗）。用于 StopFlying（Key.InFlight, 0）等。目标由 targetCondition 筛选（Source=施放者）。</summary>
    void SetAttributeOnCasterSide(string attributeName, int value, Condition? targetCondition);

    /// <summary>对敌方满足 targetCondition 的物品减少指定属性（限本场战斗，不低于 0）。attributeName 为模板属性名（如 Shield），value 为减少量；目标由 targetCondition 筛选（Source=施放者），可用 Condition.WithTag(Tag.Shield) 等。</summary>
    void ReduceAttributeToOpponentSide(string attributeName, int value, Condition? targetCondition);

    /// <summary>记录效果日志。showCrit 为 true 时显示「（暴击）」；仅对实际参与暴击的效果传 true（如伤害/灼烧/治疗等），冻结/减速等不可暴击效果传 false。</summary>
    void LogEffect(string effectName, int value, string? extraSuffix = null, bool showCrit = false);

    /// <summary>设置施放者物品的飞行状态（开始/结束飞行）。</summary>
    void SetCasterInFlight(bool inFlight);

    /// <summary>摧毁：对己方满足 targetCondition 的未摧毁物品选取至多 targetCount 个施加摧毁；先触发 Destroy 再标记 Destroyed。目标不要求有冷却。</summary>
    void ApplyDestroy(int targetCount, Condition? targetCondition = null);
}
