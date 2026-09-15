using System.Windows.Forms;

namespace Moria;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new Game());
    }
}
