using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.QualityDeckFinder;

/// <summary>
/// 组合代表选择：先在同一组合内部做“内战”快速筛选候选排列，再用少量外战对强对手集确认代表。
/// 默认偏快探索：内战/外战都使用较小预算，参数由 Config 控制。
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
        // 1) 内战：在同组合内做少量 swap-hillclimb，收集少量候选
        var internalBudget = Math.Max(1, config.InnerBudget);
        var warsPerCompare = Math.Max(1, config.InnerWars);
        var candidates = new List<DeckRep> { seed };
        var current = seed;
        int internalGames = 0;

        for (int step = 0; step < internalBudget; step++)
        {
            var challenger = PermutationNeighbor.RandomSwap(current, rng);
            if (challenger == null) break;

            var (winsA, winsB, draws) = BattleCompare.HeadToHead(current, challenger, warsPerCompare, simulator, db, rng);
            internalGames += warsPerCompare;

            // 偏保守：只在 challenger 明显更强时替换 current，避免被随机性抖动带偏
            if (winsB > winsA || (winsB == winsA && SynergyScorer.DeckOrderScore(challenger, db) > SynergyScorer.DeckOrderScore(current, db)))
            {
                current = challenger;
                candidates.Add(challenger);
            }
        }

        // 取 Top-3 候选：用一个很便宜的内部“小联赛”筛一遍
        var top = SelectTopCandidates(candidates, simulator, db, rng, config.InnerSelectTop, config.InnerSelectWars, out var extraGames);
        internalGames += extraGames;

        // 2) 外战确认：对段内/Top 的强对手集跑少量局，选最终代表
        var opponentSigs = SelectConfirmOpponents(state, config, top.Count > 0 ? GuessEloForConfirm(comboSig, state, config) : config.InitialElo, config.ConfirmOpponents);
        int externalGames = 0;
        if (opponentSigs.Count == 0 || top.Count == 0)
            return new Result(current, false, internalGames, externalGames);

        var best = top[0];
        double bestScore = double.NegativeInfinity;
        foreach (var cand in top)
        {
            var score = ExternalScore(cand, opponentSigs, config.ConfirmGamesPerOpponent, state, simulator, db, rng, out var games);
            externalGames += games;
            if (score > bestScore)
            {
                bestScore = score;
                best = cand;
            }
        }

        return new Result(best, IsConfirmed: true, internalGames, externalGames);
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

