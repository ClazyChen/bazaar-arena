# QDF（优质卡组探测器）经验整理：ELO、段位与 HillClimb 局部最优

本文汇总排查「活跃玩家段位塌缩」「全员局部最优重启」等问题时的结论与实现约定，便于后续维护与调参。

---

## 1. 现象与根因（按时间线）

### 1.1 赛季后活跃玩家 ELO 全落在最低段

- **采纳新 comboSig**：若池里没有该签名，曾用 `TryGetEntry` 失败则回落到 `InitialElo`，下一季活跃代表全是 1500 段。
- **RestartAnchoredPlayer / ApplyAbandon**：换随机新卡组时曾把新条目写成 `InitialElo`，批量重启会把大量锚定玩家打回底段。
- **修复**：新卡组继承**旧锚定玩家当前 ELO**（或评估得到的 ELO），避免无意义重置。

### 1.2 「放弃重启」数量大于当季活跃代表数

- **原因**：`ApplyAbandon` 遍历**全部锚定玩家**（数百个），与「每季仅约 52 名代表参赛」不是同一集合。
- **语义修正**：放弃阈值改为按**该锚定玩家实际参赛的赛季数**累计（`AnchoredParticipatedSeasonsSinceImproved`），未参赛的赛季不计入；状态持久化版本已递增以保存该字典。

### 1.3 诊断显示「局部最优但 bestDeltaEloSeen > 0」

- **含义**：本次 HillClimb 全程中，至少有一轮评估里出现过 `elo(neighbor) > currentElo`（故 bestDelta > 0），但最终仍被判局部最优并重启。
- **子问题 A（取值不一致）**：`FirstImprovement` 用局部变量 `elo` 判定改进，外层曾再用 `TryGetEntry(nextSig).Elo` 作为 `nextElo`，可能与评估值不一致 → **已修**：返回候选时一并返回用于判定的 `elo`；MAB fallback 用 `lastElo[i]`，不用二次 `TryGetEntry`。
- **子问题 B（语义混用）**：多轮爬山中**已前进**（`steps > 0`）后，仅「下一轮邻居无改进」不应等价于「起点局部最优」并触发重启，否则会**丢掉已改进的卡组** → **已修**：仅当 **`steps == 0`** 时返回 `isLocalOptimum = true` 并写池、触发重启；`steps > 0` 时返回 `false`，由 Runner 采纳当前组合。

### 1.4 两类「重启」日志

- **局部最优重启**：`HillClimb` 返回 `isLocalOptimum` 且 Runner 调用 `RestartAnchoredPlayer`。
- **长期不改进放弃重启**：赛季末 `ApplyAbandon` 满足「参赛赛季计数 ≥ 阈值」的锚定玩家数。

---

## 2. 配置与命令行（调参与诊断）

| 项 | 说明 | 默认 |
|----|------|------|
| `MinNoImproveRoundsForLocalOptimum` | 连续多少轮「本批邻居无改进」才在**起点**判局部最优（`steps==0` 路径） | 1 |
| `--min-no-improve-rounds N` | 同上，N≥1 | |
| `--hill-diag` | 每季输出 HillClimb 汇总：runs、找到改进数、stop 分布、邻居采样/评估均值、bestDelta；若「局部最优且 bestDelta>0」会提示疑似判断问题 | 关 |

快速诊断示例：

```bash
dotnet run --project src/BazaarArena.QualityDeckFinder/BazaarArena.QualityDeckFinder.csproj -c Debug -- --max-seasons 2 --hill-diag
```

---

## 3. 实现要点（代码位置）

| 主题 | 文件 |
|------|------|
| 赛季循环、参赛计数、重启汇总、放弃 | `Runner.cs` |
| HillClimb：nextElo 同源、`steps==0` 才真局部最优 | `HillClimb.cs` |
| 采纳新 sig、重启/放弃继承 ELO | `Runner.cs` |
| 放弃按参赛赛季计数 | `OptimizerState.cs`、`StatePersistence.cs`（v8+） |
| 新参数解析 | `Config.cs` |

---

## 4. 调参建议

- 怀疑「误判局部最优」：先开 `--hill-diag`，看 **找到改进** 与 **局部最优但 bestDelta>0** 比例；修代码后后者应接近 0。
- 若仍希望减少「一轮噪声就停」：可把 `--min-no-improve-rounds` 调到 3～5（仅影响 **`steps==0`** 仍找不到改进时的判定，不改变「已前进则保留改进」逻辑）。
- 缩短单季耗时做回归：减小 `--season-match-cap`、`--games-per-eval`、`--max-climb-steps`、`--neighbor-sample`、`--mab-budget` 等；放弃测试可调低 `--abandon-threshold` 并理解会放大放弃重启次数。

---

## 5. 历史记录（早期观测）

以下为修复前的一次观测，保留作对照：

- 赛季 1：`runs=52`，`stop` 全为 `NoImprovementInSample`，同时 `bestDeltaEloSeen avg≈+21`、`max≈+283`，与「全员局部最优」并存，指向 **nextElo 二次取值** 与 **steps>0 仍判局部最优** 两类问题。
