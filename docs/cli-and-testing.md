# CLI 与物品测试

本文档说明 Bazaar Arena 的命令行工具（CLI）用法，以及基于 CLI 的物品行为测试方法与流程。**完成任务后应使用本流程进行验证**；测试未通过时在文档中标注「未通过测试」，不放松断言。

---

## 1. 命令行工具（CLI）

- **项目**：`src/BazaarArena.Cli/BazaarArena.Cli.csproj`（控制台 exe，引用主项目 BazaarArena）
- **作用**：读入指定卡组集 JSON、选定两个卡组进行单场对战，并将详细战斗日志写入文件（可选同时输出到控制台）；也支持按批量配置在**一次进程内**完成多场对战，减少启动开销，供物品测试脚本使用
- **入口**：从仓库根目录运行（需能访问 `Data/levelups.json`，CLI 项目会将之复制到输出目录）

### 1.1 用法：单次对战

```bash
# 位置参数：<卡组集.json> <卡组1ID> <卡组2ID>
dotnet run --project src/BazaarArena.Cli -- <卡组集路径> <deck1_id> <deck2_id> [选项]

# 或命名参数
dotnet run --project src/BazaarArena.Cli -- --json <路径> --deck1 <id> --deck2 <id> [选项]
```

### 1.2 用法：批量对战（供测试脚本使用）

物品测试脚本会调用 CLI 的批量模式，在一次进程中完成多场对战以缩短总时间。批量模式配置为一个 JSON 文件：

```json
{
  "battles": [
    { "deck1": "sb_fang_p1", "deck2": "sb_fang_p2", "log": "Logs/item_tests/獠牙_fang.log" },
    { "deck1": "sb_lava_core_p1", "deck2": "sb_lava_core_p2", "log": "Logs/item_tests/岩浆核心_lava_core.log" }
  ]
}
```

调用方式：

```bash
dotnet run --project src/BazaarArena.Cli -- --json <卡组集路径> --batch <批量配置.json> [选项]
```

每个 `battles` 项会分别写入自己的日志文件；CLI 会在控制台输出每场对战的结果与日志路径。测试脚本不直接使用单次对战模式，而是构造上述批量配置后调用批量模式。

### 1.3 参数

| 参数 | 含义 | 必需 |
|------|------|------|
| 卡组集 JSON 路径 | 要加载的卡组集文件（与 GUI 使用的格式一致） | 是 |
| 卡组 1 / 卡组 2 ID | 对战双方在卡组集中的卡组 ID | 是 |
| `--log <路径>` | 详细日志输出文件路径；不传则使用 `Logs/` 下时间戳命名（批量模式下由批量配置的 `log` 字段决定） | 否 |
| `--detailed` | 控制台也输出详细日志（默认仅摘要） | 否 |
| `--nolog` | 不写文件日志，仅控制台 | 否 |

### 1.4 输出与退出码

- **标准输出**：对战结果（「对战结束：玩家 N 获胜」或「平局」）；若写入了文件日志，会打印「日志已写入：<路径>」
- **退出码**：`0` 表示成功；`1` 表示参数错误或卡组不存在等
- **日志文件**：详细日志为逐帧与每条效果（施放、伤害、灼烧结算、剧毒结算、护盾、充能、冻结等）的文本记录，用于自动化断言

---

## 2. 测试方法

- **手段**：用 CLI 跑指定卡组对（来自同一卡组集 JSON），将详细日志写入固定路径，再对日志内容做**字符串包含**断言（不修改断言策略以迁就当前实现）
- **卡组集**：物品测试用卡组定义在 `Data/Decks/item_tests/test_small_bronze.json`（小型铜）、`Data/Decks/item_tests/test_small_silver.json`（小型银）、`Data/Decks/item_tests/test_medium_bronze.json`（中型铜）、`Data/Decks/item_tests/test_medium_silver.json`（中型银）、`Data/Decks/item_tests/test_medium_gold_diamond.json`（中型金/钻）、`Data/Decks/item_tests/test_large_bronze.json`（大型铜）、`Data/Decks/item_tests/test_large_silver_gold_diamond.json`（大型银/金/钻）等；每个用例对应两个卡组 ID（P1、P2）
- **断言**：每个用例规定「日志中必须包含」的若干字符串（如「獠牙」「伤害」「灼烧结算」「冻结」）；若任一项未出现则判该用例失败
- **期望数值**：断言中的伤害/护盾等须按**当前物品模板**与光环叠加计算（模板或词条变更后应同步改 `log_contains` 与 **docs/test-cases-\*.md** / **items-list**），勿保留与实现对不齐的陈旧字面量
- **失败处理**：用例失败时，在 **docs/test-cases-small-bronze.md** 与 **docs/items-list.md** 中标注对应物品/用例「**未通过测试**」，并注明失败原因与日志路径，供后续排查与修复；**不通过放宽断言来让测试通过**

---

## 3. 测试流程（自动化）

1. **运行脚本**（仓库根目录）：
   ```bash
   # 小型铜物品
   python scripts/item_tests/run_item_tests_small_bronze.py

   # 小型银物品
   python scripts/item_tests/run_item_tests_small_silver.py

   # 中型铜物品
   python scripts/item_tests/run_item_tests_medium_bronze.py

   # 中型银物品
   python scripts/item_tests/run_item_tests_medium_silver.py

   # 海盗小型铜物品
   python scripts/item_tests/run_item_tests_vanessa_small_bronze.py

   # 海盗中型铜物品
   python scripts/item_tests/run_item_tests_vanessa_medium_bronze.py

   # 大型铜物品
   python scripts/item_tests/run_item_tests_large_bronze.py

   # 大型银/金/钻物品
   python scripts/item_tests/run_item_tests_large_silver_gold_diamond.py

   # 中型金/钻物品
   python scripts/item_tests/run_item_tests_medium_gold_diamond.py
   ```
