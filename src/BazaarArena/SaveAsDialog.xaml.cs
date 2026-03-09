using System.Windows;

namespace BazaarArena;

public partial class SaveAsDialog
{
    public string DeckId => DeckIdBox.Text;

    /// <summary>重命名时预填的卡组名；设置后标题改为「重命名卡组」。</summary>
    public string? InitialDeckId { get; set; }

    public SaveAsDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (!string.IsNullOrEmpty(InitialDeckId))
            {
                DeckIdBox.Text = InitialDeckId;
                Title = "重命名卡组";
            }
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                DeckIdBox.Focus();
                DeckIdBox.SelectAll();
            });
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
