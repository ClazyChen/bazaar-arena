# Vanessa 金/钻批次物品测试用例（大巴扎导入）

本文档对应 **`Data/Decks/item_tests/test_vanessa_gold_diamond_batch.json`** 与脚本 **`scripts/item_tests/run_item_tests_vanessa_gold_diamond_batch.py`**。P2 侧多为「獠牙」铜档承伤；P1 为待测 Vanessa 物品（金 `tier: 2` 或钻 `tier: 3`）及少量公共/海盗辅助槽位。

**批量模式**：脚本在一次 `dotnet run` 内跑完全部对战，日志写入 `Logs/item_tests/<用例名>.log`。

## 用例与日志断言摘要

| 用例名 | deck1 ID | 主要断言（`log_contains` / `log_min_count`） |
|--------|----------|-----------------------------------------------|
| 投掷飞刀_throwing_knives | vgd_throw_knives_p1 | 投掷飞刀、伤害 |
| 吹箭枪_blowgun | vgd_blowgun_p1 | 吹箭枪、伤害、剧毒 |
| 侦查望远镜_spyglass | vgd_spyglass_p1 | 侦查望远镜、暴击率提高、冷却时间提高（流星索减速触发 + 战斗开始延长敌方冷却） |
| 侦查望远镜_S1_spyglass_s1 | vgd_spyglass_s1_p1 | 侦查望远镜_S1、冷却时间提高 |
| 潜水头盔_diving_helmet | vgd_diving_helmet_p1 | 潜水头盔、护盾、充能 |
| 划艇_rowboat | vgd_rowboat_p1 | 划艇、充能 |
| 钢琴_piano | vgd_piano_p1 | 钢琴、加速（三花被使用） |
| 刺刀手枪_pistol_sword | vgd_pistol_sword_p1 | 刺刀手枪、伤害 |
| 绊索_tripwire | vgd_tripwire_p1 | 绊索、减速 |
| 逞威风腰带扣_swash_buckle | vgd_swash_buckle_p1 | 獠牙、伤害（纯光环物品，以相邻獠牙输出验证） |
| 龟壳_turtle_shell | vgd_turtle_shell_p1 | 龟壳、护盾、充能（水草非武器触发充能） |
| 火药桶_powder_keg | vgd_powder_keg_p1 | 火药桶、伤害、摧毁 |
| 狙击步枪_sniper_rifle | vgd_sniper_rifle_p1 | 狙击步枪、伤害 |
| 船锚_anchor | vgd_anchor_p1 | 船锚、伤害 |
| 雷筒_blunderbuss | vgd_blunderbuss_p1 | 雷筒、伤害、灼烧 |
| 潜行滑翔机_stealth_glider | vgd_stealth_glider_p1 | 潜行滑翔机、无敌 |
| 滚石_the_boulder | vgd_the_boulder_p1 | 滚石、伤害 |
| 巨龟托图加_tortuga | vgd_tortuga_p1 | 巨龟托图加、伤害、加速 |
| 大坝_dam | vgd_dam_p1 | 大坝、摧毁 |
| 弩炮_ballista | vgd_ballista_p1 | 弩炮、伤害 |
| 沉眠元初体_slumbering_primordial | vgd_slumbering_primordial_p1 | 沉眠元初体、伤害、灼烧 |
| 火炮阵列_cannonade | vgd_cannonade_p1 | 火炮阵列、伤害、充能 |
| 电鳗_electric_eels | vgd_electric_eels_p1 | 电鳗、伤害、减速 |
| 灯塔_lighthouse | vgd_lighthouse_p1 | 灯塔、减速、灼烧 |
| 热带岛屿_tropical_island | vgd_tropical_island_p1 | 热带岛屿、生命再生 |
| 冰山_iceberg | vgd_iceberg_p1 | 冰山、冻结 |
| 船骸_shipwreck | vgd_shipwreck_p1 | 食人鱼、伤害；`[食人鱼] 伤害` ≥ 2（多重释放 + 船骸光环） |

## 说明

- 「每小时椰子/柑橘」等局外效果未实现，本批次不测。
- 若模板或模拟器语义变更导致日志关键词变化，应同步更新脚本中的 `log_contains` 与本表。
