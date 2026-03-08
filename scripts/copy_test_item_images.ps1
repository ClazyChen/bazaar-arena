# 将参考图复制为测试物品用图，便于 UI 显示。
# 约定：pictures/png/<Name>.png
# 参考图：独角鲸（小）、尖刺圆盾（中）、哈库维发射器（大）
# 测试物品（TestItems）与尺寸：测试伤害 Small、测试灼烧 Medium、测试暴击伤害 Large、伤害随等级 Small、冷却随等级 Medium

# 脚本位于 <项目>/scripts/，故项目根目录为 PSScriptRoot 的父目录
$root = Split-Path -Parent $PSScriptRoot
$pngDir = Join-Path $root "pictures\png"

$smallRef = "独角鲸.png"
$mediumRef = "尖刺圆盾.png"
$largeRef = "哈库维发射器.png"

# 测试物品名称 -> 参考图（Small / Medium / Large）
$testItems = @(
    @{ Name = "测试伤害";     Ref = $smallRef },
    @{ Name = "测试灼烧";     Ref = $mediumRef },
    @{ Name = "测试暴击伤害"; Ref = $largeRef },
    @{ Name = "伤害随等级";   Ref = $smallRef },
    @{ Name = "冷却随等级";   Ref = $mediumRef }
)

foreach ($item in $testItems) {
    $refPath = Join-Path $pngDir $item.Ref
    if (-not (Test-Path $refPath)) {
        Write-Host "未找到参考图 $($item.Ref)，跳过: $($item.Name).png"
        continue
    }
    $dest = Join-Path $pngDir "$($item.Name).png"
    if (Test-Path $dest) {
        Remove-Item $dest -Force
    }
    Copy-Item $refPath $dest
    Write-Host "已生成: $($item.Name).png (来自 $($item.Ref))"
}