2. **脚本行为**：
   - 按顺序对每个已定义用例调用 CLI：`dotnet run --project src/BazaarArena.Cli -- Data/Decks/item_tests/test_small_bronze.json <deck1_id> <deck2_id> --log Logs/item_tests/<用例名>.log`
   - 检查 CLI 退出码为 0
   - 读取该用例的日志文件，检查是否包含该用例要求的全部字符串
3. **结果**：
   - 全部通过：脚本退出码 0，打印「全部 N 个小型铜物品测试通过」
   - 存在失败：脚本退出码 1，打印失败用例名与缺失的字符串；维护者据此更新文档中的「未通过测试」标注

---

## 4. 相关文件

| 文件 | 说明 |
|------|------|
| `src/BazaarArena.Cli/Program.cs` | CLI 入口与参数解析 |
| `Data/Decks/item_tests/test_small_bronze.json` | 小型铜物品测试用卡组集 |
| `Data/Decks/item_tests/test_small_silver.json` | 小型银物品测试用卡组集 |
| `Data/Decks/item_tests/test_medium_bronze.json` | 中型铜物品测试用卡组集 |
| `Data/Decks/item_tests/test_medium_silver.json` | 中型银物品测试用卡组集 |
| `Data/Decks/item_tests/test_medium_gold_diamond.json` | 中型金/钻物品测试用卡组集 |
| `Data/Decks/item_tests/test_large_bronze.json` | 大型铜物品测试用卡组集 |
| `Data/Decks/item_tests/test_large_silver_gold_diamond.json` | 大型银/金/钻物品测试用卡组集 |
| `scripts/item_tests/run_item_tests_small_bronze.py` | 小型铜物品自动化测试脚本 |
| `scripts/item_tests/run_item_tests_small_silver.py` | 小型银物品自动化测试脚本 |
| `scripts/item_tests/run_item_tests_medium_bronze.py` | 中型铜物品自动化测试脚本 |
| `scripts/item_tests/run_item_tests_medium_silver.py` | 中型银物品自动化测试脚本 |
| `scripts/item_tests/run_item_tests_medium_gold_diamond.py` | 中型金/钻物品自动化测试脚本 |
| `scripts/item_tests/run_item_tests_large_bronze.py` | 大型铜物品自动化测试脚本 |
| `scripts/item_tests/run_item_tests_large_silver_gold_diamond.py` | 大型银/金/钻物品自动化测试脚本 |
| `docs/test-cases-small-bronze.md` | 小型铜测试用例说明与预期 |
| `docs/test-cases-small-silver.md` | 小型银测试用例说明与预期 |
| `docs/test-cases-medium-bronze.md` | 中型铜测试用例说明与预期 |
| `docs/test-cases-medium-silver.md` | 中型银测试用例说明与预期 |
| `docs/test-cases-medium-gold-diamond.md` | 中型金/钻测试用例说明与预期 |
| `docs/test-cases-large-bronze.md` | 大型铜测试用例说明与预期 |
| `docs/test-cases-large-silver-gold-diamond.md` | 大型银/金/钻测试用例说明与预期 |
| `docs/items-list.md` | 物品列表与各档位「测试状态」列 |
| `Logs/item_tests/*.log` | 各用例的详细对战日志（脚本运行后生成） |

---

## 5. 扩展

- 新增物品或新档位测试时：在对应卡组集 JSON 中增加卡组，在相应脚本中增加用例（如小型铜用 `scripts/item_tests/run_item_tests_small_bronze.py`，小型银用 `scripts/item_tests/run_item_tests_small_silver.py`，中型铜用 `scripts/item_tests/run_item_tests_medium_bronze.py`，中型金/钻用 `scripts/item_tests/run_item_tests_medium_gold_diamond.py`，大型铜用 `scripts/item_tests/run_item_tests_large_bronze.py`，大型银/金/钻用 `scripts/item_tests/run_item_tests_large_silver_gold_diamond.py`，增加 `name`、`deck1`、`deck2`、`log_contains`），并在 `docs/test-cases-*.md` 与 `docs/items-list.md` 中补充说明与状态列。

---

## 6. 测试经验总结

- **覆盖范围**：当前已完成小型铜、小型银、中型铜、中型金/钻、大型铜、大型银/金/钻六档物品的自动化测试，卡组集与脚本一一对应（见上文表 4）；用例与 `docs/items-list.md` 中测试状态列保持一致。
- **断言方式**：以「日志中必须包含」的字符串为主（`log_contains`）；需要验证「至少出现 N 次」时使用可选字段 `log_min_count`（如牵引光束第二次伤害）。
- **纯光环物品**：仅提供光环、无施放/效果日志的物品（如外骨骼），其名称不会出现在战斗日志中；断言应改为验证**受光环影响的结果**（如相邻武器造成的伤害数值），而非物品名。
- **创造触发条件**：若被测效果依赖特定状态（如「修复已摧毁物品」「被毁物品为飞行时再造成伤害」），可在同一卡组内用其他物品创造条件（如牵引光束摧毁右侧物品、宇宙护符+友好玩偶使护符起飞），再断言后续效果与日志关键词。
- **跨档位卡组**：测试卡组可混合尺寸/档位（如中型铜废品场维修机器人用例中放入牵引光束+獠牙以创造修复目标），只要卡组集 JSON 与主项目物品注册涵盖所用物品即可。
- **回归与标注**：修改物品或模拟器后按档位跑对应脚本做回归；失败时在用例文档与 `docs/items-list.md` 标注「未通过测试」并记原因与日志路径，修复通过后同步更新为「通过」并移除旧标注。
