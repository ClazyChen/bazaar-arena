#pragma once

#include <bazaararena/core/ItemKey.hpp>
#include <bazaararena/formula/Formula.hpp>
#include <bazaararena/core/Tag.hpp>
#include <bazaararena/core/DerivedTag.hpp>

namespace bazaararena::condition {

using namespace bazaararena::formula;
using namespace bazaararena::core;

// 条件
constexpr Formula Always = True;
constexpr Formula Never = False;

constexpr Formula SameAsCaster = And<
    Eq<Item<ItemKey::SideIndex>, Caster<ItemKey::SideIndex>>,
    Eq<Item<ItemKey::ItemIndex>, Caster<ItemKey::ItemIndex>>
>;

constexpr Formula DifferentFromCaster = Not<SameAsCaster>;

constexpr Formula SameAsSource = And<
    Eq<Item<ItemKey::SideIndex>, Source<ItemKey::SideIndex>>,
    Eq<Item<ItemKey::ItemIndex>, Source<ItemKey::ItemIndex>>
>;

constexpr Formula SameAsTarget = And<
    Eq<Item<ItemKey::SideIndex>, Target<ItemKey::SideIndex>>,
    Eq<Item<ItemKey::ItemIndex>, Target<ItemKey::ItemIndex>>
>;

constexpr Formula TargetSameAsCaster = And<
    Eq<Caster<ItemKey::SideIndex>, Target<ItemKey::SideIndex>>,
    Eq<Caster<ItemKey::ItemIndex>, Target<ItemKey::ItemIndex>>
>;

constexpr Formula SameSide = Eq<Item<ItemKey::SideIndex>, Caster<ItemKey::SideIndex>>;
constexpr Formula DifferentSide = Not<SameSide>;

constexpr Formula Destroyed = Eq<Item<ItemKey::Destroyed>, Constant<1>>;
constexpr Formula NotDestroyed = Ne<Item<ItemKey::Destroyed>, Constant<1>>;

// 标签为位掩码，须 (bits & tag) != 0；不通过 And<Item, Constant> 组合（And 仅适用于 0/1 子式）。
template<int tag>
constexpr Formula HasTag = [](const BattleContext& ctx) -> int {
    const int bits = Item<ItemKey::Tags>(ctx);
    return (bits & tag) != 0 ? 1 : 0;
};

template<int tag>
constexpr Formula HasDerivedTag = [](const BattleContext& ctx) -> int {
    const int bits = Item<ItemKey::DerivedTags>(ctx);
    return (bits & tag) != 0 ? 1 : 0;
};

constexpr Formula IsSmall = Eq<Item<ItemKey::Size>, Constant<1>>;
constexpr Formula IsMedium = Eq<Item<ItemKey::Size>, Constant<2>>;
constexpr Formula IsLarge = Eq<Item<ItemKey::Size>, Constant<3>>;

template<int tag>
constexpr Formula NotHasTag = Not<HasTag<tag>>;

template<int tag>
constexpr Formula NotHasDerivedTag = Not<HasDerivedTag<tag>>;

constexpr Formula HasCooldown = HasDerivedTag<DerivedTag::Cooldown>;
constexpr Formula CanCrit = HasDerivedTag<DerivedTag::Crit>;

constexpr Formula NotFullyCharged = Ne<Item<ItemKey::ChargedTime>, Item<ItemKey::Cooldown>>;
constexpr Formula NotFrozen = Eq<Item<ItemKey::FreezeRemaining>, Constant<0>>;

constexpr Formula AdjacentToCaster = And<
    Eq<Item<ItemKey::SideIndex>, Caster<ItemKey::SideIndex>>,
    Eq<Abs<Sub<Item<ItemKey::ItemIndex>, Caster<ItemKey::ItemIndex>>>, Constant<1>>
>;

constexpr Formula LeftOfCaster = And<
    Eq<Item<ItemKey::SideIndex>, Caster<ItemKey::SideIndex>>,
    Eq<Sub<Caster<ItemKey::ItemIndex>, Item<ItemKey::ItemIndex>>, Constant<1>>
>;

constexpr Formula StrictlyLeftOfCaster = And<
    Eq<Item<ItemKey::SideIndex>, Caster<ItemKey::SideIndex>>,
    Lt<Item<ItemKey::ItemIndex>, Caster<ItemKey::ItemIndex>>
>;

constexpr Formula RightOfCaster = And<
    Eq<Item<ItemKey::SideIndex>, Caster<ItemKey::SideIndex>>,
    Eq<Sub<Item<ItemKey::ItemIndex>, Caster<ItemKey::ItemIndex>>, Constant<1>>
>;

constexpr Formula StrictlyRightOfCaster = And<
    Eq<Item<ItemKey::SideIndex>, Caster<ItemKey::SideIndex>>,
    Gt<Item<ItemKey::ItemIndex>, Caster<ItemKey::ItemIndex>>
>;

constexpr Formula InFlight = Eq<Item<ItemKey::InFlight>, Constant<1>>;
constexpr Formula NotInFlight = Not<InFlight>;

template<Formula condition>
constexpr Formula Count = [](const BattleContext& ctx) -> int {
    return ctx.CountItems(condition);
};

template<Formula condition>
constexpr Formula Only = And<
    condition,
    Eq<Count<condition>, Constant<1>>
>;

template<Formula condition>
constexpr Formula Leftmost = [](const BattleContext& ctx) -> int {
    return ctx.IsLeftmostWith(condition);
};

template<Formula condition>
constexpr Formula Rightmost = [](const BattleContext& ctx) -> int {
    return ctx.IsRightmostWith(condition);
};

// 触发扫描时 ctx.item 为 source（如施放 Slow/Haste 的物品），能力所属物为 ctx.caster。
template<int quest_index>
constexpr Formula QuestComplete = [](const BattleContext& ctx) -> int {
    return (Caster<ItemKey::Quest>(ctx) & (1 << (quest_index - 1))) != 0 ? 1 : 0;
};

}