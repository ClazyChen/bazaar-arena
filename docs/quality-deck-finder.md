# 优质卡组探测器 使用说明

优质卡组探测器（BazaarArena.QualityDeckFinder）是一个控制台工具，在**固定等级 2**、**海盗（Vanessa）最新版铜级物品池**下，通过**虚拟赛季循环**发现优质卡组：每赛季先选锚定代表、再匹配赛、再卡组优化，强度由 ELO 衡量；输出强度玩家 Top10 与抽样物品的锚定最强卡组（含对局数）。

---

## 运行方式

### 从仓库根目录运行（推荐）

```bash
dotnet run --project src/BazaarArena.QualityDeckFinder/BazaarArena.QualityDeckFinder.csproj
```

程序会从当前工作目录或程序所在目录查找 `Data/levelups.json`；项目已配置将该文件复制到输出目录，因此直接 `dotnet run` 即可。

### 先构建再运行

```bash
dotnet build src/BazaarArena.QualityDeckFinder/BazaarArena.QualityDeckFinder.csproj
cd src/BazaarArena.QualityDeckFinder/bin/Debug/net10.0-windows
./BazaarArena.QualityDeckFinder.exe
```

---

## 架构概览：虚拟赛季

主循环为**虚拟赛季**，每赛季步骤：

1. **代表选择**：每物品在锚定玩家中按权重（ELO/温度 + 探索）选一名代表；本季活跃玩家 = 所有强度玩家 + 这些代表对应的锚定玩家。
2. **匹配赛**：每个活跃玩家从**历史玩家池**与**本季其他虚拟玩家当前卡组**中按同段/邻段随机抽选对手，打对局直至达到「单赛季对局上限」或「单赛季失败次数上限」；池在段满踢人时不会踢出任何虚拟玩家当前卡组，保证可匹配集合完整。支持多 worker 并行跑局，阶段结束后**单线程**按结果更新池。
3. **卡组优化**：每个活跃玩家做邻域爬山；若找到更优卡组则切换并本季不再优化；强度玩家同卡组合并为一。
4. **赛季结束**：当前所有虚拟玩家卡组确保在池中；段满时按与同段相似度踢出。
5. **放弃**：长期无改进的强度玩家移出列表（卡组留池）；长期无改进的锚定玩家用含该物品的随机卡组重启。
6. **注入**：每 N 个赛季注入若干新强度玩家（随机卡组 + 初始对局）。
7. **报告/保存**：按 `TopInterval`/`SaveInterval` 输出 Top10、锚定样本并保存状态。

卡组优化采用**物品声明的上游/下游/邻居协同先验**（`ItemTemplate` 的 `UpstreamRequirements`、`DownstreamRequirements`、`NeighborPreference`），不再使用机制标签或 MechanicTagger。

---

## 命令行参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `--config <路径>` | 从 JSON 配置文件读取参数作为基准（命令行会覆盖同名字段） | 无 |
| `--resume <路径>` | 从指定状态文件恢复并继续优化 | 无（冷启动） |
| `--state <路径>` | 周期性保存/恢复时使用的状态文件路径 | `quality_deck_state.json` |
| `--top-interval <n>` | 每完成 n 个赛季输出一次 Top10 | 10 |
| `--save-interval <n>` | 每完成 n 个赛季保存一次状态 | 20 |
| `--segment-cap <n>` | 每个 ELO 分段的最大卡组数量 | 50 |
| `--games-per-eval <n>` | 每次评估时与对手的对战场数（量级） | 5 |
| `--max-climb-steps <n>` | 单次爬山最大步数 | 500 |
| `--restarts-per-shape <n>` | 每形状重启次数（内部/预留） | 5 |
| `--neighbor-sample <n>` | 邻域采样时每步最多评估的邻居数 | 80 |
| `--mab-budget <n>` | MAB 模式下每步最多评估的邻居数 | 30 |
| `--inner-wars <n>` | 内战：每次比较的对局数 | 3 |
| `--inner-budget <n>` | 内战：组合内部筛选预算 | 30 |
| `--inner-select-top <n>` | 内战：筛选后保留的候选数（Top-K） | 3 |
| `--inner-select-wars <n>` | 内战：候选排序时每个 match 的对局数 | 2 |
| `--confirm-opponents <n>` | 外战确认：抽取多少个强对手签名 | 8 |
| `--confirm-games <n>` | 外战确认：每个对手对局数 | 1 |
| `--explore-mix <x>` | 探索混合比例：1=完全均匀，0=完全按先验加权 | 0.30 |
| `--prior-ema <x>` | 先验 EMA 平滑系数 | 0.08 |
| `--synergy-pair <x>` | 物品对协同加权系数（0 表示不使用） | 0.35 |
| `--priors-clip <x>` | Priors 学习信号裁剪上限 | 200 |
| `--priors-unconfirmed <x>` | 未确认样本更新 Priors 的倍率（0~1） | 0.25 |
| `--priors-full-games <n>` | 达到该对局数后视为“可信度满” | 30 |
| `--priors-anneal-games <n>` | Priors 退火尺度 | 5000 |
| `--cand-rand-min <x>` | 候选生成：最低随机比例 | 0.15 |
| `--cand-item-start <x>` | 候选生成：单物品强度模式比例（早期） | 0.60 |
| `--cand-item-end <x>` | 候选生成：单物品强度模式比例（后期） | 0.15 |
| `--anchored-mix <x>` | anchored 相关（预留） | 0.50 |
| `--anchored-report <n>` | 报告中输出多少个物品的“最强拍档卡组” | 12 |
| `--representative-temperature <x>` | 锚定代表选择：softmax 温度 | 100 |
| `--representative-explore <x>` | 锚定代表选择：探索概率（均匀抽一名） | 0.10 |
| `--min-games-representative <n>` | 锚定代表选择：最小对局数低于此值则权重降权 | 0 |
| `--season-match-cap <n>` | 单赛季每玩家最大对局数（匹配赛） | 30 |
| `--season-loss-cap <n>` | 单赛季每玩家最大失败次数，超过则停止该玩家本季匹配 | 10 |
| `--inject-interval <n>` | 每完成 n **个赛季**执行一次强度玩家注入；0 表示禁用 | 20 |
| `--inject-count <n>` | 每次注入最多尝试加入的随机强度玩家数量 | 1 |
| `--abandon-threshold <n>` | 连续 n 赛季无改进则放弃（强度移出/锚定重启）；0 表示不放弃 | 15 |
| `--workers <n>` | 匹配赛阶段并行 worker 数；0 表示仅主线程 | 0 |
| `--max-seasons <n>` | 最多运行 n 个虚拟赛季后退出并保存；0 表示不限制 | 0 |

