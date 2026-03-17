# 优质卡组探测器 使用说明

优质卡组探测器（BazaarArena.QualityDeckFinder）是一个控制台工具，用于在**固定等级 2**、**海盗（Vanessa）最新版铜级物品池**下，通过随机重启与同形状内局部爬山，发现「优质卡组」——即在当前形状下，任意同尺寸替换或同尺寸交换都无法得到更强卡组的配置。强度由 ELO 体系衡量，采用单一池 + ELO 分段 + 每段独立上限，新卡组先与低分段对手对战，随爬山逐渐升到高分段。

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

从输出目录运行时，`Data/levelups.json` 已在同目录下，无需额外配置。

---

## 命令行参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `--resume <路径>` | 从指定状态文件恢复并继续优化 | 无（冷启动） |
| `--state <路径>` | 周期性保存/恢复时使用的状态文件路径 | `quality_deck_state.json` |
| `--top-interval <n>` | 每完成 n 次爬山输出一次 Top10 | 10 |
| `--save-interval <n>` | 每完成 n 次爬山保存一次状态 | 20 |
| `--segment-cap <n>` | 每个 ELO 分段的最大卡组数量 | 50 |
| `--games-per-eval <n>` | 每次评估时与对手的对战场数（量级） | 5 |
| `--max-climb-steps <n>` | 单次爬山最大步数 | 500 |
| `--restarts-per-shape <n>` | 每种形状的随机重启次数（当前实现中主循环为无限，此参数预留） | 5 |
| `--neighbor-sample <n>` | 邻域采样时每步最多评估的邻居数 | 80 |
| `--mab-budget <n>` | MAB 模式下每步最多评估的邻居数 | 30 |
| `--segment-expand-step <n>` | 分段自动扩展步长：池内最高 ELO 超过当前最高段下界超过此值时追加新边界 | 200 |
| `--segment-expand-max-bounds <n>` | 分段边界数量上限（段数=上限+1），防止无限扩展 | 10 |
| `--inject-interval <n>` | 每完成 n 次爬山执行一次随机卡组注入；0 表示禁用 | 20 |
| `--inject-count <n>` | 每次注入最多尝试加入的随机卡组数量 | 1 |

示例：

```bash
# 冷启动，每 5 次爬山输出 Top10，每 15 次保存状态
dotnet run --project src/BazaarArena.QualityDeckFinder/BazaarArena.QualityDeckFinder.csproj -- --top-interval 5 --save-interval 15

# 从已保存的状态继续优化
dotnet run --project src/BazaarArena.QualityDeckFinder/BazaarArena.QualityDeckFinder.csproj -- --resume quality_deck_state.json

# 指定状态文件路径，并提高每段容量
dotnet run --project src/BazaarArena.QualityDeckFinder/BazaarArena.QualityDeckFinder.csproj -- --state my_state.json --segment-cap 80
```

---

## 输出说明

### 控制台

- **Top10 卡组**：按配置的 `--top-interval` 周期性输出当前 ELO 最高的 10 个卡组，每行包含：
  - 排名
  - ELO 分数
  - 形状（如 `1+2+3`、`2+2+2`）
  - 槽位物品名称（多槽时可能截断为前若干项 + `...`）
  - 若该卡组已在该次爬山中收敛为局部最优，会标注 `[局部最优]`
- 每次保存状态时会打印：`[已保存状态到 <路径>]`

程序会一直运行，不会自动退出；需要手动中断（如 Ctrl+C）。

### 状态文件

使用 `--state` 指定的路径（默认 `quality_deck_state.json`）会周期性被覆盖写入，内容包含：
- 分段边界会在运行中按需向高分方向自动扩展并写入状态；断点续跑时会恢复已扩展的边界。

- 当前池内所有卡组（形状、物品列表、ELO、是否局部最优、对战次数等）
- 分段定义（边界、每段容量）
- 总重启次数、总爬山次数、总对局数等

可用于断点续跑或事后分析。

---

## 断点续跑

1. 使用 `--resume` 指定之前保存的状态文件（通常与 `--state` 保存的路径一致）。
2. 程序会加载池内卡组与 ELO、分段定义与计数，然后从中断处继续「随机重启 + 局部爬山」循环。
3. 新生成的卡组仍按「新卡组先打段 0」获得初始 ELO；已在池中的卡组按当前 ELO 从对应段及上一段抽对手评估。

示例：

```bash
dotnet run --project src/BazaarArena.QualityDeckFinder/BazaarArena.QualityDeckFinder.csproj -- --resume quality_deck_state.json --state quality_deck_state.json
```

若希望续跑时改变输出或保存频率，可同时传入 `--top-interval`、`--save-interval` 等，恢复后将按新参数执行。

---

## 约束与约定

- **等级**：固定为 2（6 槽点）。
- **物品池**：海盗（Vanessa）最新版、铜级；卡组内**不允许重复使用同一物品**。带 **OverridableAttributes** 的物品在进战时其可复写属性会设为 **Bronze 档默认值的一半**（模拟 2 级时局外成长较低）。
- **形状与次序**：尺寸组成为 7 种（如 6 小、4 小+1 中、3 小+1 大等）；**槽位顺序在同组成下随机化**（如 4 小+1 中 的中型可在左/中/右任意位置）。邻域为「同尺寸替换」+「任意两槽交换」（交换时同时交换尺寸与物品），即不同大小的物品也可调换次序。
- **段满踢出**：某段达到容量上限时，新卡组若加入则踢出该段内**与同段最相似（最冗余）且 ELO 低于新卡组**的一张，以保护池内多样性（而非仅踢 ELO 最低）。
- **优质**：在当前形状下，不存在严格更强的邻居（邻域采样 + 首次改进 + MAB 下收敛即标记为局部最优）。
- **随机卡组注入**：当 `--inject-interval` 大于 0 时，每隔该次数爬山会向池中注入若干随机新卡组（与重启同分布），打初始对局后入池，以丰富多样性、便于其他流派进入高分段。

更多算法与设计细节见项目计划与 `implementation-notes` 中相关章节。
