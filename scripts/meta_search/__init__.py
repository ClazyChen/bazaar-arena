"""meta_search：优质阵容探测主管线包（操作手册：docs/deck-search-pipeline.md）。

主管线模块：
- enumerate：并行锚点枚举（候选生成，驱动 bazaararena_gdf）。
- battle：对战评估层（主路径为 bazaararena_meta --serve 常驻模式；带缓存、自适应预算）。
- matrix / nash：真值矩阵构建与近似 Nash 求解。
- neighborhood_scan / elite_report：邻域穷举认证与收敛闭环（主驱动）。
- gdf_conditions：GDF 等级规则的 Python 复刻（仅限转换用途：CLI 观战/前端导入）。
- perm_constraints：排列机制等价类划分（供邻域扰动）。
- smoke_test：冒烟自检（改任何模块后必跑）。

遗产线（DO/PSRO 与理由图提议器）在 legacy/ 子包，主管线对其零依赖。
"""
