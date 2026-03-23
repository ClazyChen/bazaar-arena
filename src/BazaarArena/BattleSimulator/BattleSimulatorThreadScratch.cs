namespace BazaarArena.BattleSimulator;

/// <summary>
/// 对战模拟热路径上的线程局部分配复用（<see cref="BattleSimulator"/> 实例可能被多线程共享，故不可使用实例字段存可写缓冲区）。
/// </summary>
internal static class BattleSimulatorThreadScratch
{
    private const int MaxNesting = 32;

    [ThreadStatic] private static int s_invokeDepth;
    [ThreadStatic] private static List<int>?[]? s_invokeIndexLists;

    [ThreadStatic] private static int s_execDepth;
    [ThreadStatic] private static List<(int TriggerName, int SideIndex, int ItemIndex)>?[]? s_execTriggerLists;
    [ThreadStatic] private static EffectApplyContextImpl?[]? s_execContexts;

    internal static void BeginInvokeTrigger()
    {
        s_invokeIndexLists ??= new List<int>[MaxNesting];
        if (s_invokeDepth >= MaxNesting)
            throw new InvalidOperationException("InvokeTrigger 嵌套超过上限，请检查是否出现意外的同步递归。");
        int d = s_invokeDepth++;
        s_invokeIndexLists[d] ??= new List<int>(32);
        s_invokeIndexLists[d]!.Clear();
    }

    internal static List<int> CurrentInvokeIndices() => s_invokeIndexLists![s_invokeDepth - 1]!;

    internal static void EndInvokeTrigger()
    {
        if (s_invokeDepth <= 0) return;
        s_invokeDepth--;
    }

    internal static void BeginExecuteOneEffect(
        out List<(int TriggerName, int SideIndex, int ItemIndex)> triggerList,
        out EffectApplyContextImpl ctx)
    {
        s_execTriggerLists ??= new List<(int, int, int)>[MaxNesting];
        s_execContexts ??= new EffectApplyContextImpl[MaxNesting];
        if (s_execDepth >= MaxNesting)
            throw new InvalidOperationException("ExecuteOneEffect 嵌套超过上限，请检查效果委托是否意外同步递归。");
        int d = s_execDepth++;
        s_execTriggerLists[d] ??= new List<(int, int, int)>(12);
        s_execTriggerLists[d]!.Clear();
        triggerList = s_execTriggerLists[d]!;
        s_execContexts[d] ??= new EffectApplyContextImpl();
        ctx = s_execContexts[d]!;
    }

    internal static void EndExecuteOneEffect()
    {
        if (s_execDepth <= 0) return;
        s_execDepth--;
    }

    internal static Core.IEffectApplyContext CurrentEffectApplyContextOrThrow()
    {
        if (s_execDepth <= 0 || s_execContexts == null)
            throw new InvalidOperationException("当前不在效果应用上下文内，无法执行 Apply。");
        var ctx = s_execContexts[s_execDepth - 1];
        if (ctx == null)
            throw new InvalidOperationException("效果应用上下文未初始化。");
        return ctx;
    }
}
