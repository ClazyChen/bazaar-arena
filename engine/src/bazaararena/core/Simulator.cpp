#include "bazaararena/core/SideKey.hpp"
#include <bazaararena/core/Simulator.hpp>
#include <bazaararena/core/DerivedTag.hpp>
#include <bazaararena/formula/Percent.hpp>
#include <bazaararena/core/AbilityApply.hpp>

#include <bit>

namespace bazaararena::core {

// 调用触发器并执行相关效果
void Simulator::InvokeTrigger(int trigger, const ItemState* source, const ItemState* target, int count) { 
    auto trigger_bitmap = ability_bitmap[trigger];
    BattleContext ctx = { this, source, source, source, target };
    while (trigger_bitmap != 0) {
        int index = static_cast<int>(std::countr_zero(trigger_bitmap));
        trigger_bitmap &= ~(1u << index);
        auto side_index = index >> 4;
        auto item_index = index & 0x0F;
        auto& ability_caster = sides[side_index].items[item_index];
        if (ability_caster.attrs[ItemKey::Destroyed] == 1) continue;
        ctx.item = ctx.caster = &ability_caster;
        for (int i = 0; i < ability_caster.templ->ability_count; i++) {
            auto& ability = ability_caster.templ->abilities[i];
            for (int j = 0; j < ability.trigger_entry_count; j++) {
                if (ability.trigger_entries[j].trigger != trigger) continue;
                if (ability.trigger_entries[j].condition(ctx) == 0) continue;
                if (ability.priority == AbilityPriority::Immediate) {
                    for (int k = 0; k < count; k++) {
                        ApplyAbility({ 0, 1, 0, ctx.caster, ctx.source, ctx.target });
                    }
                } else {
                    ability_queue.Enqueue((index << 3) | i, ability.priority, ctx, count);
                }
                break;
            }
        }
    }
}

// 判断某个物品是否充能完成，如果充能完成且满足施放条件则加入施放队列
void Simulator::CheckCharge(ItemState& item, bool ignore_charge_remaining) {
    if (item.attrs[ItemKey::Destroyed] == 1) return; // 被摧毁的物品无法施放
    if (item.attrs[ItemKey::FreezeRemaining] > 0) return; // 被冻结的物品无法施放
    int cooldown = GetItemInt(&item, ItemKey::Cooldown);
    if (ignore_charge_remaining || item.attrs[ItemKey::ChargedTime] >= cooldown) { // 充能完成
        if (item.attrs[ItemKey::DerivedTags] & DerivedTag::Ammo) {
            // 弹药物品充能完成
            if (item.attrs[ItemKey::AmmoRemaining] == 0) {
                // 充能完成但是没有弹药，不满足施放条件，不进入施放队列
                return;
            } else {
                // 充能完成且有弹药，立刻消耗 1 枚弹药
                item.attrs[ItemKey::AmmoRemaining]--;
                // 触发「弹药」触发器
                InvokeTrigger(Trigger::Ammo, &item, &item, 1);
            }
        }
        if (!ignore_charge_remaining) { // 如果忽略充能剩余时间，则不消耗充能
            // 正常充能完成，消耗所有充能，并触发施放
            item.attrs[ItemKey::ChargedTime] = 0;
        }
        int multicast = GetItemInt(&item, ItemKey::Multicast);
        // 触发「施放」触发器（主动效果）
        InvokeTrigger(Trigger::Cast, &item, &item, multicast);
        // 触发「使用物品」触发器（被动效果）
        InvokeTrigger(Trigger::UseItem, &item, &item, multicast);
    }
}

// 执行一次模拟，返回胜方（0 或 1）或 -1 表示平局
int Simulator::Run(bool allow_tie) {
    // 进行每一帧的模拟
    while (time < sandstorm.End) {

        // 1. 触发「战斗开始」和「每帧触发」
        if (time == 0) {
            InvokeTrigger(Trigger::BattleStart, nullptr, nullptr, 1);
        }
        InvokeTrigger(Trigger::EveryFrame, nullptr, nullptr, 1);

        // 2. 处理冷却时间，充能完成则加入施放队列
        for (int i = 0; i < SideCount; i++) {
            for (int j = 0; j < sides[i].attrs[SideKey::ItemCount]; j++) {
                auto& item = sides[i].items[j];
                if (!(item.attrs[ItemKey::DerivedTags] & DerivedTag::Cooldown)) continue; // 无冷却时间的物品，不处理
                if (item.attrs[ItemKey::Destroyed] == 1) continue; // 被摧毁的物品，不处理
                if (item.attrs[ItemKey::FreezeRemaining] > 0) continue; // 被冻结的物品，不处理
                int advance = Frame; // 每帧充能时间
                if (item.attrs[ItemKey::HasteRemaining] > 0) advance *= 2; // 加速时充能时间翻倍
                if (item.attrs[ItemKey::SlowRemaining] > 0) advance /= 2; // 减速时充能时间减半
                int cooldown = GetItemInt(&item, ItemKey::Cooldown);
                advance = std::min(advance, std::max(1, cooldown / 20)); // 充能时间不能超过冷却时间的 1/20
                item.attrs[ItemKey::ChargedTime] += advance;
                CheckCharge(item);
            }
        }

        // 3. 加速、减速、冻结的剩余时间减少一帧
        for (int i = 0; i < SideCount; i++) {
            for (int j = 0; j < sides[i].attrs[SideKey::ItemCount]; j++) {
                auto& item = sides[i].items[j];
                if (item.attrs[ItemKey::HasteRemaining] > 0) item.attrs[ItemKey::HasteRemaining] = std::max(0, item.attrs[ItemKey::HasteRemaining] - Frame);
                if (item.attrs[ItemKey::SlowRemaining] > 0) item.attrs[ItemKey::SlowRemaining] = std::max(0, item.attrs[ItemKey::SlowRemaining] - Frame);
                if (item.attrs[ItemKey::FreezeRemaining] > 0) item.attrs[ItemKey::FreezeRemaining] = std::max(0, item.attrs[ItemKey::FreezeRemaining] - Frame);
            }
        }

        // 4. 处理剧毒
        if (time % PoisonTickInterval == 0) {
            for (int i = 0; i < SideCount; i++) {
                if (sides[i].attrs[SideKey::Poison] > 0) {
                    // 如果有剧毒，则造成削减生命
                    sides[i].ApplyDamage(sides[i].attrs[SideKey::Poison], false, true);
                }
            }
        }

        // 5. 处理灼烧
        if (time % BurnTickInterval == 0) {
            for (int i = 0; i < SideCount; i++) {
                if (sides[i].attrs[SideKey::Burn] > 0) {
                    // 如果有灼烧，则造成削减生命（会受到护盾影响）
                    sides[i].ApplyDamage(sides[i].attrs[SideKey::Burn], true, false);
                    // 灼烧衰减 3%
                    int decay = formula::PercentFloor(sides[i].attrs[SideKey::Burn], 3);
                    sides[i].attrs[SideKey::Burn] = std::max(0, sides[i].attrs[SideKey::Burn] - decay);
                }
            }
        }

        // 6. 处理生命再生
        if (time % RegenTickInterval == 0) {
            for (int i = 0; i < SideCount; i++) {
                if (sides[i].attrs[SideKey::Regen] > 0) {
                    // 如果有生命再生，则治疗生命
                    sides[i].ApplyHeal(sides[i].attrs[SideKey::Regen]);
                }
            }
        }

        // 7. 处理沙尘暴
        if (time >= sandstorm.next_tick) {
            for (int i = 0; i < SideCount; i++) {
                sides[i].ApplyDamage(sandstorm.damage, false, false);
            }
            // 沙尘暴的间隔递减或伤害递增
            if (sandstorm.interval > sandstorm.MinInterval) {
                sandstorm.interval -= sandstorm.IntervalDecrease;
            } else {
                sandstorm.damage += sandstorm.DamageIncrease;
            }
            sandstorm.next_tick += sandstorm.interval;
        }

        // 8. 处理能力队列
        ability_queue.Scan(this, time);

        // 9. 检查胜负
        bool dead0 = sides[0].attrs[SideKey::Hp] <= 0;
        bool dead1 = sides[1].attrs[SideKey::Hp] <= 0;
        if (dead0 || dead1) { // 即将落败，会触发救生圈等保命道具效果
            InvokeTrigger(Trigger::AboutToLose, nullptr, nullptr, 1);
        }

        // 触发效果后再次检查胜负
        dead0 = sides[0].attrs[SideKey::Hp] <= 0;
        dead1 = sides[1].attrs[SideKey::Hp] <= 0;
        
        if (dead0 && dead1) {
            if (allow_tie) {
                return -1;
            } else {
                // 双方生命均 <= 0，根据双方剩余生命值决定胜负
                int hp0 = sides[0].attrs[SideKey::Hp];
                int hp1 = sides[1].attrs[SideKey::Hp];
                if (hp0 > hp1) {
                    return 0;
                } else if (hp1 > hp0) {
                    return 1;
                } else {
                    return rng.Next(2);
                }
            }
        } else if (dead0) {
            return 1;
        } else if (dead1) {
            return 0;
        }

        // 10. 清理本帧的暴击状态
        crit_bitmap = 0;
        crit_checked_bitmap = 0;

        time += Frame;
    }

    // 时间结束时战斗未结束
    if (allow_tie) {
        return -1;
    } else {
        // 时间结束时战斗未结束，根据双方剩余生命和护盾的和决定胜负
        int hp_shield_0 = sides[0].attrs[SideKey::Hp] + sides[0].attrs[SideKey::Shield];
        int hp_shield_1 = sides[1].attrs[SideKey::Hp] + sides[1].attrs[SideKey::Shield];
        if (hp_shield_0 > hp_shield_1) {
            return 0;
        } else if (hp_shield_1 > hp_shield_0) {
            return 1;
        } else {
            return rng.Next(2);
        }
    }
}

int Simulator::GetItemInt(const ItemState* item, int key) const {
    BattleContext ctx = { this, item, item, item, item };
    return ctx.GetItemInt(item, key);
}

// 应用某个能力的效果
void Simulator::ApplyAbility(const AbilityQueue::Entry& entry) {
    int index = entry.ability_index;
    auto side_index = index >> 7;
    auto item_index = (index >> 3) & 0x0F;
    auto ability_index = index & 0x07;
    auto& item = sides[side_index].items[item_index];
    auto& ability = item.templ->abilities[ability_index];
    BattleContext ctx = { this, &item, &item, entry.source, entry.target };
    AbilityApplyTable[ability.type](ability, ctx);
}

} // namespace bazaararena::core