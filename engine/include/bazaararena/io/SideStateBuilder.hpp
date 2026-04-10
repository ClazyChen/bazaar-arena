#pragma once

#include <optional>
#include <string>

#include <bazaararena/core/SideState.hpp>

#include "bazaararena/io/SimulateJob.hpp"

namespace bazaararena::io {

struct BuildSideStateResult final {
    std::optional<bazaararena::core::SideState> side;
    std::string error;
};

BuildSideStateResult BuildSideState(const SideSpec& spec);

}  // namespace bazaararena::io

