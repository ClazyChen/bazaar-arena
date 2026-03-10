namespace BazaarArena.BattleSimulator;

/// <summary>实现 <see cref="IBattleLogSink"/>，在回调中累加每物品统计与按时间采样的强度曲线，Run 结束后可生成 <see cref="BattleRunStats"/>。</summary>
public class StatsCollectingSink : IBattleLogSink
{
    /// <summary>强度曲线采样间隔（毫秒），默认 500。</summary>
    public int CurveIntervalMs { get; set; } = 500;

    private readonly Dictionary<(int Side, int ItemIndex), ItemAccumEntry> _itemAccum = [];
    private int _side0Damage;
    private int _side0Burn;
    private int _side0Poison;
    private int _side0Shield;
    private int _side0Heal;
    private int _side0Regen;
    private int _side1Damage;
    private int _side1Burn;
    private int _side1Poison;
    private int _side1Shield;
    private int _side1Heal;
    private int _side1Regen;
    private int _lastCurveTimeMs = -1;
    private int _lastHp0, _lastHp1;
    private int _lastRecordedHp0 = -1, _lastRecordedHp1 = -1;
    private int _initialHp0, _initialHp1;
    private readonly List<StrengthCurvePoint> _curveSide0 = [];
    private readonly List<StrengthCurvePoint> _curveSide1 = [];
    private int _winner = -2;
    private int _durationMs;

    private sealed class ItemAccumEntry
    {
        public string ItemName = "";
        public ItemAccum Accum;
    }

    private struct ItemAccum
    {
        public int CastCount;
        public int Damage;
        public int Burn;
        public int Poison;
        public int Shield;
        public int Heal;
        public int Regen;
    }

    public void OnFrameStart(int timeMs, int frame) { }

