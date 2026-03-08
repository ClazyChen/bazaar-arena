# 将参考图复制为测试物品用图，便于 UI 显示。
# 约定：pictures/png/<Name>.png
# 参考图：独角鲸（小）、尖刺圆盾（中）、哈库维发射器（大）
# 测试物品（TestItems）：测试伤害、测试灼烧、测试暴击伤害、伤害随等级、冷却随等级（均为 Small）

# 脚本位于 <项目>/scripts/，故项目根目录为 PSScriptRoot 的父目录
$root = Split-Path -Parent $PSScriptRoot
$pngDir = Join-Path $root "pictures\png"

$smallRef = "独角鲸.png"
$mediumRef = "尖刺圆盾.png"
$largeRef = "哈库维发射器.png"

$testItems = @(
    "测试伤害", "测试灼烧", "测试暴击伤害", "伤害随等级", "冷却随等级"
)

if (-not (Test-Path (Join-Path $pngDir $smallRef))) {
    Write-Host "未找到参考图 $smallRef，请先将参考图放入 pictures\png\ 目录。"
    exit 0
}

foreach ($name in $testItems) {
    $dest = Join-Path $pngDir "$name.png"
    if (-not (Test-Path $dest)) {
        Copy-Item (Join-Path $pngDir $smallRef) $dest
        Write-Host "已复制: $name.png"
    }
}
