## Engine CLI samples

### simulate（最小例子）

在仓库根目录运行（`-B` 可为任意 CMake 构建目录；**可执行文件始终输出到仓库根的 `bin/`**）：

```powershell
cmake -S engine -B build -DCMAKE_BUILD_TYPE=Debug
cmake --build build --config Debug --target bazaararena_cli
bin\bazaararena_cli.exe --input samples\cli\simulate_minimal_input.json --output build\simulate_minimal_output.json
```

