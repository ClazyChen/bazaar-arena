namespace BazaarArena.GreedyDeckFinder;

public sealed record DeckRep(IReadOnlyList<string> ItemNames)
{
    public string Signature() => string.Join(",", ItemNames);
}

public sealed class CandidateState
{
    public required string ComboKey { get; init; }
    public required DeckRep Representative { get; set; }
    public required int SizeSum { get; init; }
    public double SwissScore { get; set; }
    public double RoundRobinScore { get; set; }
    public HashSet<string> PlayedOpponents { get; } = new(StringComparer.Ordinal);
}

public static class ComboKeyUtil
{
    public static string BuildComboKey(IReadOnlyList<string> itemNames)
    {
        return string.Join(",", itemNames.OrderBy(x => x, StringComparer.Ordinal));
    }
}
