#include <iostream>

#include "bazaararena/engine.hpp"
#include "bazaararena/data/ItemDatabase.hpp"
#include "bazaararena/core/ItemKey.hpp"
#include "bazaararena/core/ItemTemplate.hpp"
#include "bazaararena/core/ItemTier.hpp"

int main(int argc, char** argv) {
    (void)argc;
    (void)argv;

    auto v = bazaararena::GetEngineVersion();
    std::cout << "bazaararena_cli " << v.major << "." << v.minor << "." << v.patch << "\n";

    auto items = bazaararena::data::GetAllItems();
    std::cout << "items=" << items.size() << "\n";
    for (const auto& r : items) {
        auto* t = r.templ;
        const auto& bronze = t->attributes[bazaararena::core::ItemTier::Bronze];
        std::cout << "- id=" << r.id << " key=" << r.key << " name=" << t->name
                  << " cooldown=" << bronze[bazaararena::core::ItemKey::Cooldown]
                  << " damage=" << bronze[bazaararena::core::ItemKey::Damage]
                  << " burn=" << bronze[bazaararena::core::ItemKey::Burn]
                  << " poison=" << bronze[bazaararena::core::ItemKey::Poison] << "\n";
    }

    return 0;
}

