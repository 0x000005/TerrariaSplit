using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            RecordUnhandled(eventArgs.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception."), "process");
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            AppLog.Failure("App", "task.unobserved", "An unobserved task failed.", "Source: task scheduler", eventArgs.Exception);
            eventArgs.SetObserved();
        };
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, eventArgs) =>
        {
            AppFault fault = RecordUnhandled(eventArgs.Exception, "ui-thread");
            TerrariaSplit.UI.Settings.SettingsMessageDialog.ShowThemed(null, "TerrariaSplit error", fault.ToReport(),
                MessageBoxButtons.OK, MessageBoxIcon.Error, text => text, selectableMessage: true, fault: fault);
            System.Windows.Forms.Application.Exit();
        };
        try
        {
            return Run(args);
        }
        catch (Exception exception)
        {
            RecordUnhandled(exception, "main");
            return 1;
        }
        finally
        {
            StartupDiagnostics.FlushTrace();
            AppLog.Provider.Dispose();
        }
    }

    private static AppFault RecordUnhandled(Exception exception, string source)
    {
        AppFault fault = AppLog.Failure("App", "app.unhandled", "An unhandled exception occurred.", "Source: " + source, exception);
        try { AppLog.Provider.FlushAsync().GetAwaiter().GetResult(); }
        catch (TimeoutException) { }
        return fault;
    }

    private static int Run(string[] args)
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
