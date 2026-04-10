#pragma once

#include <array>

#include <bazaararena/core/Random.hpp>
#include <bazaararena/literals/duration.hpp>
#include <bazaararena/core/Trigger.hpp>
#include <bazaararena/core/SideState.hpp>
#include <bazaararena/core/AbilityQueue.hpp>

namespace bazaararena::core {

// 战斗模拟器类
class Simulator {
public:
    static constexpr int SideCount = 2; // 参加对战的阵营数量
    static constexpr int Frame = 0.05_s; // 每帧的时间间隔
    static constexpr int BurnTickInterval = 0.5_s; // 灼烧的间隔
    static constexpr int PoisonTickInterval = 1_s; // 剧毒的间隔
    static constexpr int RegenTickInterval = 1_s; // 生命再生的间隔

    struct SandStorm final {
        static constexpr int Start = 30_s; // 沙尘暴开始时间
        static constexpr int End = 120_s; // 沙尘暴结束时间
        static constexpr int InitialInterval = 0.3_s; // 沙尘暴的初始间隔
        static constexpr int MinInterval = 0.14_s; // 沙尘暴的最小间隔
        static constexpr int InitialDamage = 1; // 沙尘暴的初始伤害
        static constexpr int DamageIncrease = 2; // 达到最小间隔后，沙尘暴的伤害递增
        static constexpr int IntervalDecrease = 0.02_s; // 达到最小间隔前，每次沙尘暴间隔减少的时间
        
        // 下次沙尘暴触发的时刻
        int next_tick = Start;
        // 沙尘暴的间隔
        int interval = InitialInterval;
        // 沙尘暴的伤害
        int damage = InitialDamage;
    };

    // 沙尘暴状态
    SandStorm sandstorm;

    // 当前已经过去的时间（毫秒）
    int time = 0;

    // 阵营的属性
    std::array<SideState, SideCount> sides;

    // 用来检索能力的表格，以位图形式
    // 每个触发器对应了一个位图，ability_bitmap[trigger] 代表了该触发器对应的位图
    // 位图中的每一位表示一个物品是否具有该触发器下可以触发的能力
    // ability_bitmap[trigger] 的第 ((side_index << 4) | item_index) 位表示物品在 side_index 阵营中，第 item_index 个物品是否具有该触发器下可以触发的能力
    // 由于一个物品可能有多个在该触发器下可以触发的能力，所以在定位到可以触发能力的物品之后，需要遍历其 template 中的能力定义，找到对应的触发器和触发条件
    std::array<unsigned int, Trigger::Count> ability_bitmap;

    // 用来检索光环的表格，以位图形式
    // 每个物品属性对应了一个位图，aura_bitmap[item_key] 代表了该物品属性对应的位图
    // 位图中的每一位表示一个物品的光环是否会影响到该物品属性
    // aura_bitmap[item_key] 的第 ((side_index << 4) | item_index) 位表示物品在 side_index 阵营中，第 item_index 个物品是否具有该物品属性
    std::array<unsigned int, ItemKey::Count> aura_bitmap;

    // 在本帧等待被释放的物品（位图），语义和上述相同
    unsigned int cast_queue;

    // 本帧的物品是否暴击（位图），语义和上述相同
    unsigned int crit_bitmap;

    // 本帧的物品是否已经检查过暴击（位图），语义和上述相同
    unsigned int crit_checked_bitmap;

    // 临时存储的目标物品（用于选择目标）
    std::array<ItemState*, SideCount * SideState::MaxItems> targets;

    // 能力队列
    AbilityQueue ability_queue;

    // 随机数生成器
    Random rng;

    // 判断某个物品是否充能完成，如果充能完成且满足施放条件则加入施放队列
    void CheckCharge(ItemState& item);

    // 调用触发器并执行相关效果
    void InvokeTrigger(int trigger, const ItemState* source, const ItemState* target, int count = 1);

    // 应用某个能力的效果
    void ApplyAbility(const AbilityQueue::Entry& entry);

    // 执行一次模拟，返回胜方（0 或 1）或 -1 表示平局
    // 调用这个函数时，需要保证 sides 已经被初始化，并且 ability_bitmap 和 aura_bitmap 已经被计算出来
    // allow_tie 表示是否允许平局，如果为 false，则：
    // 1. 在双方生命均 <= 0 时，根据双方剩余生命值决定胜负
    // 2. 在战斗时间结束（120s）时，根据双方剩余生命和护盾的和决定胜负
    // 3. 如果仍然平局，则随机决定胜负
    int Run(bool allow_tie = true);

    // 读取指定物品属性（考虑光环影响）
    int GetItemInt(const ItemState* item, int key) const;
};

}