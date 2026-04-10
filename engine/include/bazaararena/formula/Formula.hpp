#pragma once

#include <bazaararena/core/BattleContext.hpp>

namespace bazaararena::formula {

using namespace bazaararena::core;

// 公式类型（函数指针）
// 输入 BattleContext，输出 int。条件语义中 0=false, 非0=true。
using Formula = int(*)(const BattleContext&);

// 以下定义常用的公式函数

// 常数
template<int value>
constexpr Formula Constant = [](const BattleContext& ctx) -> int { return value; };

// 特殊常数：真假
constexpr Formula True = Constant<1>;
constexpr Formula False = Constant<0>;

// 当前正在被评估的物品的属性
template<int key>
constexpr Formula Item = [](const BattleContext& ctx) -> int { 
    return ctx.GetItemInt(ctx.item, key); 
};

// 能力/光环释放者物品的属性
// 当 Caster = Item 时，等价于 Item，此时优先使用 Item
template<int key>
constexpr Formula Caster = [](const BattleContext& ctx) -> int { 
    return ctx.GetItemInt(ctx.caster, key); 
};

// 能力/光环释放者所在阵营的属性
template<int key>
constexpr Formula Side = [](const BattleContext& ctx) -> int { 
    return ctx.GetSideInt(key); 
};

// 能力/光环释放者所在阵营的对手阵营的属性
template<int key>
constexpr Formula Opp = [](const BattleContext& ctx) -> int { 
    return ctx.GetOppInt(key); 
};

// 公式的组合计算
template<Formula... formulas>
constexpr Formula And = [](const BattleContext& ctx) -> int { 
    return (formulas(ctx) & ... & 1);
};

template<Formula... formulas>
constexpr Formula Or = [](const BattleContext& ctx) -> int { 
    return (formulas(ctx) | ... | 0);
};

template<Formula... formulas>
constexpr Formula Xor = [](const BattleContext& ctx) -> int { 
    return (formulas(ctx) ^ ... ^ 0);
};

template<Formula a>
constexpr Formula Not = [](const BattleContext& ctx) -> int { 
    return a(ctx) == 0 ? 1 : 0; 
};

template<Formula a, Formula b>
constexpr Formula Add = [](const BattleContext& ctx) -> int { 
    return a(ctx) + b(ctx); 
};

template<Formula a, Formula b>
constexpr Formula Sub = [](const BattleContext& ctx) -> int { 
    return a(ctx) - b(ctx); 
};

template<Formula a, Formula b>
constexpr Formula Mul = [](const BattleContext& ctx) -> int { 
    return a(ctx) * b(ctx); 
};

template<Formula a, Formula b>
constexpr Formula Eq = [](const BattleContext& ctx) -> int { 
    return a(ctx) == b(ctx) ? 1 : 0; 
};

template<Formula a, Formula b>
constexpr Formula Ne = [](const BattleContext& ctx) -> int { 
    return a(ctx) != b(ctx) ? 1 : 0; 
};

template<Formula a, Formula b>
constexpr Formula Lt = [](const BattleContext& ctx) -> int { 
    return a(ctx) < b(ctx) ? 1 : 0; 
};
template<Formula a, Formula b>
constexpr Formula Le = [](const BattleContext& ctx) -> int { 
    return a(ctx) <= b(ctx) ? 1 : 0; 
};

template<Formula a, Formula b>
constexpr Formula Gt = [](const BattleContext& ctx) -> int { 
    return a(ctx) > b(ctx) ? 1 : 0; 
};

template<Formula a, Formula b>
constexpr Formula Ge = [](const BattleContext& ctx) -> int { 
    return a(ctx) >= b(ctx) ? 1 : 0; 
};

} // namespace bazaararena::formula
