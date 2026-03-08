# 运行 Bazaar Arena（开发态）
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
dotnet run --project src/BazaarArena/BazaarArena.csproj -- @args
