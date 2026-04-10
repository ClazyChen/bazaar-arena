#include <bazaararena/core/SideState.hpp>
#include <bazaararena/formula/Percent.hpp>

namespace bazaararena::core {

void SideState::ApplyDamage(int damage, bool is_burn, bool is_poison) {
    
    // 处理抗性百分比
    if (attrs[SideKey::Resistance] > 0) {
        damage = std::max(0, damage - formula::PercentFloor(damage, attrs[SideKey::Resistance]));
    }

    // 处理护盾
    if (is_burn) {
        int burn_shield_consume = std::min(attrs[SideKey::Shield], damage / 2);
        int burn_shield_damage = burn_shield_consume * 2;
        attrs[SideKey::Shield] -= burn_shield_consume;
        damage = std::max(0, damage - burn_shield_damage);
    } else if (!is_poison) {
        int shield_consume = std::min(attrs[SideKey::Shield], damage);
        attrs[SideKey::Shield] -= shield_consume;
        damage = std::max(0, damage - shield_consume);
    } else {
        /* 剧毒：护盾不阻挡，直接作用于生命 */
    }

    attrs[SideKey::Hp] -= damage;
}

void SideState::ApplyHeal(int heal) {
    attrs[SideKey::Hp] = std::min(attrs[SideKey::Hp] + heal, attrs[SideKey::MaxHp]);
}

}