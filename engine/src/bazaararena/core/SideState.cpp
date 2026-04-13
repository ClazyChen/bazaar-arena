#include <bazaararena/core/SideState.hpp>
#include <bazaararena/formula/Percent.hpp>

namespace bazaararena::core {

void SideState::ApplyDamage(int damage, bool is_burn, bool is_poison, int effective_resistance_pct) {
    // 处理抗性百分比（数值存于槽位 0 的 ItemKey::Resistance，此处用已解析的有效百分比）
    if (effective_resistance_pct > 0) {
        damage = std::max(0, damage - formula::PercentFloor(damage, effective_resistance_pct));
    }

    // 处理护盾
    if (is_burn) {
        int burn_shield_consume = std::min(attrs[SideKey::Shield], damage / 2);
        // 格挡量：pay*2+1（pay>0 时），奇数灼烧可与 floor(灼烧/2) 点护盾对齐而不漏 1 血
        int burn_shield_damage = burn_shield_consume * 2;
        if (burn_shield_consume > 0) {
            burn_shield_damage += 1;
        }
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