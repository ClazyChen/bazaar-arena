using System.Windows;
using BazaarArena.Core;
using BazaarArena.DeckManager;
using BazaarArena.ItemDatabase;

namespace BazaarArena;

public partial class App : Application
{
    public DeckManager.DeckManager DeckManager { get; private set; } = null!;
    public ItemDatabase.ItemDatabase ItemDatabase { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dataDir))
            dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
        var levelupsPath = Path.Combine(dataDir, "levelups.json");
        if (File.Exists(levelupsPath))
            LevelUpTable.Load(levelupsPath);
        var decksDir = Path.Combine(dataDir, "Decks");
        DeckManager = new DeckManager.DeckManager(decksDir);
        ItemDatabase = new ItemDatabase.ItemDatabase();
        TestItems.RegisterAll(ItemDatabase);
    }
}
