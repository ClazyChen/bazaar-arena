---
name: greedy-deck-finder
description: 修改或扩展 GreedyDeckFinder（锚定贪心搜索）的玩家等级、物品池、战斗档位或 Python 批跑脚本时使用。
---

# GreedyDeckFinder 技能

## 何时使用

- 调整 `--level` / `playerLevel` 合法范围、物品池 `MinTier` 过滤、战斗扁平化档位、Overridable 阶梯。
- 修改 `run_greedy_vanessa_bronze_top1.py` 或同类脚本，使其与 C# 行为一致。

## 必读

1. **`GreedyLevelRules.cs`**（`BazaarArena.GreedyDeckFinder`）：池子门槛、`CombatTier`、`ComputeOverridableValue` 的**唯一实现源**。
2. **勿与 GUI 混用**：`Deck.TierAllowedForLevel`（银 3 / 金 7 / 钻 10）≠ Greedy 的池子/档位规则。
3. **文档**：`docs/greedy-deck-finder.md`（参数与规则表）、`docs/greedy-deck-finder-performance-notes.md`（性能与原型语义）、`docs/implementation-notes.md`「GreedyDeckFinder 玩家等级」。
4. **Cursor 规则**：`.cursor/rules/greedy-deck-finder.mdc`（并行、随机、`GreedyLevelRules` 摘要）。

## 修改后建议

- 冒烟：`dotnet run --project src/BazaarArena.GreedyDeckFinder/BazaarArena.GreedyDeckFinder.csproj -- --level 11 --anchor-item <池内物品> --top-k 1`（高等级含钻池）。
- 若改了脚本：`python scripts/run_greedy_vanessa_bronze_top1.py`（或其中单测逻辑）。
