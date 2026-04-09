#include <iostream>

#include "bazaararena/engine.hpp"

int main(int argc, char** argv) {
  (void)argc;
  (void)argv;

  auto v = bazaararena::GetEngineVersion();
  std::cout << "bazaararena_cli " << v.major << "." << v.minor << "." << v.patch
            << "\n";
  std::cout << "用法（占位）：bazaararena_cli --input <path.json> --output <path.json>\n";
  return 0;
}

