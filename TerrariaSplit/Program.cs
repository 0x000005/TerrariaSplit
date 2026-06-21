using System.Windows.Forms;

namespace TerrariaSplit;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 1;
        }

        ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.Run(new MainForm());
        return 0;
    }
}
