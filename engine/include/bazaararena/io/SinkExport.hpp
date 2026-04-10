#pragma once

#include <string_view>

#include <bazaararena/io/JsonLite.hpp>
#include <bazaararena/io/Sink.hpp>

namespace bazaararena::io {

inline std::string_view SinkTypeToDebugLevel(int sink_type) {
    if (sink_type == Sink::TypeSummary) return "summary";
    if (sink_type == Sink::TypeDetailed) return "detailed";
    return "none";
}

inline void FillDebugJson(JsonObject& debugObj, const Sink& sink) {
    debugObj["level"] = std::string(SinkTypeToDebugLevel(sink.sink_type));
    debugObj["truncated"] = sink.truncated;

    if (sink.sink_type == Sink::TypeSummary) {
        JsonArray lines;
        lines.reserve(sink.lines.size());
        for (const auto& s : sink.lines) lines.emplace_back(s);
        debugObj["lines"] = std::move(lines);
    } else if (sink.sink_type == Sink::TypeDetailed) {
        debugObj["events"] = sink.events;
    } else {
        debugObj["events"] = JsonArray{};
    }
}

}  // namespace bazaararena::io

