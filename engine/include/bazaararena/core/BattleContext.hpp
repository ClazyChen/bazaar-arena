#pragma once

namespace bazaararena::core {

// 前置声明
class Simulator;
class ItemState;

// 战斗上下文类（轻量的上下文，用于传递给触发器和效果应用函数）
//
// 字段语义按使用场景约定如下（公式里 Item<> 读 ctx.item，Caster<> 读 ctx.caster）：
//
// 1) 触发条件（ability.trigger_entries[].condition，在 Simulator::InvokeTrigger 扫描能力时）
//    - source / target：由 InvokeTrigger(trigger, source, target) 固定传入。
//    - 当 source != nullptr：item 与 source 保持一致；caster 为当前正被判断「其能力是否响应本次触发」的那件物品（能力所属物）。
//    - 当 source == nullptr（BattleStart / EveryFrame / AboutToLose 等）：在扫描循环内将 item 与 caster 均设为当前能力所属物品，以便仅依赖 Item 的条件仍可评估。
//
// 2) 目标条件（ability.target_condition 与 GetTargets 内的 force_condition）
//    - source / target：沿用进入 GetTargets 时的触发链信息（通常来自队列条目或上层 ctx）。
//    - caster：固定为「正在施放该效果、且提供 target_count_key 等」的能力拥有者。
//    - item：在双层循环中依次指向每个候选槽位物品，用于判断该候选是否满足强制条件与 target_condition。
//    口头描述「正在判断是否被选为目标的一方」对应公式中的 Item（候选）；Caster 为参照轴（施放者），例如 SameSide、AdjacentToCaster 等即在此约定下编写。
//
// 3) 光环条件（BattleContext::GetItemInt 扫描光环时）
//    - 读取某件物品属性时，外层上下文中 item / source / target 均指向该被读属性主体（见 Simulator::GetItemInt 的构造）。
//    - 扫描各光环源时，仅临时将 ctx.caster 置为光环来源物品，item 仍指向被读属性主体，用于 aura.condition / aura.value。
class BattleContext {
public:
    const Simulator* simulator; // 模拟器指针
    const ItemState* item; // 见类注释：触发扫描时多与 source 一致；选目标时为候选物品；读属性时为被读主体
    const ItemState* caster; // 见类注释：触发扫描时为当前能力所属物；选目标时为能力拥有者；光环扫描时为光环来源
    const ItemState* source; // 引起当前触发链的物品（如 Cast/UseItem 的施放者）；全局触发时可为 nullptr
    const ItemState* target; // 触发链中的目标物品（如 Freeze 的承受方）；无单独目标时多与 source 一致，亦可为 nullptr

    int GetItemInt(const ItemState* item, int key) const; // 读取物品的某个属性（会受到光环的影响）
    int GetItemIntRaw(const ItemState* item, int key) const; // 读取物品的某个属性（不受光环的影响）

    int GetSideInt(int key) const; // 读取能力/光环释放者所在阵营的某个属性
    int GetOppInt(int key) const; // 读取能力/光环释放者所在阵营的对手阵营的某个属性

    // 公式类型（函数指针），和 formula::Formula 一致
    using Formula = int(*)(const BattleContext&);

    // 计算满足某个条件的物品数量
    int CountItems(Formula condition) const;

    // 满足某个条件的最左侧的物品
    int IsLeftmostWith(Formula condition) const;

    // 满足某个条件的最右侧的物品
    int IsRightmostWith(Formula condition) const;

};

}