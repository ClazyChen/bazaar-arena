// Bazaar Arena 命令行对战测试工具：读入卡组集 JSON，选定两个卡组进行对战，并将日志输出到文件。
using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.DeckManager;
using BazaarArena.ItemDatabase;

var (jsonPath, deck1Id, deck2Id, logPath, detailed, skipFileLog) = ParseArgs(args);
if (jsonPath == null || deck1Id == null || deck2Id == null)
{
    PrintUsage();
    return 1;
}

var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
if (!Directory.Exists(dataDir))
    dataDir = Path.Combine(AppContext.BaseDirectory, "Data");

var levelupsPath = Path.Combine(dataDir, "levelups.json");
if (File.Exists(levelupsPath))
    LevelUpTable.Load(levelupsPath);

if (!File.Exists(jsonPath))
{
    Console.WriteLine($"错误：卡组集文件不存在：{jsonPath}");
    return 1;
}

var deckManager = new DeckManager();
deckManager.OpenCollection(jsonPath);

var deckA = deckManager.Load(deck1Id);
var deckB = deckManager.Load(deck2Id);

if (deckA == null || deckB == null)
{
    Console.WriteLine("错误：指定的卡组 ID 在当前卡组集中不存在。");
    Console.WriteLine($"卡组集：{jsonPath}");
    if (deckA == null)
        Console.WriteLine($"  未找到卡组：{deck1Id}");
    if (deckB == null)
        Console.WriteLine($"  未找到卡组：{deck2Id}");
    var ids = deckManager.List().ToList();
    if (ids.Count > 0)
    {
        Console.WriteLine($"当前卡组列表：{string.Join(", ", ids)}");
    }
    return 1;
}

var db = new ItemDatabase();
CommonSmall.RegisterAll(db);
CommonMedium.RegisterAll(db);
CommonLarge.RegisterAll(db);

var logLevel = detailed ? BattleLogLevel.Detailed : BattleLogLevel.Summary;
IBattleLogSink logSink;
FileBattleLogSink? fileSink = null;

if (skipFileLog)
{
    logSink = new ConsoleBattleLogSink(logLevel);
}
else
{
    var path = Path.GetFullPath(logPath ?? LogPaths.GetTimestampedLogPath());
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);
    fileSink = new FileBattleLogSink(BattleLogLevel.Detailed, path);
    logSink = new CompositeBattleLogSink(new ConsoleBattleLogSink(logLevel), fileSink);
}

var simulator = new BattleSimulator();
int winner = simulator.Run(deckA, deckB, db, logSink, BattleLogLevel.Detailed);

if (fileSink != null)
{
    Console.WriteLine($"日志已写入：{fileSink.Path}");
    fileSink.Dispose();
}

Console.WriteLine(winner < 0 ? "对战结束：平局。" : $"对战结束：玩家 {winner + 1} 获胜。");
return 0;

static (string? jsonPath, string? deck1Id, string? deck2Id, string? logPath, bool detailed, bool skipFileLog) ParseArgs(string[] args)
{
    string? jsonPath = null;
    string? deck1Id = null;
    string? deck2Id = null;
    string? logPath = null;
    bool detailed = false;
    bool skipFileLog = false;

    var list = args.Select(a => a.Trim()).ToList();
    for (var i = 0; i < list.Count; i++)
    {
        var a = list[i];
        if (a.Equals("--json", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= list.Count) continue;
            jsonPath = list[++i];
        }
        else if (a.Equals("--deck1", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= list.Count) continue;
            deck1Id = list[++i];
        }
        else if (a.Equals("--deck2", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= list.Count) continue;
            deck2Id = list[++i];
        }
        else if (a.Equals("--log", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= list.Count) continue;
            logPath = list[++i];
        }
        else if (a.Equals("--detailed", StringComparison.OrdinalIgnoreCase))
            detailed = true;
        else if (a.Equals("--nolog", StringComparison.OrdinalIgnoreCase) || a.Equals("--nofile", StringComparison.OrdinalIgnoreCase))
            skipFileLog = true;
        else if (!a.StartsWith("--"))
        {
            // 位置参数：jsonPath, deck1Id, deck2Id
            if (jsonPath == null)
                jsonPath = a;
            else if (deck1Id == null)
                deck1Id = a;
            else if (deck2Id == null)
                deck2Id = a;
        }
    }

    return (jsonPath, deck1Id, deck2Id, logPath, detailed, skipFileLog);
}

static void PrintUsage()
{
    Console.WriteLine("用法：BazaarArena.Cli <卡组集.json> <卡组1ID> <卡组2ID> [选项]");
    Console.WriteLine("  或：BazaarArena.Cli --json <路径> --deck1 <ID> --deck2 <ID> [选项]");
    Console.WriteLine("选项：");
    Console.WriteLine("  --log <路径>    日志输出文件路径（默认：Logs/ 下时间戳命名）");
    Console.WriteLine("  --detailed      控制台也输出详细日志");
    Console.WriteLine("  --nolog        不写文件日志，仅控制台");
}
