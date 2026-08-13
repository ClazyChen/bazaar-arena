"""meta_search：优质阵容搜索的测量底座与元博弈工具链。

阶段 1（测量底座）模块：
- gdf_conditions：GDF 等级规则（战斗档位/任务覆写/overridable 缩放/魂石变体）的 Python 复刻，
  用于把卡组签名转换为与 GDF 完全一致的 CLI 对战条件。
- battle：基于 bazaararena_cli 的可复现、带缓存、自适应预算的系列赛评估。
"""
