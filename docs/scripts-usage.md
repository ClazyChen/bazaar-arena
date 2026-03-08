# scripts 目录命令行工具使用说明

`scripts` 目录提供用于开发与发布 Bazaar Arena 的便捷脚本，支持 CMD 与 PowerShell 两种调用方式。

## 环境要求

- **.NET SDK**：需已安装与项目匹配的 .NET SDK（用于 `dotnet run` / `dotnet publish`）
- **Windows**：脚本针对 Windows 设计；`build-exe` 产出为 `win-x64` 自包含 exe
- **PowerShell**：`.cmd` 脚本会调用同名的 `.ps1`，需系统已安装 PowerShell
- **Python 3 + Pillow**：仅 **webp_to_png** 需要；用于将 `pictures/webp/` 下的 WebP 转为 PNG（见下文）

---

## 1. run — 开发态运行

在开发环境下直接运行 Bazaar Arena 项目（不打包成 exe）。

### 用法

**CMD（推荐在资源管理器中双击或命令行执行）：**

```cmd
scripts\run.cmd
```

**PowerShell：**

```powershell
.\scripts\run.ps1
```

### 命令行参数（可选）

运行时可传入以下参数，控制日志行为（不区分大小写）：

| 参数        | 说明 |
|-------------|------|
| `detailed`  | 控制台输出**详细**战斗日志（默认为摘要 Summary） |
| `nolog` 或 `nofile` | 不写入文件日志，仅输出到控制台 |

**示例：**

```cmd
REM 仅控制台、不写文件
scripts\run.cmd nolog

REM 控制台输出详细日志且不写文件
scripts\run.cmd nolog detailed
```

```powershell
.\scripts\run.ps1 nolog
.\scripts\run.ps1 nofile detailed
```

参数会原样传递给 Bazaar Arena 程序；未在上述列表中使用的参数会被程序忽略。

### 行为说明

- 自动将当前工作目录切换到仓库根目录（即 `scripts` 的上一级）
- 执行：`dotnet run --project src/BazaarArena/BazaarArena.csproj -- [参数...]`
- 适用于日常开发、调试与快速验证

---

## 2. build-exe — 发布可执行文件

将 Bazaar Arena 发布为 **Windows x64 自包含** 可执行文件，便于在没有安装 .NET 的机器上运行。

### 用法

**CMD：**

```cmd
scripts\build-exe.cmd
```

**PowerShell：**

```powershell
.\scripts\build-exe.ps1
```

若 PowerShell 执行策略限制脚本运行，可使用：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-exe.ps1
```

### 行为说明

- 自动将当前工作目录切换到仓库根目录
- 使用 `dotnet publish`，配置为：
  - 配置：`Release`
  - 运行时：`win-x64`
  - 自包含：`true`
- 输出目录：仓库根目录下的 **`publish`** 文件夹
- 生成的可执行文件：**`publish\BazaarArena.exe`**

### 输出示例

脚本执行成功后会提示：

```
已输出到: <仓库根路径>\publish\BazaarArena.exe
```

可将 `publish` 目录整体拷贝到目标 Windows x64 机器上，直接运行 `BazaarArena.exe`，无需单独安装 .NET 运行时。

发布后的 exe 同样支持 [run 的日志参数](#命令行参数可选)（如 `BazaarArena.exe nolog`、`BazaarArena.exe nolog detailed`）。

---

## 3. webp_to_png — WebP 转 PNG（图片资源）

将 **`pictures/webp/`** 目录下**尚未转换**的 WebP 图片转换为 PNG，保存到 **`pictures/png/`**。已存在同名 PNG 的文件会被跳过，便于增量更新（例如新增来自游戏资源网站的 WebP 后只转换新文件）。

### 环境

- **Python 3**
- **Pillow**：`pip install Pillow`

### 用法

在仓库根目录下执行（或从任意目录执行，脚本会自行定位仓库根）：

**CMD：**

```cmd
python scripts\webp_to_png.py
```

**PowerShell：**

```powershell
python scripts\webp_to_png.py
```

### 行为说明

- **源目录**：`pictures/webp/`（仅处理 `.webp` 文件）
- **输出目录**：`pictures/png/`（若不存在会自动创建）
- **“未转换”规则**：仅当 `pictures/png/<文件名>.png` 不存在时，才对该 WebP 进行转换；已存在的 PNG 不会覆盖
- 控制台会打印本次转换的文件数量及被跳过的数量，便于下一阶段界面测试时批量更新图片资源

---

## 脚本与入口对应关系

| 功能       | CMD 入口        | PowerShell 入口     | 实际逻辑所在   |
|------------|-----------------|----------------------|----------------|
| 开发态运行 | `scripts\run.cmd` | `scripts\run.ps1`   | `run.ps1`      |
| 发布 exe   | `scripts\build-exe.cmd` | `scripts\build-exe.ps1` | `build-exe.ps1` |
| WebP 转 PNG | `python scripts\webp_to_png.py` | 同上 | `scripts\webp_to_png.py` |

`.cmd` 脚本会先 `cd` 到仓库根目录，再以 `-ExecutionPolicy Bypass` 调用对应 `.ps1`，因此从任意子目录执行 `scripts\run.cmd` 或 `scripts\build-exe.cmd` 均可，工作目录会被正确设置。
