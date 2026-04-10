#pragma once

#include <array>
#include <bazaararena/core/AbilityType.hpp>

namespace bazaararena::core {

// 这个文件定义了各种能力的应用效果

// 前置声明
class AbilityDefinition;
class BattleContext;

// 空函数
constexpr void None(const AbilityDefinition& ability, const BattleContext& ctx) {
    // 什么都不做
};

// 应用某个能力的效果
void Damage(const AbilityDefinition& ability, const BattleContext& ctx); // 造成伤害
void Burn(const AbilityDefinition& ability, const BattleContext& ctx); // 造成灼烧
void Poison(const AbilityDefinition& ability, const BattleContext& ctx); // 造成剧毒
void Heal(const AbilityDefinition& ability, const BattleContext& ctx); // 治疗
void Shield(const AbilityDefinition& ability, const BattleContext& ctx); // 获得护盾
void Regen(const AbilityDefinition& ability, const BattleContext& ctx); // 生命再生
void Freeze(const AbilityDefinition& ability, const BattleContext& ctx); // 冻结
void Haste(const AbilityDefinition& ability, const BattleContext& ctx); // 加速
void Slow(const AbilityDefinition& ability, const BattleContext& ctx); // 减速
void Charge(const AbilityDefinition& ability, const BattleContext& ctx); // 充能
void Reload(const AbilityDefinition& ability, const BattleContext& ctx); // 装填
void Repair(const AbilityDefinition& ability, const BattleContext& ctx); // 修复
void Destroy(const AbilityDefinition& ability, const BattleContext& ctx); // 摧毁
void AddAttribute(const AbilityDefinition& ability, const BattleContext& ctx); // 添加属性
void ReduceAttribute(const AbilityDefinition& ability, const BattleContext& ctx); // 减少属性
void GainGold(const AbilityDefinition& ability, const BattleContext& ctx); // 获得金币
void Resistance(const AbilityDefinition& ability, const BattleContext& ctx); // 抗性
void PoisonSelf(const AbilityDefinition& ability, const BattleContext& ctx); // 自身施加剧毒
void Cast(const AbilityDefinition& ability, const BattleContext& ctx); // 立刻施放

// 能力应用表
constexpr std::array<void(*)(const AbilityDefinition& ability, const BattleContext& ctx), AbilityType::Count> AbilityApplyTable = {
    None,
    Damage,
    Shield,
    Heal,
    Burn,
    Poison,
    Charge,
    Haste,
    Slow,
    Freeze,
    Reload,
    Repair,
    Destroy,
    AddAttribute,
    ReduceAttribute,
    GainGold,
    Regen,
    Resistance,
    PoisonSelf,
    Cast,
};


}