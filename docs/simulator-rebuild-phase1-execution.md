# Simulator 重构第1阶段执行细化（Core + BattleSimulator）

本文用于落实 `.cursor/rules/simulator-rebuild-roadmap.mdc`，覆盖：
- 与现有规则的优先级对齐；
- 第1阶段人工 Code Review 门禁；
- 可执行到文件级的任务拆分。

## 1. 与现有规则对齐（冲突处理）

### 1.1 优先级顺序

1. `.cursor/rules/no-workarounds.mdc`  
2. `.cursor/rules/battle-simulator-ability-queue.mdc`  
3. `.cursor/rules/simulator-rebuild-roadmap.mdc`  
4. 其他规则（如 `project-conventions.mdc`、`csharp-standards.mdc`）

### 1.2 对齐说明

- `project-conventions.mdc` 要求的战斗语义（队列、触发、条件上下文）继续保持，不因性能改造改变。
- `battle-simulator-ability-queue.mdc` 的帧边界与优先级顺序视为“不可破坏契约”。
- 第1阶段“允许破坏 API”仅指 **对外调用层**（CLI/GUI/Greedy），不代表允许破坏对战行为语义。
- 日志链路在第1阶段可最小化，但不能以“忽略逻辑”换取通过。

## 2. 第1阶段人工 Code Review 清单（性能 + 安全）

每次提交至少逐项检查：

1. 热路径是否出现动态分配（`new`、隐式扩容、`ToList`、闭包捕获）。
2. 是否引入 LINQ/反射/字符串拼接到主循环。
3. unsafe 读写是否仅发生在当前对局独占内存块。
4. 队列容量、索引范围、边界断言是否完整。
5. `UseItem/BattleStart/Ammo/Crit/Freeze/Slow/Destroy/AboutToLose` 触发时机是否与既有规则一致。
6. `current/next` 队列迁移是否仅在规则指定位置发生。
7. PendingCount 合并语义（含 InvokeTarget 分支）是否保持一致。
8. 是否出现按物品名/ID 的临时绕过逻辑。
9. 多线程场景下是否存在共享可写状态。
10. Debug 与 Release 的行为是否仅在断言开关上不同（无语义分叉）。

## 3. 第1阶段文件级任务拆分（可执行）

以下任务只覆盖 `Core + BattleSimulator`，不触及 CLI/GUI/Greedy 适配。

### Task A：建立运行态内存模型（Core）

**建议修改/新增**
- `src/BazaarArena/Core/`（新增运行态结构文件）
  - 建议新增：`BattleRuntimeLayout.cs`
  - 建议新增：`BattleRuntimeState.cs`
  - 建议新增：`BattleRuntimePools.cs`

**目标**
- 定义 side/item/ability 的索引化布局；
- 统一管理固定容量缓冲区；
- 明确每局初始化与复用边界。

**完成标准**
- 运行态结构可独立构建；
- Debug 下容量越界可被断言捕获；
- 无业务语义改动。

### Task B：重建 BuildSide/初始化路径（BattleSimulator）

**建议修改**
- `src/BazaarArena/BattleSimulator/BattleSimulator.cs`
- `src/BazaarArena/BattleSimulator/BattleSimulatorThreadScratch.cs`

**目标**
- 将“模板克隆 + 字典热写”路径收敛到新运行态初始化；
- 统一进入点，避免热路径重复初始化。

**完成标准**
- 单场对战可以完成初始化并进入帧循环；
- 初始化阶段与帧循环阶段职责清晰分离。

### Task C：重写能力队列与触发分发热循环

**建议修改**
- `src/BazaarArena/BattleSimulator/BattleSimulator.cs`
- `src/BazaarArena/BattleSimulator/TriggerInvokeContext.cs`（若存在）
- `src/BazaarArena/BattleSimulator/EffectAppliedTriggerQueue.cs`（若存在）

**目标**
- 以预分配结构承载 current/next 队列；
- 维持既有触发遍历顺序和优先级；
- 移除热路径 LINQ 与对象临时分配。

**完成标准**
- 步骤 7/8/9 语义与规则一致；
- 热路径无显式托管分配。

### Task D：效果应用上下文索引化（进展说明）

**当前代码路径**
- **`src/BazaarArena/Core/BattleContext.cs`** + **`src/BazaarArena/Core/BattleContext.EffectApply.cs`**：**`partial BattleContext`** 承载效果应用；**`ExecuteOneEffect`** 复用 **`BattleContext`**（见 **`BattleSimulatorThreadScratch`**）。
- **`src/BazaarArena/BattleSimulator/BattleAuraModifiers.cs`**：光环累加静态方法（**`Accumulate`**）；与读数路径的完全接线见 **implementation-notes**「光环（Aura）与属性读取」。

**目标（阶段 1 后续）**
- **`GetResolvedValue` / `GetItemInt`** 等热调用进一步索引化、减少分配；
- 保留光环数值结算语义。

**完成标准**
- 关键效果行为不回退；
- 热路径不重复 **`new BattleContext()`**（嵌套外）。

### Task E：构建第1阶段最小验证入口（仅内核）

**建议修改/新增**
- `src/BazaarArena/BattleSimulator/`（可新增最小验证辅助）
- `docs/implementation-notes.md`（记录阶段结果与风险）

**目标**
- 在不依赖 GUI 的情况下验证内核可跑通；
- 输出阶段风险与下一步切换点（CLI 对接前置条件）。

**完成标准**
- 可重复运行的最小验证流程可用；
- 明确记录“已完成/待完成”的语义覆盖范围。

## 4. 第1阶段退出条件（进入 CLI 对接前）

- Core + Simulator 主循环稳定可运行；
- 热路径分配与热点函数采样达标（相对旧版显著下降）；
- 关键触发器语义经人工 review 未发现退化；
- 无特判绕过逻辑。

