#include <algorithm>

#include <bazaararena/core/AbilityQueue.hpp>
#include <bazaararena/core/Simulator.hpp>

namespace bazaararena::core {

// 将一个能力加入到队列
void AbilityQueue::Enqueue(int ability_index, int ability_priority, const BattleContext& ctx, int count) {

    // 完成加入到队列的流程
    entries[queue_size].ability_index = ability_index;
    entries[queue_size].caster = ctx.caster;
    entries[queue_size].source = ctx.source;
    entries[queue_size].target = ctx.target;
    entries[queue_size].count = count;
    
    int trigger_time = std::max(next_legal_trigger_time[ability_index], ctx.simulator->time + Simulator::Frame);
    entries[queue_size].priority = trigger_time << 3 | ability_priority;
    next_legal_trigger_time[ability_index] = trigger_time + AbilityInterval * count;
    
    queue_size++;

    // 调整堆
    std::push_heap(entries.begin(), entries.begin() + queue_size);
}

// 扫描当前帧的队列
void AbilityQueue::Scan(Simulator* simulator, int time) {
    while (queue_size > 0 && (entries[0].priority >> 3) <= time) {
        auto& entry = entries[0];
        simulator->ApplyAbility(entry);
        // 如果剩余触发次数大于 1，则将剩余触发次数减 1，延后 AbilityInterval 秒再触发
        if (entry.count > 1) {
            entry.count--;
            entry.priority += AbilityInterval << 3;
            // 堆顶的优先级发生了变化，需要手动实现 sift down
            int parent = 0;
            int child = 1;
            while (child < queue_size) {
                if (child + 1 < queue_size && entries[child + 1].priority < entries[child].priority) {
                    child++;
                }
                if (entries[child].priority < entries[parent].priority) {
                    std::swap(entries[parent], entries[child]);
                }
                parent = child;
                child = parent * 2 + 1;
            }
        } else {
            // 如果剩余触发次数为 1，则直接从队列中移除
            std::pop_heap(entries.begin(), entries.begin() + queue_size);
            queue_size--;
        }
    }
}

} // namespace bazaararena::core