# 生成 Bazaar Arena 可执行文件（exe）
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$OutDir = Join-Path $Root "publish"
dotnet publish src/BazaarArena/BazaarArena.csproj -c Release -r win-x64 --self-contained true -o $OutDir
Write-Host "已输出到: $OutDir\BazaarArena.exe"