示例：

```bash
# 冷启动，每 5 赛季输出 Top10，每 15 赛季保存状态
dotnet run --project src/BazaarArena.QualityDeckFinder/BazaarArena.QualityDeckFinder.csproj -- --top-interval 5 --save-interval 15

# 使用 JSON 配置，并启用 4 worker 加速匹配赛
dotnet run --project src/BazaarArena.QualityDeckFinder/BazaarArena.QualityDeckFinder.csproj -- --config quality_deck_config.json --workers 4

# 从已保存的状态继续
dotnet run --project src/BazaarArena.QualityDeckFinder/BazaarArena.QualityDeckFinder.csproj -- --resume quality_deck_state.json

# 小规模测试：仅跑 10 个赛季后退出并保存到指定文件
dotnet run --project src/BazaarArena.QualityDeckFinder/BazaarArena.QualityDeckFinder.csproj -- --max-seasons 10 --state quality_deck_test_10.json --top-interval 5
```

**根据保存的元数据检查探测效果**：运行结束后可用脚本查看状态摘要与 Top 卡组：

```bash
python scripts/inspect_quality_deck_state.py quality_deck_test_10.json
```

---

## JSON 配置文件（--config）

`--config` 支持用 JSON 文件一次性配置大部分参数；程序先加载 JSON，再解析命令行覆盖同名字段。

---

## 输出说明

### 控制台

- **Top10 卡组（强度玩家）**：按 `--top-interval` 周期性输出当前 ELO 最高的 10 个强度玩家卡组，每行包含排名、ELO、**对局数**、形状、物品列表。
- **物品最强拍档（锚定玩家）**：对抽样物品（数量 `--anchored-report`），输出该物品所有锚定玩家中 ELO 最高者的当前卡组，含 **对局数**、形状、物品列表。
- 每次保存状态时打印：`[已保存状态到 <路径>]`

程序会一直运行，需手动中断（如 Ctrl+C）。

### 状态文件

使用 `--state` 指定的路径会周期性被覆盖，内容包含：

- 当前赛季编号、总对局数
- 锚定玩家（key → 当前 comboSig）、强度玩家 comboSig 列表
- 池内所有卡组（形状、物品列表、ELO、对局数等）
- 分段边界（固定五段，如 [1600, 1800, 2000, 2200]）、每段容量
- Priors（ItemWeights、PairWeights、ShapeCountWeights）等

可用于断点续跑或事后分析。

---

## 断点续跑

1. 使用 `--resume` 指定之前保存的状态文件。
2. 程序会加载池、虚拟玩家（锚定/强度）、赛季号、分段与计数，从中断处继续虚拟赛季循环。
3. 新注入的强度玩家或重启的锚定玩家仍按「新卡组」从段 0 起获得初始 ELO。

---

## 约束与约定

- **等级**：固定为 2（6 槽点）。
- **物品池**：海盗（Vanessa）最新版、铜级；卡组内不允许重复使用同一物品。带 **OverridableAttributes** 的物品进战时可复写属性设为 Bronze 档默认值的一半。
- **形状与次序**：7 种尺寸组成；槽位顺序在同组成下随机化。邻域为「同尺寸替换」+「任意两槽交换」。卡组优化时邻域与排列打分采用**物品声明的上游/下游/邻居协同先验**（无 MechanicTagger）。
- **段满踢出**：某段达到容量上限时，新卡组若加入则踢出该段内与同段最相似且 ELO 低于新卡组者，以保护多样性；**当前被锚定/强度玩家使用的卡组不会被踢出**，以保证匹配赛对手来源（历史玩家 + 其他虚拟玩家）完整。
- **多 worker**：匹配赛阶段可设 `--workers > 0`；对局由多 worker 并行执行，每 worker 只产出对局结果，阶段结束后单线程按顺序更新池，无需对池加锁。

更多算法与设计见 `docs/优质卡组探测器设计文档.md` 与 `implementation-notes` 中相关章节。
