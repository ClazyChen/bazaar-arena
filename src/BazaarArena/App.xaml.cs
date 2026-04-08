using System.Windows;
using BazaarArena.Core;
using BazaarArena.DeckManager;
using BazaarArena.ItemDatabase;
using BazaarArena.ItemDatabase.Mak.Medium;
using BazaarArena.ItemDatabase.Mak.Small;
using BazaarArena.ItemDatabase.Vanessa.Large;
using BazaarArena.ItemDatabase.Vanessa.Medium;
using BazaarArena.ItemDatabase.Vanessa.Small;

namespace BazaarArena;

public partial class App : Application
{
    public DeckManager.DeckManager DeckManager { get; private set; } = null!;
    public ItemDatabase.ItemDatabase ItemDatabase { get; private set; } = null!;

    /// <summary>卡组集默认目录（Data/Decks），用于打开/保存对话框的初始路径。</summary>
    public static string DecksDirectory { get; private set; } = "";

    private static string LastCollectionFilePath => Path.Combine(Path.GetDirectoryName(DecksDirectory) ?? "", "last-collection.txt");

    /// <summary>读取上次打开的卡组集路径；无记录或文件不存在时返回 null。</summary>
    public static string? GetLastCollectionPath()
    {
        try
        {
            var p = File.ReadAllText(LastCollectionFilePath).Trim();
            return string.IsNullOrEmpty(p) ? null : p;
        }
        catch { return null; }
    }

    /// <summary>记录本次打开的卡组集路径，下次启动时优先打开。</summary>
    public static void SetLastCollectionPath(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(LastCollectionFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(LastCollectionFilePath, path);
        }
        catch { /* 忽略写入失败 */ }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dataDir))
            dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
        DecksDirectory = Path.Combine(dataDir, "Decks");
        if (!Directory.Exists(DecksDirectory))
            Directory.CreateDirectory(DecksDirectory);
        var levelupsPath = Path.Combine(dataDir, "levelups.json");
        if (File.Exists(levelupsPath))
            LevelUpTable.Load(levelupsPath);
        DeckManager = new DeckManager.DeckManager();
        ItemDatabase = new ItemDatabase.ItemDatabase();
        CommonSmall.RegisterAll(ItemDatabase);
        CommonMedium.RegisterAll(ItemDatabase);
        CommonLarge.RegisterAll(ItemDatabase);
        VanessaSmall.RegisterAll(ItemDatabase);
        VanessaMedium.RegisterAll(ItemDatabase);
        VanessaLarge.RegisterAll(ItemDatabase);
        MakSmall.RegisterAll(ItemDatabase);
        MakMedium.RegisterAll(ItemDatabase);
    }
}
