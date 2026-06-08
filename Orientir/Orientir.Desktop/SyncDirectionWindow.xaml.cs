using System.Windows;

namespace Orientir.Desktop;

// Маленький діалог вибору напрямку синхронізації налаштувань змагання.
// ToServer=true — локально→сервер; false — сервер→локально.
public partial class SyncDirectionWindow : Window
{
    public bool ToServer { get; private set; }

    public SyncDirectionWindow(string slug)
    {
        InitializeComponent();
        TxtPrompt.Text = $"Синхронізувати налаштування змагання «{slug}». Оберіть напрямок:";
    }

    private void ToServer_Click(object sender, RoutedEventArgs e)
    {
        ToServer = true;
        DialogResult = true;
    }

    private void ToLocal_Click(object sender, RoutedEventArgs e)
    {
        ToServer = false;
        DialogResult = true;
    }
}
