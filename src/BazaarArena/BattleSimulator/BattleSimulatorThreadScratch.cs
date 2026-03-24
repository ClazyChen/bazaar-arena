namespace BazaarArena.BattleSimulator;

/// <summary>
/// 对战模拟热路径上的线程局部分配复用（<see cref="BattleSimulator"/> 实例可能被多线程共享，故不可使用实例字段存可写缓冲区）。
/// </summary>
internal static class BattleSimulatorThreadScratch
{
    private const int MaxNesting = 32;

    [ThreadStatic] private static int s_invokeDepth;
    [ThreadStatic] private static List<int>?[]? s_invokeIndexLists;
    [ThreadStatic] private static Core.BattleContext?[]? s_invokeContexts;
    [ThreadStatic] private static BattleState?[]? s_invokeBattleStates;

    [ThreadStatic] private static int s_execDepth;
    [ThreadStatic] private static Core.BattleContext?[]? s_execContexts;
    [ThreadStatic] private static BattleState?[]? s_execBattleStates;

    internal static void BeginInvokeTrigger()
    {
        s_invokeIndexLists ??= new List<int>[MaxNesting];
        s_invokeContexts ??= new Core.BattleContext[MaxNesting];
        s_invokeBattleStates ??= new BattleState[MaxNesting];
        if (s_invokeDepth >= MaxNesting)
            throw new InvalidOperationException("InvokeTrigger 嵌套超过上限，请检查是否出现意外的同步递归。");
        int d = s_invokeDepth++;
        s_invokeIndexLists[d] ??= new List<int>(32);
        s_invokeIndexLists[d]!.Clear();
        s_invokeContexts[d] ??= new Core.BattleContext();
        s_invokeBattleStates[d] ??= new BattleState();
    }

    internal static List<int> CurrentInvokeIndices() => s_invokeIndexLists![s_invokeDepth - 1]!;
    internal static Core.BattleContext CurrentInvokeContext() => s_invokeContexts![s_invokeDepth - 1]!;
    internal static BattleState CurrentInvokeBattleState() => s_invokeBattleStates![s_invokeDepth - 1]!;

    internal static void EndInvokeTrigger()
    {
        if (s_invokeDepth <= 0) return;
        s_invokeDepth--;
    }

    internal static void BeginExecuteOneEffect(
        out Core.BattleContext ctx,
        out BattleState battleState)
    {
        s_execContexts ??= new Core.BattleContext[MaxNesting];
        s_execBattleStates ??= new BattleState[MaxNesting];
        if (s_execDepth >= MaxNesting)
            throw new InvalidOperationException("ExecuteOneEffect 嵌套超过上限，请检查效果委托是否意外同步递归。");
        int d = s_execDepth++;
        s_execContexts[d] ??= new Core.BattleContext();
        s_execBattleStates[d] ??= new BattleState();
        ctx = s_execContexts[d]!;
        battleState = s_execBattleStates[d]!;
    }

    internal static void EndExecuteOneEffect()
    {
        if (s_execDepth <= 0) return;
        s_execDepth--;
    }

}
