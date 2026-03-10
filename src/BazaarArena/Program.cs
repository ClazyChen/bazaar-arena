// Bazaar Arena 程序入口。
using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.DeckManager;
using BazaarArena.ItemDatabase;

Console.WriteLine("你好，Bazaar Arena！");

// 基座：运行一场测试对战并输出分级日志
var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
if (!Directory.Exists(dataDir))
    dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
var decksDir = Path.Combine(dataDir, "Decks");

// 加载等级→最大生命值表（1 级 300，2 级 = 300 + Level1 的 HealthIncrease，以此类推）
var levelupsPath = Path.Combine(dataDir, "levelups.json");
if (File.Exists(levelupsPath))
    LevelUpTable.Load(levelupsPath);

var deckManager = new DeckManager();
var db = new ItemDatabase();
CommonSmall.RegisterAll(db);
CommonMedium.RegisterAll(db);

var defaultCollectionPath = Path.Combine(decksDir, "default.json");
if (File.Exists(defaultCollectionPath))
    deckManager.OpenCollection(defaultCollectionPath);

Deck? deckA = deckManager.Load("deck_a");
Deck? deckB = deckManager.Load("deck_b");

if (deckA == null || deckB == null)
{
    // 若未找到文件则用内存卡组
    deckA = new Deck
    {
        PlayerLevel = 5,
        Slots =
        [
            new() { ItemName = "獠牙", Tier = ItemTier.Bronze },
            new() { ItemName = "獠牙", Tier = ItemTier.Silver },
        ],
    };
    deckB = new Deck
    {
        PlayerLevel = 5,
        Slots =
        [
            new() { ItemName = "獠牙", Tier = ItemTier.Gold },
            new() { ItemName = "獠牙", Tier = ItemTier.Diamond },
        ],
    };
    Console.WriteLine("使用内存卡组（未找到 Data/Decks 下的 JSON）。");
}

var argsList = args.Select(a => a.Trim()).ToList();
var logLevel = argsList.Contains("detailed", StringComparer.OrdinalIgnoreCase)
    ? BattleLogLevel.Detailed
    : BattleLogLevel.Summary;
var skipFileLog = argsList.Contains("nolog", StringComparer.OrdinalIgnoreCase) || argsList.Contains("nofile", StringComparer.OrdinalIgnoreCase);

IBattleLogSink logSink;
FileBattleLogSink? fileSink = null;
if (skipFileLog)
{
    logSink = new ConsoleBattleLogSink(logLevel);
}
else
{
    // 文件日志始终使用 Detailed，保证日志文件包含完整战斗过程；控制台仍按参数选择 Summary/Detailed
    fileSink = new FileBattleLogSink(BattleLogLevel.Detailed);
    logSink = new CompositeBattleLogSink(new ConsoleBattleLogSink(logLevel), fileSink);
}

var simulator = new BattleSimulator();
int winner = simulator.Run(deckA, deckB, db, logSink, logLevel);
if (fileSink != null)
{
    Console.WriteLine($"日志已写入：{fileSink.Path}");
    fileSink.Dispose();
}
Console.WriteLine(winner < 0 ? "对战结束：平局。" : $"对战结束：玩家 {winner + 1} 获胜。");
