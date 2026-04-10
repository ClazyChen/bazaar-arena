#pragma once

namespace bazaararena::core {

// 前置声明
class Simulator;
class ItemState;

// 战斗上下文类（轻量的上下文，用于传递给触发器和效果应用函数）
class BattleContext {
public:
    const Simulator* simulator; // 模拟器指针
    const ItemState* item; // 当前物品的指针
    const ItemState* caster; // 能力/光环释放者物品的指针，大部分情况下与 item 一致，仅在“选择目标”的情况下，“当前物品”变为可被选择的目标物品，仅在“光环”判定中，“当前物品”变为承受光环的目标物品
    const ItemState* source; // 引起当前触发的那件物品（如 UseItem 的被使用物、Slow 的施放者）；无单独“原因”时与 caster 一致（如战斗开始）
    const ItemState* target; // 当前触发器的目标物品（如 UseItem 的被使用物、Slow 的减速目标）；无单独“目标”时与 source 一致（如触发灼烧、剧毒）

    int GetItemInt(const ItemState* item, int key) const; // 读取物品的某个属性（会受到光环的影响）
    int GetItemIntRaw(const ItemState* item, int key) const; // 读取物品的某个属性（不受光环的影响）

    int GetSideInt(int key) const; // 读取能力/光环释放者所在阵营的某个属性
    int GetOppInt(int key) const; // 读取能力/光环释放者所在阵营的对手阵营的某个属性

};

}