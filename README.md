# Bazaar Arena（重构中）

本仓库已进入新技术栈重构阶段，采用 **monorepo** 结构。

- `legacy/`：旧版 C# 参考实现（只读参考，避免继续扩展）。
- `data/`：物品数据源（YAML）与 schema（仅提交源数据）。
- `tools/`：YAML 校验与代码/数据库生成工具（Python）。
- `engine/`：C++ 计算层（核心库 + 调试 CLI）。
- `app/`：Web 应用（后端 Flask + 前端 Vue）。
- `infra/`：容器、部署与开发环境配置。
- `docs/`：新栈架构、协议与开发指南。

