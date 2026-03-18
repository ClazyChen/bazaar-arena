using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.QualityDeckFinder;

/// <summary>
/// 组合代表选择：无先验时直接返回 seed；有先验时用协同得分爬山快速得到代表，可选一次外战确认。
/// </summary>
public static class RepresentativeSelector
{
    public sealed record Result(
        DeckRep Representative,
        bool IsConfirmed,
        int InternalGames,
        int ExternalGames);

    /// <summary>
    /// 为 comboSig 找到代表排列。seed 必须属于该组合（物品集合一致，shapeCounts 一致）。
    /// 无协同先验时直接返回 seed；有先验时用协同得分指导爬山，快速完成（不做内战对战）。
    /// </summary>
    public static Result EnsureRepresentative(
        string comboSig,
        DeckRep seed,
        OptimizerState state,
        Config config,
        ItemPool pool,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random rng)
    {
        // 无先验：顺序可任意，直接返回 seed，不跑对战
        if (!SynergyScorer.DeckHasAnySynergyPrior(seed, db))
            return new Result(seed, false, 0, 0);

        // 有先验：用协同得分指导爬山，不依赖对战
        var internalBudget = Math.Max(1, config.InnerBudget);
        var current = seed;
        double currentScore = SynergyScorer.DeckOrderScore(current, db);

        for (int step = 0; step < internalBudget; step++)
        {
            var challenger = PermutationNeighbor.RandomSwap(current, rng);
            if (challenger == null) break;

            var challengerScore = SynergyScorer.DeckOrderScore(challenger, db);
            if (challengerScore > currentScore)
            {
                current = challenger;
                currentScore = challengerScore;
            }
        }

        // 外战确认：对手不足时直接返回当前代表
        var opponentSigs = SelectConfirmOpponents(state, config, GuessEloForConfirm(comboSig, state, config), config.ConfirmOpponents);
        if (opponentSigs.Count == 0)
            return new Result(current, false, 0, 0);

        _ = ExternalScore(current, opponentSigs, config.ConfirmGamesPerOpponent, state, simulator, db, rng, out int externalGames);
        return new Result(current, true, 0, externalGames);
    }

    private static double GuessEloForConfirm(string comboSig, OptimizerState state, Config config)
        => state.Pool.TryGetValue(comboSig, out var e) ? e.Elo : config.InitialElo;

    private static List<string> SelectConfirmOpponents(OptimizerState state, Config config, double baseElo, int k)
    {
        if (k <= 0) return [];
        var sigs = EloSystem.SelectOpponentSignatures(state, config, isNewDeck: false, baseElo, M: k);
        return sigs.Distinct(StringComparer.Ordinal).Take(k).ToList();
    }

    private static double ExternalScore(
        DeckRep cand,
        IReadOnlyList<string> opponentSigs,
        int gamesPerOpponent,
        OptimizerState state,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random rng,
        out int gamesPlayed)
    {
        int wins = 0, losses = 0, draws = 0;
        gamesPlayed = 0;
        var silentSink = new SilentBattleLogSink();
        foreach (var sig in opponentSigs)
        {
            if (!state.Pool.TryGetValue(sig, out var opp)) continue;
            var deckA = cand.ToDeck(db);
            var deckB = opp.Representative.ToDeck(db);
            for (int g = 0; g < Math.Max(1, gamesPerOpponent); g++)
            {
                int swap = rng.Next(2);
                Deck d0, d1;
                if (swap == 0) { d0 = deckA; d1 = deckB; }
                else { d0 = deckB; d1 = deckA; }
                int winner = simulator.Run(d0, d1, db, silentSink, BattleLogLevel.None);
                if (winner < 0) draws++;
                else
                {
                    // winner=0 表示 d0 赢；若交换后 cand 在 d1，则需要翻转
                    int candWinner = swap == 0 ? winner : (winner == 0 ? 1 : 0);
                    if (candWinner == 0) wins++; else losses++;
                }
                gamesPlayed++;
            }
        }
        // score：胜=1 平=0.5 负=0
        var total = wins + losses + draws;
        return total <= 0 ? 0 : (wins + 0.5 * draws) / total;
    }

