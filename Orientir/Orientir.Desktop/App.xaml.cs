using System.Text;
using System.Windows;

namespace Orientir.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // cp1251 потрібен Core для читання DBF.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        base.OnStartup(e);
    }
}
