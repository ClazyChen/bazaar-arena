using System.Windows;
using BazaarArena.Core;
using BazaarArena.DeckManager;
using BazaarArena.ItemDatabase;

namespace BazaarArena;

public partial class App : Application
{
    public DeckManager.DeckManager DeckManager { get; private set; } = null!;
    public ItemDatabase.ItemDatabase ItemDatabase { get; private set; } = null!;

    /// <summary>卡组集默认目录（Data/Decks），用于打开/保存对话框的初始路径。</summary>
    public static string DecksDirectory { get; private set; } = "";

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
    }
}
