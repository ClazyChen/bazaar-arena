// 优质卡组探测器：形状枚举 + 随机重启 + 同形状内局部爬山，单一池 + ELO 分段 + 每段独立上限。
using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using BazaarArena.ItemDatabase.Vanessa.Large;
using BazaarArena.ItemDatabase.Vanessa.Medium;
using BazaarArena.ItemDatabase.Vanessa.Small;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;
using BazaarArena.QualityDeckFinder;

var config = Config.Parse(args);
var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
if (!Directory.Exists(dataDir))
    dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
var levelupsPath = Path.Combine(dataDir, "levelups.json");
if (File.Exists(levelupsPath))
    LevelUpTable.Load(levelupsPath);

var db = new ItemDb();
CommonSmall.RegisterAll(db);
CommonMedium.RegisterAll(db);
CommonLarge.RegisterAll(db);
VanessaSmall.RegisterAll(db);
VanessaMedium.RegisterAll(db);
VanessaLarge.RegisterAll(db);

var simulator = new SimulatorClass();
var pool = new ItemPool(db);

if (config.ResumePath != null)
{
    if (!File.Exists(config.ResumePath))
    {
        Console.WriteLine($"错误：状态文件不存在：{config.ResumePath}");
        return 1;
    }
    var state = StatePersistence.Load(config.ResumePath, db, config);
    if (state == null)
    {
        Console.WriteLine("错误：加载状态失败");
        return 1;
    }
    Runner.Run(simulator, db, pool, state, config);
}
else
{
    var state = new OptimizerState(config);
    Runner.Run(simulator, db, pool, state, config);
}

return 0;
