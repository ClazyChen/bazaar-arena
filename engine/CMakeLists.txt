cmake_minimum_required(VERSION 3.20)
project(bazaararena_engine LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(bazaararena_engine STATIC
  src/engine/engine.cpp
  src/bazaararena/data/ItemDatabase.cpp
  src/bazaararena/data/items_generated.cpp
  src/bazaararena/core/BattleContext.cpp
  src/bazaararena/core/AbilityApply.cpp
  src/bazaararena/core/AbilityDefinition.cpp
  src/bazaararena/core/AbilityQueue.cpp
  src/bazaararena/core/SideState.cpp
  src/bazaararena/core/Simulator.cpp
  src/bazaararena/io/JsonLite.cpp
  src/bazaararena/io/SimulateJob.cpp
  src/bazaararena/io/SideStateBuilder.cpp
)

target_include_directories(bazaararena_engine PUBLIC
  ${CMAKE_CURRENT_SOURCE_DIR}/include
)

add_executable(bazaararena_cli
  cli/main.cpp
)
target_link_libraries(bazaararena_cli PRIVATE bazaararena_engine)

