using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (ApplicationUpdateCommandLine.TryRun(args, out int updateExitCode))
        {
            return updateExitCode;
        }

        if (!OperatingSystem.IsWindows())
        {
            return 1;
        }

        StartupDiagnostics.RecordTrace("ManagedEntry");
        ApplicationConfiguration.Initialize();
        StartupDiagnostics.RecordTrace("ApplicationConfigured");
        System.Windows.Forms.Application.Run(new MainForm());
        return 0;
    }
}
