cmake_minimum_required(VERSION 3.20)
project(bazaararena_engine LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

find_package(Threads REQUIRED)

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
  src/bazaararena/io/Sink.cpp
  src/bazaararena/io/SimulateJob.cpp
  src/bazaararena/io/SideStateBuilder.cpp
)

target_include_directories(bazaararena_engine PUBLIC
  ${CMAKE_CURRENT_SOURCE_DIR}/include
)

add_executable(bazaararena_cli
  cli/main.cpp
)
target_link_libraries(bazaararena_cli PRIVATE bazaararena_engine Threads::Threads)

add_executable(bazaararena_gdf
  gdf/main.cpp
  src/bazaararena/gdf/GdfLevelRules.cpp
  src/bazaararena/gdf/GdfLoadYamlPool.cpp
  src/bazaararena/gdf/ItemPool.cpp
  src/bazaararena/gdf/DeckRep.cpp
  src/bazaararena/gdf/GdfSideBuilder.cpp
  src/bazaararena/gdf/GdfItemPrototypeCache.cpp
  src/bazaararena/gdf/BattleEvaluator.cpp
  src/bazaararena/gdf/SwissTournament.cpp
  src/bazaararena/gdf/GreedySearcher.cpp
)
target_link_libraries(bazaararena_gdf PRIVATE bazaararena_engine Threads::Threads)

# 统一 CLI 输出到仓库根目录 bin/（与后端 simulate_run.default_cli_path 约定一致）
set(BAZAARARENA_CLI_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../bin")
file(MAKE_DIRECTORY "${BAZAARARENA_CLI_OUTPUT_DIR}")
set_target_properties(bazaararena_cli PROPERTIES
  RUNTIME_OUTPUT_DIRECTORY "${BAZAARARENA_CLI_OUTPUT_DIR}"
  RUNTIME_OUTPUT_DIRECTORY_DEBUG "${BAZAARARENA_CLI_OUTPUT_DIR}"
  RUNTIME_OUTPUT_DIRECTORY_RELEASE "${BAZAARARENA_CLI_OUTPUT_DIR}"
  RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO "${BAZAARARENA_CLI_OUTPUT_DIR}"
  RUNTIME_OUTPUT_DIRECTORY_MINSIZEREL "${BAZAARARENA_CLI_OUTPUT_DIR}"
)

set_target_properties(bazaararena_gdf PROPERTIES
  RUNTIME_OUTPUT_DIRECTORY "${BAZAARARENA_CLI_OUTPUT_DIR}"
  RUNTIME_OUTPUT_DIRECTORY_DEBUG "${BAZAARARENA_CLI_OUTPUT_DIR}"
  RUNTIME_OUTPUT_DIRECTORY_RELEASE "${BAZAARARENA_CLI_OUTPUT_DIR}"
  RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO "${BAZAARARENA_CLI_OUTPUT_DIR}"
  RUNTIME_OUTPUT_DIRECTORY_MINSIZEREL "${BAZAARARENA_CLI_OUTPUT_DIR}"
)

add_executable(bazaararena_gdf_pa
  gdf_pa/main.cpp
  gdf_pa/GdfPaFullTopk.cpp
  gdf_pa/GdfPaBracket.cpp
  gdf_pa/GdfPaMetrics.cpp
  gdf_pa/GdfPaGraph.cpp
  gdf_pa/GdfPaRoundRobin.cpp
  src/bazaararena/gdf/GdfLevelRules.cpp
  src/bazaararena/gdf/GdfLoadYamlPool.cpp
  src/bazaararena/gdf/ItemPool.cpp
  src/bazaararena/gdf/DeckRep.cpp
  src/bazaararena/gdf/GdfSideBuilder.cpp
  src/bazaararena/gdf/GdfItemPrototypeCache.cpp
  src/bazaararena/gdf/BattleEvaluator.cpp
)
target_link_libraries(bazaararena_gdf_pa PRIVATE bazaararena_engine Threads::Threads)
set_target_properties(bazaararena_gdf_pa PROPERTIES
  RUNTIME_OUTPUT_DIRECTORY "${BAZAARARENA_CLI_OUTPUT_DIR}"
  RUNTIME_OUTPUT_DIRECTORY_DEBUG "${BAZAARARENA_CLI_OUTPUT_DIR}"
  RUNTIME_OUTPUT_DIRECTORY_RELEASE "${BAZAARARENA_CLI_OUTPUT_DIR}"
  RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO "${BAZAARARENA_CLI_OUTPUT_DIR}"
  RUNTIME_OUTPUT_DIRECTORY_MINSIZEREL "${BAZAARARENA_CLI_OUTPUT_DIR}"
)

