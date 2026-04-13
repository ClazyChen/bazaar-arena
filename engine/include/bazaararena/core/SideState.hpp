#pragma  once

#include <bazaararena/core/ItemState.hpp>
#include <bazaararena/core/SideKey.hpp>

namespace bazaararena::core {

class SideState {
public:
    static constexpr int MaxItems = 10; // 卡组中的最大物品数量

    std::array<ItemState, MaxItems> items; // 物品列表（静态布局，固定数量）
    std::array<int, SideKey::Count> attrs; // 阵营状态属性

    // 直接伤害、剧毒和灼烧的统一入口；effective_resistance_pct 为槽位 0 物品抗性（含光环），由 Simulator 计算后传入
    void ApplyDamage(int damage, bool is_burn = false, bool is_poison = false, int effective_resistance_pct = 0);

    // 治疗生命，不能超过最大生命值
    void ApplyHeal(int heal);
};

}