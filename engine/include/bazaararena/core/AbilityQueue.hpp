#pragma once

#include <array>

#include <bazaararena/literals/duration.hpp>

namespace bazaararena::core {

// 前置声明
class Simulator;
class ItemState;
class BattleContext;

// 能力队列，用于存储等待释放的能力
class AbilityQueue {
public:
    static constexpr int AbilityInterval = 0.25_s; // 同一能力连续触发的最小间隔
    static constexpr int MaxAbilityCount = 256; // 能力 ID，最大 256 个，格式为 (side_index << 7) | (item_index << 3) | ability_index
    static constexpr int MaxQueueSize = 4096; // 整个能力队列的最大容量（静态分配），实际上应该只会使用最前的一部分，一局模拟不可能超过这个数量

    // 能力队列的条目，用于存储等待释放的能力，以及需要使用的上下文和剩余触发次数
    // 这里一个条目占用的内存大小应该是 32 字节
    struct Entry {
        short int ability_index = 0; // 能力索引
        short int count = 0; // 剩余触发次数
        int priority = 0; // 优先级（next_trigger_time << 3 | ability_priority）
        const ItemState* caster = nullptr; // 能力释放者
        const ItemState* source = nullptr; // 引起能力触发的那件物品
        const ItemState* target = nullptr; // 引起能力触发的触发器中的目标物品

        // 重载 < 运算符，用于优先级队列的比较（这里需要实现小顶堆，所以要反过来）
        bool operator<(const Entry& other) const {
            return priority > other.priority;
        }
    };

    // 整个能力队列的实际大小，初始化时只需要重置为 0 即可
    int queue_size = 0;

    // 整个能力队列的实际内容（优先级队列），这里一个条目占用的内存大小应该是 32 字节
    std::array<Entry, MaxQueueSize> entries;

    // 每个能力，下一次可以合法触发的时间
    // next_legal_trigger_time[ability_index] 表示第 ability_index 个能力下一次可以合法触发的时间
    // 初始均为 0 表示可以立即触发
    std::array<int, MaxAbilityCount> next_legal_trigger_time;

    // 将一个能力加入到队列
    void Enqueue(int ability_index, int ability_priority, const BattleContext& ctx, int count);

    // 扫描当前帧的队列并执行全部可以触发的能力
    void Scan(Simulator* simulator, int time);

};

} // namespace bazaararena::core