#include <bazaararena/gdf/DeckRep.hpp>

#include <algorithm>
#include <sstream>

namespace bazaararena::gdf {

std::string DeckRep::Signature() const {
    std::ostringstream os;
    for (size_t i = 0; i < item_names.size(); i++) {
        if (i) os << ',';
        os << item_names[i];
    }
    return os.str();
}

ResolvedItem ResolveItemAlias(std::string_view display_name) {
    ResolvedItem r;
    r.db_key = std::string(display_name);
    if (display_name == "减速烙刀") {
        r.db_key = "烙刀";
        r.quest_index = 1;
    } else if (display_name == "加速烙刀") {
        r.db_key = "烙刀";
        r.quest_index = 2;
    }
    return r;
}

std::string BuildComboKey(const std::vector<std::string>& item_names) {
    std::vector<std::string> sorted = item_names;
    std::sort(sorted.begin(), sorted.end());
    std::ostringstream os;
    for (size_t i = 0; i < sorted.size(); i++) {
        if (i) os << ',';
        os << sorted[i];
    }
    return os.str();
}

DeckRep StripSeeds(const DeckRep& rep, const std::unordered_set<std::string>& seed_names) {
    DeckRep out;
    for (const auto& n : rep.item_names) {
        if (seed_names.count(n)) continue;
        out.item_names.push_back(n);
    }
    return out;
}

}  // namespace bazaararena::gdf
