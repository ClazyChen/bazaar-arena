#pragma once

#include <random>

namespace bazaararena::core {

class Random {
public:
    std::mt19937_64 rng;

    int Next(int maxExclusive) { return rng() % maxExclusive; }
    int Next(int minInclusive, int maxExclusive) { return rng() % (maxExclusive - minInclusive) + minInclusive; }
    int Next100() { return Next(100); }

    void Seed(int seed = std::random_device()()) { rng.seed(seed); }
};

} // namespace bazaararena::core
