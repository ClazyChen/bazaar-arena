using System.Windows;

namespace BazaarArena;

public partial class SaveAsDialog
{
    public string DeckId => DeckIdBox.Text;

    public SaveAsDialog()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
