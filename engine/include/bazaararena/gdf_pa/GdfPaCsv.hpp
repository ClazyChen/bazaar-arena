#pragma once

#include <string>
#include <string_view>

namespace bazaararena::gdf_pa {

/// RFC 4180 风格：含逗号、引号、换行时加双引号并转义引号。
[[nodiscard]] inline std::string CsvEscapeField(std::string_view s) {
    bool need = false;
    for (char c : s) {
        if (c == '"' || c == ',' || c == '\r' || c == '\n') {
            need = true;
            break;
        }
    }
    if (!need) return std::string(s);
    std::string o;
    o.push_back('"');
    for (char c : s) {
        if (c == '"')
            o += "\"\"";
        else
            o += c;
    }
    o.push_back('"');
    return o;
}

}  // namespace bazaararena::gdf_pa
