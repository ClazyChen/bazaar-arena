#pragma once

// Sink 仅负责收集调试输出（summary 文本行 / detailed JSON 事件）
#include <string>
#include <vector>

#include <bazaararena/io/JsonLite.hpp>

// 前向声明
namespace bazaararena::core {

class Simulator;
class ItemState;

}

namespace bazaararena::io {

// 用于战斗日志的管理和输出
class Sink {
public:
    static constexpr int TypeNone = 0;
    static constexpr int TypeSummary = 1;   // debug.level=summary：收集纯文本行
    static constexpr int TypeDetailed = 2;  // debug.level=detailed：收集 JSON 事件

    // 日志类型
    // 0: 静默：不输出任何内容（默认；利用分支预测降低开销，会比嵌入 if constexpr 慢一些，但实现简单）
    // 1. 简洁：输出可读形式
    // 2. 详细：输出 JSON 形式
    int sink_type = TypeNone;

    // 事件/行的最大收集数量（对应 debug.maxEvents）。达到上限后 truncated=true，后续丢弃。
    int max_events = 20000;
    bool truncated = false;

    // debug.level=summary
    std::vector<std::string> lines;
    // debug.level=detailed
    JsonArray events;

    void Clear() {
        truncated = false;
        lines.clear();
        events.clear();
    }

    // 每帧结束时调用
    // 1 - 不进行任何输出
    // 2 - 输出时间、双方当前生命值、生命上限、灼烧、剧毒、生命再生、抗性、以及每个物品的以下属性：
    //     Damage/Burn/Poison/Regen/Shield/Heal/Value/Multicast/AmmoRemaining/AmmoCap/InFlight/Destroyed/Cooldown/ChargedTime/FreezeRemaining/SlowRemaining/HasteRemaining/Value/LifeSteal
    //     用于完整显示战斗过程
    void OnFrameEnd(const core::Simulator& simulator);

    // 造成伤害时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 造成 10 伤害"
    //     (is_crit = true 时："[3.00s] 玩家1 [獠牙] 造成 10 伤害（暴击）")
    //     (is_life_steal = true 时："[3.00s] 玩家1 [獠牙] 吸血 10 伤害")
    // 2 - 输出时间、伤害目标（sideindex）、伤害量（damage）、是否暴击（is_crit）（只需要显示动画，伤害数字）
    void OnDamage(const core::Simulator& simulator, const core::ItemState& source, int damage, bool is_crit, bool is_life_steal);

    // 造成灼烧时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 造成 10 灼烧"
    // 2 - 输出时间、灼烧目标（sideindex）、灼烧量（burn）、是否暴击（is_crit）（只需要显示动画，灼烧数字）
    void OnBurn(const core::Simulator& simulator, const core::ItemState& source, int burn, bool is_crit);

    // 造成剧毒时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 造成 10 剧毒"
    //     (is_self_poison = true 时："[3.00s] 玩家1 [獠牙] 造成 10 自我剧毒")
    // 2 - 输出时间、剧毒目标（sideindex）、剧毒量（poison）、是否暴击（is_crit）（只需要显示动画，剧毒数字）
    void OnPoison(const core::Simulator& simulator, const core::ItemState& source, int poison, bool is_crit, bool is_self_poison);

    // 造成生命再生时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 造成 10 生命再生"
    // 2 - 输出时间、生命再生目标（sideindex）、生命再生量（regen）、是否暴击（is_crit）（只需要显示动画，生命再生数字）
    void OnRegen(const core::Simulator& simulator, const core::ItemState& source, int regen, bool is_crit);

    // 造成护盾时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 造成 10 护盾"
    // 2 - 输出时间、护盾目标（sideindex）、护盾量（shield）、是否暴击（is_crit）（只需要显示动画，护盾数字）
    void OnShield(const core::Simulator& simulator, const core::ItemState& source, int shield, bool is_crit);

    // 造成治疗时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 造成 10 治疗"
    // 2 - 输出时间、治疗目标（sideindex）、治疗量（heal）、是否暴击（is_crit）（只需要显示动画，治疗数字）
    void OnHeal(const core::Simulator& simulator, const core::ItemState& source, int heal, bool is_crit);

