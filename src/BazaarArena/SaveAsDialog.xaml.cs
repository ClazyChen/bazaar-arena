using System.Windows;

namespace BazaarArena;

public partial class SaveAsDialog
{
    public string DeckId => DeckIdBox.Text;

    public SaveAsDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
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