    /// <summary>每帧调用；仅在生命值相对上一帧发生变化时记录曲线点，以即时反映生命变化。</summary>
    public void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp)
    {
        _lastHp0 = side0Hp;
        _lastHp1 = side1Hp;
        if (_lastCurveTimeMs < 0)
        {
            _initialHp0 = side0Hp;
            _initialHp1 = side1Hp;
            AddCurvePointSide0(0, side0Hp);
            AddCurvePointSide1(0, side1Hp);
            _lastRecordedHp0 = side0Hp;
            _lastRecordedHp1 = side1Hp;
            _lastCurveTimeMs = 0;
            return;
        }
        if (side0Hp != _lastRecordedHp0)
        {
            AddCurvePointSide0(timeMs, _lastRecordedHp0); // 阶梯：先延续旧值到当前时刻
            AddCurvePointSide0(timeMs, side0Hp);         // 再在同一时刻跳到新值
            _lastRecordedHp0 = side0Hp;
        }
        if (side1Hp != _lastRecordedHp1)
        {
            AddCurvePointSide1(timeMs, _lastRecordedHp1);
            AddCurvePointSide1(timeMs, side1Hp);
            _lastRecordedHp1 = side1Hp;
        }
    }

    public void OnCast(int sideIndex, int itemIndex, string itemName, int timeMs, int? ammoRemainingAfter = null)
    {
        var key = (sideIndex, itemIndex);
        if (!_itemAccum.TryGetValue(key, out var entry))
        {
            entry = new ItemAccumEntry { ItemName = itemName };
            _itemAccum[key] = entry;
        }
        entry.ItemName = itemName;
        entry.Accum.CastCount++;
        TryRecordCurve(timeMs);
    }

    public void OnEffect(int sideIndex, int itemIndex, string itemName, string effectKind, int value, int timeMs, bool isCrit = false, string? extraSuffix = null)
    {
        var key = (sideIndex, itemIndex);
        if (!_itemAccum.TryGetValue(key, out var entry))
        {
            entry = new ItemAccumEntry { ItemName = itemName };
            _itemAccum[key] = entry;
        }
        entry.ItemName = itemName;
        ref var a = ref entry.Accum;

        switch (effectKind)
        {
            case "伤害":
            case "吸血": // 吸血与伤害为同一数值，仅展示不同，统计时均计入伤害
                a.Damage += value;
                AddSide(sideIndex, damage: value);
                break;
            case "灼烧":
                a.Burn += value;
                AddSide(sideIndex, burn: value);
                break;
            case "剧毒":
                a.Poison += value;
                AddSide(sideIndex, poison: value);
                break;
            case "护盾":
                a.Shield += value;
                AddSide(sideIndex, shield: value);
                break;
            case "治疗":
                a.Heal += value;
                AddSide(sideIndex, heal: value);
                break;
            case "生命再生":
                a.Regen += value;
                AddSide(sideIndex, regen: value);
                break;
        }

        TryRecordCurve(timeMs);
    }

    private void AddSide(int sideIndex, int damage = 0, int burn = 0, int poison = 0, int shield = 0, int heal = 0, int regen = 0)
    {
        if (sideIndex == 0)
        {
            _side0Damage += damage;
            _side0Burn += burn;
            _side0Poison += poison;
            _side0Shield += shield;
            _side0Heal += heal;
            _side0Regen += regen;
        }
        else
        {
            _side1Damage += damage;
            _side1Burn += burn;
            _side1Poison += poison;
            _side1Shield += shield;
            _side1Heal += heal;
            _side1Regen += regen;
        }
    }

    private void AddCurvePointSide0(int timeMs, int hp)
    {
        _curveSide0.Add(new StrengthCurvePoint
        {
            TimeMs = timeMs,
            Damage = _side0Damage,
            Burn = _side0Burn,
            Poison = _side0Poison,
            Shield = _side0Shield,
            Heal = _side0Heal,
            Regen = _side0Regen,
            Hp = Math.Max(0, hp),
        });
    }

    private void AddCurvePointSide1(int timeMs, int hp)
    {
        _curveSide1.Add(new StrengthCurvePoint
        {
            TimeMs = timeMs,
            Damage = _side1Damage,
            Burn = _side1Burn,
            Poison = _side1Poison,
            Shield = _side1Shield,
            Heal = _side1Heal,
            Regen = _side1Regen,
            Hp = Math.Max(0, hp),
        });
    }

    private void TryRecordCurve(int timeMs)
    {
        // 生命曲线改由 OnHpSnapshot 在生命值变化时记录，此处仅保留供其他扩展用（如按间隔采样）。
        if (CurveIntervalMs <= 0) return;
        if (_lastCurveTimeMs >= 0 && timeMs - _lastCurveTimeMs < CurveIntervalMs)
            return;
        int t = (timeMs / CurveIntervalMs) * CurveIntervalMs;
        if (t <= _lastCurveTimeMs)
            return;
        _lastCurveTimeMs = t;
    }

    public void OnBurnTick(int sideIndex, int burnDamage, int remainingBurn, int timeMs) => TryRecordCurve(timeMs);
    public void OnPoisonTick(int sideIndex, int poisonDamage, int timeMs) => TryRecordCurve(timeMs);
    public void OnRegenTick(int sideIndex, int heal, int timeMs) => TryRecordCurve(timeMs);
    public void OnSandstormTick(int damage, int timeMs) { }

    public void OnResult(int winnerSideIndex, int timeMs, bool isDraw)
    {
        _winner = winnerSideIndex;
        _durationMs = timeMs;
        TryRecordCurve(timeMs);
    }

    /// <summary>获取本次 Run 的统计；应在 Run 结束后调用。</summary>
    public BattleRunStats GetStats()
    {
        var itemStats = _itemAccum.OrderBy(kv => kv.Key.Side).ThenBy(kv => kv.Key.ItemIndex)
            .Select(kv => new ItemStatRow
            {
                SideIndex = kv.Key.Side,
                ItemName = kv.Value.ItemName,
                CastCount = kv.Value.Accum.CastCount,
                Damage = kv.Value.Accum.Damage,
                Burn = kv.Value.Accum.Burn,
                Poison = kv.Value.Accum.Poison,
                Shield = kv.Value.Accum.Shield,
                Heal = kv.Value.Accum.Heal,
                Regen = kv.Value.Accum.Regen,
            }).ToList();

        List<StrengthCurvePoint> c0 = _curveSide0.Count == 0 || _curveSide0[0].TimeMs > 0
            ? [new StrengthCurvePoint { TimeMs = 0, Hp = _initialHp0 }, .._curveSide0]
            : [.._curveSide0];
        List<StrengthCurvePoint> c1 = _curveSide1.Count == 0 || _curveSide1[0].TimeMs > 0
            ? [new StrengthCurvePoint { TimeMs = 0, Hp = _initialHp1 }, .._curveSide1]
            : [.._curveSide1];
        // 若最后一点早于结束时间，补终点使曲线延伸到战斗结束（生命显示为 0 以上）
        if (_durationMs > 0 && c0.Count > 0 && c0[^1].TimeMs < _durationMs)
            c0.Add(new StrengthCurvePoint { TimeMs = _durationMs, Hp = Math.Max(0, _lastHp0), Damage = _side0Damage, Burn = _side0Burn, Poison = _side0Poison, Shield = _side0Shield, Heal = _side0Heal, Regen = _side0Regen });
        if (_durationMs > 0 && c1.Count > 0 && c1[^1].TimeMs < _durationMs)
            c1.Add(new StrengthCurvePoint { TimeMs = _durationMs, Hp = Math.Max(0, _lastHp1), Damage = _side1Damage, Burn = _side1Burn, Poison = _side1Poison, Shield = _side1Shield, Heal = _side1Heal, Regen = _side1Regen });

        return new BattleRunStats
        {
            Winner = _winner,
            DurationMs = _durationMs,
            ItemStats = itemStats,
            StrengthCurveSide0 = c0,
            StrengthCurveSide1 = c1,
        };
    }

    /// <summary>重置状态以便复用同一 Sink 进行下一次 Run。</summary>
    public void Reset()
    {
        _itemAccum.Clear();
        _side0Damage = _side0Burn = _side0Poison = _side0Shield = _side0Heal = _side0Regen = 0;
        _side1Damage = _side1Burn = _side1Poison = _side1Shield = _side1Heal = _side1Regen = 0;
        _lastCurveTimeMs = -1;
        _lastRecordedHp0 = _lastRecordedHp1 = -1;
        _initialHp0 = _initialHp1 = 0;
        _curveSide0.Clear();
        _curveSide1.Clear();
        _winner = -2;
        _durationMs = 0;
    }
}
