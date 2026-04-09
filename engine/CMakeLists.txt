cmake_minimum_required(VERSION 3.20)
project(bazaararena_engine LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(bazaararena_engine STATIC
  src/engine/engine.cpp
)

target_include_directories(bazaararena_engine PUBLIC
  ${CMAKE_CURRENT_SOURCE_DIR}/include
)

add_executable(bazaararena_cli
  cli/main.cpp
)
target_link_libraries(bazaararena_cli PRIVATE bazaararena_engine)

