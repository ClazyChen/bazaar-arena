using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.QualityDeckFinder;

/// <summary>ELO 更新、按段抽样对手、新卡组先打段 0、入池时按段踢出最低（若满）。</summary>
public static class EloSystem
{
    /// <summary>计算 ELO 期望得分：己方 eloA 对 己方 eloB 的期望得分（0~1）。</summary>
    public static double ExpectedScore(double eloA, double eloB)
    {
        return 1.0 / (1.0 + Math.Pow(10, (eloB - eloA) / 400.0));
    }

    /// <summary>根据对战结果更新 ELO：winner 0 或 1 表示哪方胜，-1 平局。K 因子。</summary>
    public static void UpdateElo(double eloA, double eloB, int winner, double k, out double newEloA, out double newEloB)
    {
        var expA = ExpectedScore(eloA, eloB);
        var expB = 1.0 - expA;
        double actualA, actualB;
        if (winner < 0) { actualA = 0.5; actualB = 0.5; }
        else if (winner == 0) { actualA = 1; actualB = 0; }
        else { actualA = 0; actualB = 1; }
        newEloA = eloA + k * (actualA - expA);
        newEloB = eloB + k * (actualB - expB);
    }

    /// <summary>为卡组 D 选取对手签名列表：新卡组从段 0 起往高段抽直到够 M 个；否则从 D 所在段及上一段抽，最多 M 个。</summary>
    public static List<string> SelectOpponentSignatures(OptimizerState state, Config config, bool isNewDeck, double? deckElo, int M)
    {
        var segmentIndex = isNewDeck ? 0 : state.SegmentIndex(deckElo ?? config.InitialElo);
        var candidates = new List<string>();
        var maxSeg = config.SegmentBounds.Count;
        if (isNewDeck)
        {
            for (int s = 0; s <= maxSeg && candidates.Count < M; s++)
            {
                var inSeg = state.SignaturesInSegment(s);
                foreach (var sig in inSeg) { candidates.Add(sig); if (candidates.Count >= M) break; }
            }
        }
        else
        {
            for (int s = segmentIndex; s >= 0 && candidates.Count < M; s--)
            {
                var inSeg = state.SignaturesInSegment(s);
                foreach (var sig in inSeg) { candidates.Add(sig); if (candidates.Count >= M) break; }
            }
        }
        return candidates;
    }

    /// <summary>运行若干场对战：D 与 opponents 中随机对手对战，更新 D 与对手的 ELO；返回 D 的更新后 ELO。</summary>
    public static double RunGamesAndUpdateElo(
        DeckRep deckD,
        IReadOnlyList<string> opponentSignatures,
        OptimizerState state,
        Config config,
        SimulatorClass simulator,
        IItemTemplateResolver db)
    {
        if (opponentSignatures.Count == 0)
            return state.Pool.TryGetValue(deckD.Signature(), out var e) ? e.Elo : config.InitialElo;

        var silentSink = new SilentBattleLogSink();
        var k = config.EloK;
        var gamesPerOpp = Math.Max(1, config.GamesPerEval / Math.Max(1, opponentSignatures.Count));
        double currentEloD = state.Pool.TryGetValue(deckD.Signature(), out var entryD) ? entryD.Elo : config.InitialElo;

        foreach (var oppSig in opponentSignatures)
        {
            if (!state.Pool.TryGetValue(oppSig, out var oppEntry)) continue;
            var deckOpp = oppEntry.Deck;
            var deckDO = deckD.ToDeck();
            var deckOppO = deckOpp.ToDeck();
            double eloOpp = oppEntry.Elo;

            for (int g = 0; g < gamesPerOpp; g++)
            {
                int swap = Random.Shared.Next(2);
                Deck dA, dB;
                double eloA, eloB;
                if (swap == 0) { dA = deckDO; dB = deckOppO; eloA = currentEloD; eloB = eloOpp; }
                else { dA = deckOppO; dB = deckDO; eloA = eloOpp; eloB = currentEloD; }

                int winner = simulator.Run(dA, dB, db, silentSink, BattleLogLevel.None);
                if (winner >= 0 && swap == 1) winner = 1 - winner;
                UpdateElo(currentEloD, eloOpp, winner, k, out currentEloD, out eloOpp);

                state.Pool[oppSig] = new DeckEntry(oppEntry.Deck, eloOpp, oppEntry.IsLocalOptimum, oppEntry.GameCount + 1);
                oppEntry = state.Pool[oppSig];
                state.TotalGames++;
            }
        }

        TryAddToPool(state, config, deckD, currentEloD);
        return currentEloD;
    }

    /// <summary>将卡组加入池：按 ELO 归段，若段满则踢出该段 ELO 最低的一个。</summary>
    public static void TryAddToPool(OptimizerState state, Config config, DeckRep deck, double elo, bool isLocalOptimum = false)
    {
        var sig = deck.Signature();
        var segmentIndex = state.SegmentIndex(elo);
        var inSegment = state.SignaturesInSegment(segmentIndex);
        if (state.Pool.TryGetValue(sig, out var existing))
        {
            state.Pool[sig] = new DeckEntry(existing.Deck, elo, isLocalOptimum, existing.GameCount);
            return;
        }
        if (inSegment.Count >= config.SegmentCap)
        {
            string? lowestSig = null;
            double lowestElo = double.MaxValue;
            foreach (var s in inSegment)
            {
                if (state.Pool.TryGetValue(s, out var e) && e.Elo < lowestElo)
                {
                    lowestElo = e.Elo;
                    lowestSig = s;
                }
            }
            if (lowestSig != null && elo > lowestElo)
                state.Pool.Remove(lowestSig);
            else if (lowestSig == null || elo <= lowestElo)
                return;
        }
        state.Pool[sig] = new DeckEntry(deck, elo, isLocalOptimum, 0);
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
