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

constexpr Formula SameSide = Eq<Item<ItemKey::SideIndex>, Caster<ItemKey::SideIndex>>;
constexpr Formula DifferentSide = Not<SameSide>;

constexpr Formula Destroyed = Eq<Item<ItemKey::Destroyed>, Constant<1>>;
constexpr Formula NotDestroyed = Ne<Item<ItemKey::Destroyed>, Constant<1>>;

template<int tag>
constexpr Formula HasTag = And<Item<ItemKey::Tags>, Constant<tag>>;

template<int tag>
constexpr Formula HasDerivedTag = And<Item<ItemKey::DerivedTags>, Constant<tag>>;

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

constexpr Formula InFlight = Eq<Item<ItemKey::InFlight>, Constant<1>>;
constexpr Formula NotInFlight = Not<InFlight>;

}