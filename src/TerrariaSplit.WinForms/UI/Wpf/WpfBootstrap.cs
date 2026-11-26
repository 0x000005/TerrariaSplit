using System.IO.Packaging;
using System.Windows;
using System.Windows.Forms.Integration;

namespace TerrariaSplit.UI.Wpf;

internal static class WpfBootstrap
{
    private static int initialized;

    public static void EnsureInitialized()
    {
        if (Interlocked.CompareExchange(ref initialized, 1, 0) != 0)
        {
            return;
        }

        Controls.ComboBoxWheelGuard.Register();

        // Register the pack:// URI scheme when WPF runs inside a WinForms host without App.xaml.
        _ = PackUriHelper.UriSchemePack;
        if (!UriParser.IsKnownScheme("pack"))
        {
            _ = new FrameworkElement();
        }

        if (System.Windows.Application.ResourceAssembly is null)
        {
            try
            {
                System.Windows.Application.ResourceAssembly = typeof(WpfBootstrap).Assembly;
            }
            catch (InvalidOperationException)
            {
                // ResourceAssembly can only be set once per AppDomain.
            }
        }
    }

    public static void EnableModelessKeyboardInterop(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        EnsureInitialized();
        ElementHost.EnableModelessKeyboardInterop(window);
    }
}
