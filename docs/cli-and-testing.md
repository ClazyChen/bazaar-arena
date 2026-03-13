# CLI 与物品测试

本文档说明 Bazaar Arena 的命令行工具（CLI）用法，以及基于 CLI 的物品行为测试方法与流程。**完成任务后应使用本流程进行验证**；测试未通过时在文档中标注「未通过测试」，不放松断言。

---

## 1. 命令行工具（CLI）

- **项目**：`src/BazaarArena.Cli/BazaarArena.Cli.csproj`（控制台 exe，引用主项目 BazaarArena）
- **作用**：读入指定卡组集 JSON、选定两个卡组进行单场对战，并将详细战斗日志写入文件（可选同时输出到控制台）
- **入口**：从仓库根目录运行（需能访问 `Data/levelups.json`，CLI 项目会将之复制到输出目录）

### 1.1 用法

```bash
# 位置参数：<卡组集.json> <卡组1ID> <卡组2ID>
dotnet run --project src/BazaarArena.Cli -- <卡组集路径> <deck1_id> <deck2_id> [选项]

# 或命名参数
dotnet run --project src/BazaarArena.Cli -- --json <路径> --deck1 <id> --deck2 <id> [选项]
```

### 1.2 参数

| 参数 | 含义 | 必需 |
|------|------|------|
| 卡组集 JSON 路径 | 要加载的卡组集文件（与 GUI 使用的格式一致） | 是 |
| 卡组 1 / 卡组 2 ID | 对战双方在卡组集中的卡组 ID | 是 |
| `--log <路径>` | 详细日志输出文件路径；不传则使用 `Logs/` 下时间戳命名 | 否 |
| `--detailed` | 控制台也输出详细日志（默认仅摘要） | 否 |
| `--nolog` | 不写文件日志，仅控制台 | 否 |

### 1.3 输出与退出码

- **标准输出**：对战结果（「对战结束：玩家 N 获胜」或「平局」）；若写入了文件日志，会打印「日志已写入：<路径>」
- **退出码**：`0` 表示成功；`1` 表示参数错误或卡组不存在等
- **日志文件**：详细日志为逐帧与每条效果（施放、伤害、灼烧结算、剧毒结算、护盾、充能、冻结等）的文本记录，用于自动化断言

---

## 2. 测试方法

- **手段**：用 CLI 跑指定卡组对（来自同一卡组集 JSON），将详细日志写入固定路径，再对日志内容做**字符串包含**断言（不修改断言策略以迁就当前实现）
- **卡组集**：物品测试用卡组定义在 `Data/Decks/test_small_bronze.json`（小型铜物品）等；每个用例对应两个卡组 ID（P1、P2）
- **断言**：每个用例规定「日志中必须包含」的若干字符串（如「獠牙」「伤害」「灼烧结算」「冻结」）；若任一项未出现则判该用例失败
- **失败处理**：用例失败时，在 **docs/test-cases-small-bronze.md** 与 **docs/items-list.md** 中标注对应物品/用例「**未通过测试**」，并注明失败原因与日志路径，供后续排查与修复；**不通过放宽断言来让测试通过**

---

## 3. 测试流程（自动化）

1. **运行脚本**（仓库根目录）：
   ```bash
   python scripts/run_item_tests.py
   ```
2. **脚本行为**：
   - 按顺序对每个已定义用例调用 CLI：`dotnet run --project src/BazaarArena.Cli -- Data/Decks/test_small_bronze.json <deck1_id> <deck2_id> --log Logs/item_tests/<用例名>.log`
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
| `Data/Decks/test_small_bronze.json` | 小型铜物品测试用卡组集 |
| `scripts/run_item_tests.py` | 小型铜物品自动化测试脚本（用例与断言在此定义） |
| `docs/test-cases-small-bronze.md` | 小型铜测试用例说明与预期；含「未通过测试」标注 |
| `docs/items-list.md` | 物品列表与小型铜「测试状态」列 |
| `Logs/item_tests/*.log` | 各用例的详细对战日志（脚本运行后生成） |

---

## 5. 扩展

- 新增物品或新档位测试时：在对应卡组集 JSON 中增加卡组，在 `run_item_tests.py` 中增加用例（`name`、`deck1`、`deck2`、`log_contains`），并在 `docs/test-cases-*.md` 与 `docs/items-list.md` 中补充说明与状态列；脚本可扩展为支持多套用例（如中型、大型）或多卡组集。
