#include <algorithm>

#include <bazaararena/core/AbilityQueue.hpp>
#include <bazaararena/core/Simulator.hpp>

namespace bazaararena::core {

// 将一个能力加入到队列
void AbilityQueue::Enqueue(int ability_index, int ability_priority, const BattleContext& ctx) {

    // 完成加入到队列的流程
    entries[queue_size].ability_index = ability_index;
    entries[queue_size].caster = ctx.caster;
    entries[queue_size].source = ctx.source;
    entries[queue_size].target = ctx.target;
    
    int trigger_time = std::max(next_legal_trigger_time[ability_index], ctx.simulator->time + Simulator::Frame);
    entries[queue_size].priority = trigger_time << 3 | ability_priority;
    next_legal_trigger_time[ability_index] = trigger_time + AbilityInterval;
    
    queue_size++;

    // 调整堆
    std::push_heap(entries.begin(), entries.begin() + queue_size);
}

// 扫描当前帧的队列
void AbilityQueue::Scan(Simulator* simulator, int time) {
    while (queue_size > 0 && (entries[0].priority >> 3) <= time) {
        const auto entry = entries[0];
        simulator->ApplyAbility(entry);
        std::pop_heap(entries.begin(), entries.begin() + queue_size);
        queue_size--;
    }
}

} // namespace bazaararena::core