    private static List<DeckRep> SelectTopCandidates(
        IReadOnlyList<DeckRep> candidates,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random rng,
        int topK,
        int warsPerMatch,
        out int gamesPlayed)
    {
        gamesPlayed = 0;
        var uniq = candidates
            .DistinctBy(c => c.Signature(), StringComparer.Ordinal)
            .ToList();
        if (uniq.Count <= 1) return uniq;

        topK = Math.Max(1, topK);
        warsPerMatch = Math.Max(1, warsPerMatch);

        // 简单“选王”：从一个随机开始，依次挑战；赢者留存。顺便把挑战者中打得最接近的记录为 runner-up。
        var king = uniq[rng.Next(uniq.Count)];
        var winRates = new Dictionary<string, double>(StringComparer.Ordinal) { [king.Signature()] = 1.0 };

        foreach (var ch in uniq)
        {
            if (ch.Signature() == king.Signature()) continue;
            var (winsA, winsB, draws) = BattleCompare.HeadToHead(king, ch, warsPerMatch, simulator, db, rng);
            gamesPlayed += warsPerMatch;
            var total = winsA + winsB + draws;
            var kingScore = total <= 0 ? 0.5 : (winsA + 0.5 * draws) / total;
            winRates[ch.Signature()] = 1.0 - kingScore;
            if (winsB > winsA || (winsB == winsA && SynergyScorer.DeckOrderScore(ch, db) > SynergyScorer.DeckOrderScore(king, db)))
                king = ch;
        }

        // 取 topK：按对 king 的胜率排序，平局时用协同顺序得分 tie-break
        var ordered = uniq
            .OrderByDescending(c => c.Signature() == king.Signature() ? 1.0 : winRates.GetValueOrDefault(c.Signature(), 0.0))
            .ThenByDescending(c => SynergyScorer.DeckOrderScore(c, db))
            .Take(topK)
            .ToList();
        if (!ordered.Any(c => c.Signature() == king.Signature()))
            ordered.Insert(0, king);
        return ordered.Take(topK).ToList();
    }

    private sealed class SilentBattleLogSink : IBattleLogSink
    {
        public void OnFrameStart(int timeMs, int frame) { }
        public void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp) { }
        public void OnCast(BattleItemState caster, string itemName, int timeMs, int? ammoRemainingAfter = null) { }
        public void OnEffect(BattleItemState caster, string itemName, string effectKind, int value, int timeMs, bool isCrit = false, string? extraSuffix = null) { }
        public void OnBurnTick(BattleSide victim, int burnDamage, int remainingBurn, int timeMs) { }
        public void OnPoisonTick(BattleSide victim, int poisonDamage, int timeMs) { }
        public void OnRegenTick(BattleSide side, int heal, int timeMs) { }
        public void OnSandstormTick(int damage, int timeMs) { }
        public void OnResult(int winnerSideIndex, int timeMs, bool isDraw) { }
    }
}

internal static class BattleCompare
{
    public static (int winsA, int winsB, int draws) HeadToHead(
        DeckRep a,
        DeckRep b,
        int games,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random rng)
    {
        var silentSink = new SilentBattleLogSink();
        int winsA = 0, winsB = 0, draws = 0;
        var deckA = a.ToDeck(db);
        var deckB = b.ToDeck(db);
        for (int g = 0; g < games; g++)
        {
            int swap = rng.Next(2);
            Deck d0, d1;
            if (swap == 0) { d0 = deckA; d1 = deckB; }
            else { d0 = deckB; d1 = deckA; }
            int winner = simulator.Run(d0, d1, db, silentSink, BattleLogLevel.None);
            if (winner < 0) draws++;
            else
            {
                int winnerA = swap == 0 ? winner : (winner == 0 ? 1 : 0);
                if (winnerA == 0) winsA++; else winsB++;
            }
        }
        return (winsA, winsB, draws);
    }

    private sealed class SilentBattleLogSink : IBattleLogSink
    {
        public void OnFrameStart(int timeMs, int frame) { }
        public void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp) { }
        public void OnCast(BattleItemState caster, string itemName, int timeMs, int? ammoRemainingAfter = null) { }
        public void OnEffect(BattleItemState caster, string itemName, string effectKind, int value, int timeMs, bool isCrit = false, string? extraSuffix = null) { }
        public void OnBurnTick(BattleSide victim, int burnDamage, int remainingBurn, int timeMs) { }
        public void OnPoisonTick(BattleSide victim, int poisonDamage, int timeMs) { }
        public void OnRegenTick(BattleSide side, int heal, int timeMs) { }
        public void OnSandstormTick(int damage, int timeMs) { }
        public void OnResult(int winnerSideIndex, int timeMs, bool isDraw) { }
    }
}

internal static class PermutationNeighbor
{
    public static DeckRep? RandomSwap(DeckRep rep, Random rng)
    {
        if (rep.Shape.Count <= 1) return null;
        int i = rng.Next(rep.Shape.Count);
        int j = rng.Next(rep.Shape.Count - 1);
        if (j >= i) j++;
        var newShape = rep.Shape.ToList();
        var newNames = rep.ItemNames.ToList();
        (newShape[i], newShape[j]) = (newShape[j], newShape[i]);
        (newNames[i], newNames[j]) = (newNames[j], newNames[i]);
        return new DeckRep(newShape, newNames);
    }
}

