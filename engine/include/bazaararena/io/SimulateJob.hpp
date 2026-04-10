#pragma once

#include <optional>
#include <string>
#include <string_view>
#include <vector>

namespace bazaararena::io {

struct DebugSpec final {
    bool enabled = false;
    std::string level = "none";  // none|summary|detailed
    int maxEvents = 20000;
};

struct ItemSpec final {
    std::string key;        // UTF-8 中文 key（用于 GetItemByKey）
    std::string tier;       // bronze|silver|gold|diamond (default bronze)
    std::optional<int> custom_0;
    std::optional<int> custom_1;
    std::optional<int> custom_2;
    std::optional<int> custom_3;
};

struct SideSpec final {
    int sideId = 0;
    int level = 1;

    // side attrs override (all optional). ItemCount is forbidden.
    std::optional<int> id;
    std::optional<int> maxHp;
    std::optional<int> hp;
    std::optional<int> shield;
    std::optional<int> burn;
    std::optional<int> poison;
    std::optional<int> regen;
    std::optional<int> resistance;
    std::optional<int> gold;
    std::optional<int> income;

    std::vector<ItemSpec> items;
};

struct SimulateJob final {
    int schemaVersion = 1;
    std::string jobId;
    std::string mode;

    std::optional<int> seed;
    bool allowTie = true;
    DebugSpec debug;

    std::vector<SideSpec> sides;  // must be 2
};

struct ParseJobResult final {
    std::optional<SimulateJob> job;
    std::string error;
};

ParseJobResult ParseSimulateJobJson(std::string_view jsonText);

}  // namespace bazaararena::io