    // 造成充能时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 充能 1 秒 → [物品1, 物品2]"
    // 2 - 不进行输出（充能效果会在 OnFrameEnd 中显示）
    void OnCharge(const core::Simulator& simulator, const core::ItemState& source, int target_count,int charge);

    // 造成加速时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 加速 2 秒 → [物品1, 物品2]"
    // 2 - 不进行输出（加速效果会在 OnFrameEnd 中显示）
    void OnHaste(const core::Simulator& simulator, const core::ItemState& source, int target_count, int haste);

    // 造成减速时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 减速 2 秒 → [物品1, 物品2]"
    // 2 - 不进行输出（减速效果会在 OnFrameEnd 中显示）
    void OnSlow(const core::Simulator& simulator, const core::ItemState& source, int target_count, int slow);

    // 造成冻结时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 冻结 2 秒 → [物品1, 物品2]"
    // 2 - 不进行输出（冻结效果会在 OnFrameEnd 中显示）
    void OnFreeze(const core::Simulator& simulator, const core::ItemState& source, int target_count, int freeze);

    // 造成装填时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 装填 1 弹药 → [物品1, 物品2]"
    //     (reload = 99 即全装填时："[3.00s] 玩家1 [獠牙] 装填 → [物品1, 物品2]")
    // 2 - 不进行输出（装填效果会在 OnFrameEnd 中显示）
    void OnReload(const core::Simulator& simulator, const core::ItemState& source, int target_count, int reload);

    // 造成摧毁时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 摧毁 → [物品1, 物品2]"
    // 2 - 不进行输出（摧毁效果会在 OnFrameEnd 中显示）
    void OnDestroy(const core::Simulator& simulator, const core::ItemState& source, int target_count);

    // 造成修复时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 修复 → [物品1, 物品2]"
    // 2 - 不进行输出（修复效果会在 OnFrameEnd 中显示）
    void OnRepair(const core::Simulator& simulator, const core::ItemState& source, int target_count);

    // 造成属性提高时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 伤害提高 10 → [物品1, 物品2]"
    // 2 - 不进行输出（属性提高效果会在 OnFrameEnd 中显示）
    void OnAttributeIncrease(const core::Simulator& simulator, const core::ItemState& source, int target_count, int attribute, int value);

    // 造成属性降低时调用
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 伤害降低 10 → [物品1, 物品2]"
    // 2 - 不进行输出（属性降低效果会在 OnFrameEnd 中显示）
    void OnAttributeDecrease(const core::Simulator& simulator, const core::ItemState& source, int target_count, int attribute, int value);

    // 造成抗性改变时调用
    // 仅输出是否无敌（抗性 >= 100），不输出具体抗性值
    // 1 - e.g. "[3.00s] 玩家1 [獠牙] 无敌" （抗性变为至少 100 时）
    //   - e.g. "[3.00s] 玩家1 解除无敌" （抗性降低到少于 100 时）
    // 2 - 不进行输出（抗性改变效果会在 OnFrameEnd 中显示）
    void OnResistance(const core::Simulator& simulator, const core::ItemState& source, int old_resistance, int delta_resistance);

    // Tick: 来自 side 状态的周期结算（无来源物品）
    // summary: e.g. "[3.00s] 玩家1 受到灼烧 2"
    // detailed: 不输出
    void OnBurnTick(const core::Simulator& simulator, int side_index, int burn);

    // summary: e.g. "[3.00s] 玩家1 受到剧毒 2"
    // detailed: 不输出
    void OnPoisonTick(const core::Simulator& simulator, int side_index, int poison);

    // summary: e.g. "[3.00s] 玩家1 受到生命再生 2"
    // detailed: 不输出
    void OnRegenTick(const core::Simulator& simulator, int side_index, int regen);

    // summary: e.g. "[3.00s] 玩家1 受到沙尘暴 2"
    // detailed: 输出（格式与 OnDamage 相同）
    void OnSandstormTick(const core::Simulator& simulator, int side_index, int damage);

    // 游戏结束时调用
    // 1 - e.g. "[3.00s] 玩家1 获胜"
    //     (is_draw = true 时："[3.00s] 平局")
    // 2 - 输出时间、结果、获胜方，并保存最终状态
    // winner: 0/1 表示获胜方；-1 表示平局（与 Simulator::Run 返回一致）
    void OnGameEnd(const core::Simulator& simulator, int winner);
};


}