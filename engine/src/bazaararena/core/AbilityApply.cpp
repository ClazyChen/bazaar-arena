#include "bazaararena/core/DerivedTag.hpp"
#include "bazaararena/formula/Formula.hpp"
#include <bazaararena/core/AbilityApply.hpp>
#include <bazaararena/core/BattleContext.hpp>
#include <bazaararena/core/Simulator.hpp>
#include <bazaararena/formula/Percent.hpp>
#include <bazaararena/formula/Condition.hpp>


namespace bazaararena::core {

// 辅助函数：进行暴击检查
bool CheckCrit(const AbilityDefinition& ability, const BattleContext& ctx) {
    auto caster = ctx.caster;
    if (ability.trigger_entries[0].trigger != Trigger::Cast) return false; // 只有施放触发器可以暴击
    if (caster->attrs[ItemKey::DerivedTags] & DerivedTag::Crit) return false; // 不能暴击的物品不进行检查
    int item_mask = 1 << ((caster->attrs[ItemKey::SideIndex] << 4) | caster->attrs[ItemKey::ItemIndex]);
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    if (simulator->crit_bitmap & item_mask) return true; // 已经暴击过
    if (simulator->crit_checked_bitmap & item_mask) return false; // 已经检查过暴击，但未暴击
    simulator->crit_checked_bitmap |= item_mask;
    int crit_rate = ctx.GetItemInt(caster, ItemKey::CritRate);
    if (crit_rate <= 0) return false;
    bool crit = simulator->rng.Next100() < crit_rate;
    if (crit) {
        simulator->crit_bitmap |= item_mask;
    }
    return crit;
}

// 造成伤害
void Damage(const AbilityDefinition& ability, const BattleContext& ctx) {
    int damage = ctx.GetItemInt(ctx.caster, ability.value_key);
    if (CheckCrit(ability, ctx)) {
        damage *= ctx.GetItemInt(ctx.caster, ItemKey::CritDamage);
    }
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    auto& opp = simulator->sides[1 - ctx.caster->attrs[ItemKey::SideIndex]];
    opp.ApplyDamage(damage, false, false);
    // 触发「伤害」触发器
    simulator->InvokeTrigger(Trigger::Damage, ctx.caster, &opp.items[0], 1);
}

// 造成灼烧
void Burn(const AbilityDefinition& ability, const BattleContext& ctx) {
    int damage = ctx.GetItemInt(ctx.caster, ability.value_key);
    if (CheckCrit(ability, ctx)) {
        damage *= ctx.GetItemInt(ctx.caster, ItemKey::CritDamage);
    }
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    auto& opp = simulator->sides[1 - ctx.caster->attrs[ItemKey::SideIndex]];
    opp.attrs[SideKey::Burn] += damage;
    // 触发「灼烧」触发器
    simulator->InvokeTrigger(Trigger::Burn, ctx.caster, &opp.items[0], 1);
}

// 造成剧毒
void Poison(const AbilityDefinition& ability, const BattleContext& ctx) {
    int damage = ctx.GetItemInt(ctx.caster, ability.value_key);
    if (CheckCrit(ability, ctx)) {
        damage *= ctx.GetItemInt(ctx.caster, ItemKey::CritDamage);
    }
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    auto& opp = simulator->sides[1 - ctx.caster->attrs[ItemKey::SideIndex]];
    opp.attrs[SideKey::Poison] += damage;
    // 触发「中毒」触发器
    simulator->InvokeTrigger(Trigger::Poison, ctx.caster, &opp.items[0], 1);
}

// 治疗
void Heal(const AbilityDefinition& ability, const BattleContext& ctx) {
    int heal = ctx.GetItemInt(ctx.caster, ability.value_key);
    if (CheckCrit(ability, ctx)) {
        heal *= ctx.GetItemInt(ctx.caster, ItemKey::CritDamage);
    }
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    auto& side = simulator->sides[ctx.caster->attrs[ItemKey::SideIndex]];
    side.ApplyHeal(heal);
    // 治疗时，清除 5% 治疗量的灼烧和剧毒
    int clear = formula::PercentFloor(heal, 5);
    side.attrs[SideKey::Burn] = std::max(0, side.attrs[SideKey::Burn] - clear);
    side.attrs[SideKey::Poison] = std::max(0, side.attrs[SideKey::Poison] - clear);
    // 触发「治疗」触发器
    simulator->InvokeTrigger(Trigger::Heal, ctx.caster, &side.items[0], 1);
}

// 生命再生
void Regen(const AbilityDefinition& ability, const BattleContext& ctx) {
    int regen = ctx.GetItemInt(ctx.caster, ability.value_key);
    if (CheckCrit(ability, ctx)) {
        regen *= ctx.GetItemInt(ctx.caster, ItemKey::CritDamage);
    }
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    auto& side = simulator->sides[ctx.caster->attrs[ItemKey::SideIndex]];
    side.attrs[SideKey::Regen] += regen;
    // 触发「生命再生」触发器
    simulator->InvokeTrigger(Trigger::Regen, ctx.caster, &side.items[0], 1);
}

// 抗性
void Resistance(const AbilityDefinition& ability, const BattleContext& ctx) {
    int resistance = ctx.GetItemInt(ctx.caster, ability.value_key);
    if (CheckCrit(ability, ctx)) {
        resistance *= ctx.GetItemInt(ctx.caster, ItemKey::CritDamage);
    }
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    auto& side = simulator->sides[ctx.caster->attrs[ItemKey::SideIndex]];
    side.attrs[SideKey::Resistance] += resistance;
    // 目前没有效果需要在抗性触发器中处理，不实现触发器
}

// 自身施加剧毒
void PoisonSelf(const AbilityDefinition& ability, const BattleContext& ctx) {
    int damage = ctx.GetItemInt(ctx.caster, ability.value_key);
    if (CheckCrit(ability, ctx)) {
        damage *= ctx.GetItemInt(ctx.caster, ItemKey::CritDamage);
    }
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    auto& side = simulator->sides[ctx.caster->attrs[ItemKey::SideIndex]];
    side.attrs[SideKey::Poison] += damage;
    // 触发「剧毒」触发器
    simulator->InvokeTrigger(Trigger::Poison, ctx.caster, &side.items[0], 1);
}

// 获得护盾
void Shield(const AbilityDefinition& ability, const BattleContext& ctx) {
    int shield = ctx.GetItemInt(ctx.caster, ability.value_key);
    if (CheckCrit(ability, ctx)) {
        shield *= ctx.GetItemInt(ctx.caster, ItemKey::CritDamage);
    }
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    auto& side = simulator->sides[ctx.caster->attrs[ItemKey::SideIndex]];
    side.attrs[SideKey::Shield] += shield;
    // 触发「护盾」触发器
    simulator->InvokeTrigger(Trigger::Shield, ctx.caster, &side.items[0], 1);
}

// 辅助函数：获取满足条件的目标，返回被选中的目标数量
int GetTargets(const AbilityDefinition& ability, BattleContext& ctx, formula::Formula force_condition = formula::True) {
    auto target_count = ctx.GetItemInt(ctx.caster, ability.target_count_key);
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    int candidate_count = 0;
    for (int i = 0; i < Simulator::SideCount; i++) {
        for (int j = 0; j < simulator->sides[i].attrs[SideKey::ItemCount]; j++) {
            auto& item = simulator->sides[i].items[j];
            ctx.item = &item;
            if (!force_condition(ctx)) continue; // 强制条件不满足
            if (!ability.target_condition(ctx)) continue; // 目标条件不满足
            simulator->targets[candidate_count++] = &item;
        }
    }
    // 如果候选数量小于目标数量，则返回所有候选均为目标，全部返回
    if (candidate_count < target_count) return candidate_count;
    // 否则，随机打乱候选物品，然后返回前 target_count 个
    std::shuffle(simulator->targets.begin(), simulator->targets.begin() + candidate_count, simulator->rng);
    return target_count;
}

// 充能
void Charge(const AbilityDefinition& ability, const BattleContext& ctx) {
    int charge = ctx.GetItemInt(ctx.caster, ability.value_key);
    BattleContext ctx_copy = ctx; // 复制一份上下文用于选择目标
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    int target_count = GetTargets(ability, ctx_copy, formula::And<
        condition::HasCooldown,
        condition::NotDestroyed,
        condition::NotFullyCharged
        >
    );
    for (int i = 0; i < target_count; i++) {
        auto& target = *simulator->targets[i];
        target.attrs[ItemKey::ChargedTime] += charge;
        simulator->CheckCharge(target);
        // 触发「充能」触发器
        simulator->InvokeTrigger(Trigger::Charge, ctx.caster, &target, 1);
    }
}

// 冻结
void Freeze(const AbilityDefinition& ability, const BattleContext& ctx) {
    int freeze = ctx.GetItemInt(ctx.caster, ability.value_key);
    BattleContext ctx_copy = ctx; // 复制一份上下文用于选择目标
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    int target_count = GetTargets(ability, ctx_copy, formula::And<
        condition::NotDestroyed
    >);
    for (int i = 0; i < target_count; i++) {
        auto& target = *simulator->targets[i];
        target.attrs[ItemKey::FreezeRemaining] += freeze;
        // 触发「冻结」触发器
        simulator->InvokeTrigger(Trigger::Freeze, ctx.caster, &target, 1);
    }
}

// 减速
void Slow(const AbilityDefinition& ability, const BattleContext& ctx) {
    int slow = ctx.GetItemInt(ctx.caster, ability.value_key);
    BattleContext ctx_copy = ctx; // 复制一份上下文用于选择目标
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    int target_count = GetTargets(ability, ctx_copy, formula::And<
        condition::HasCooldown,
        condition::NotDestroyed
    >);
    for (int i = 0; i < target_count; i++) {
        auto& target = *simulator->targets[i];
        target.attrs[ItemKey::SlowRemaining] += slow;
        // 触发「减速」触发器
        simulator->InvokeTrigger(Trigger::Slow, ctx.caster, &target, 1);
    }
}

// 加速
void Haste(const AbilityDefinition& ability, const BattleContext& ctx) {
    int haste = ctx.GetItemInt(ctx.caster, ability.value_key);
    BattleContext ctx_copy = ctx; // 复制一份上下文用于选择目标
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    int target_count = GetTargets(ability, ctx_copy, formula::And<
        condition::HasCooldown,
        condition::NotDestroyed
    >);
    for (int i = 0; i < target_count; i++) {
        auto& target = *simulator->targets[i];
        target.attrs[ItemKey::HasteRemaining] += haste;
        // 触发「加速」触发器
        simulator->InvokeTrigger(Trigger::Haste, ctx.caster, &target, 1);
    }
}

// 装填
void Reload(const AbilityDefinition& ability, const BattleContext& ctx) {
    int reload = ctx.GetItemInt(ctx.caster, ability.value_key);
    BattleContext ctx_copy = ctx; // 复制一份上下文用于选择目标
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    int target_count = GetTargets(ability, ctx_copy, formula::And<
        condition::NotDestroyed,
        condition::HasDerivedTag<DerivedTag::Ammo>,
        condition::Lt<
            formula::Item<ItemKey::AmmoRemaining>, 
            formula::Item<ItemKey::AmmoCap>>
    >);
    for (int i = 0; i < target_count; i++) {
        auto& target = *simulator->targets[i];
        target.attrs[ItemKey::AmmoRemaining] = std::min(target.attrs[ItemKey::AmmoRemaining] + reload, ctx.GetItemInt(&target, ItemKey::AmmoCap));
        // 触发「装填」触发器
        simulator->InvokeTrigger(Trigger::Reload, ctx.caster, &target, 1);
        simulator->CheckCharge(target);
    }
}

// 修复
void Repair(const AbilityDefinition& ability, const BattleContext& ctx) {
    BattleContext ctx_copy = ctx; // 复制一份上下文用于选择目标
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    int target_count = GetTargets(ability, ctx_copy, condition::Destroyed);
    for (int i = 0; i < target_count; i++) {
        auto& target = *simulator->targets[i];
        target.attrs[ItemKey::Destroyed] = 0;
        // 触发「修复」触发器
        simulator->InvokeTrigger(Trigger::Repair, ctx.caster, &target, 1);
        simulator->CheckCharge(target);
    }
}

// 摧毁
void Destroy(const AbilityDefinition& ability, const BattleContext& ctx) {
    BattleContext ctx_copy = ctx; // 复制一份上下文用于选择目标
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    int target_count = GetTargets(ability, ctx_copy, condition::NotDestroyed);
    for (int i = 0; i < target_count; i++) {
        auto& target = *simulator->targets[i];
        // 触发「摧毁」触发器
        simulator->InvokeTrigger(Trigger::Destroy, ctx.caster, &target, 1);
        // 摧毁物品
        target.attrs[ItemKey::Destroyed] = 1;
    }
}

// 增加属性
void AddAttribute(const AbilityDefinition& ability, const BattleContext& ctx) {
    int attribute = ctx.GetItemInt(ctx.caster, ability.value_key);
    BattleContext ctx_copy = ctx; // 复制一份上下文用于选择目标
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    int target_count = GetTargets(ability, ctx_copy, condition::NotDestroyed);
    for (int i = 0; i < target_count; i++) {
        auto& target = *simulator->targets[i];
        target.attrs[ability.attribute_key] += attribute;
        if (ability.attribute_key == ItemKey::CritRate) {
            // 触发「暴击率提高」触发器
            simulator->InvokeTrigger(Trigger::CritRateIncreased, ctx.caster, &target, 1);
        }
    }
}

// 减少属性
void ReduceAttribute(const AbilityDefinition& ability, const BattleContext& ctx) {
    int attribute = ctx.GetItemInt(ctx.caster, ability.value_key);
    BattleContext ctx_copy = ctx; // 复制一份上下文用于选择目标
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    int target_count = GetTargets(ability, ctx_copy, condition::NotDestroyed);
    for (int i = 0; i < target_count; i++) {
        auto& target = *simulator->targets[i];
        target.attrs[ability.attribute_key] = std::max(0, target.attrs[ability.attribute_key] - attribute);
    }
}

// 获得金币
void GainGold(const AbilityDefinition& ability, const BattleContext& ctx) {
    int gold = ctx.GetItemInt(ctx.caster, ability.value_key);
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    simulator->sides[ctx.caster->attrs[ItemKey::SideIndex]].attrs[SideKey::Gold] += gold;
}

// 立刻施放
void Cast(const AbilityDefinition& ability, const BattleContext& ctx) {
    auto simulator = const_cast<Simulator*>(ctx.simulator);
    simulator->cast_queue |= 1 << ((ctx.caster->attrs[ItemKey::SideIndex] << 4) | ctx.caster->attrs[ItemKey::ItemIndex]);
}

} // namespace bazaararena::core