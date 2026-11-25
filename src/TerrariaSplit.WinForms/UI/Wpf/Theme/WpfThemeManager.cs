using System.Windows;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace TerrariaSplit.UI.Wpf.Theme;

internal static class WpfThemeManager
{
    private const string ThemeBundleMarkerKey = "__TerrariaSplitWpfThemeBundle";
    private const string AssemblyComponentPrefix = "pack://application:,,,/TerrariaSplit;component/UI/Wpf/Theme/";

    private static readonly object Sync = new();
    private static readonly List<WeakReference<Window>> TrackedWindows = [];

    private static WpfThemePreset currentTheme = WpfThemePreset.FluentMica;
    private static WpfDensityPreset currentDensity = WpfDensityPreset.Compact;

    public static WpfThemePreset CurrentTheme
    {
        get
        {
            lock (Sync)
            {
                return currentTheme;
            }
        }
    }

    public static WpfDensityPreset CurrentDensity
    {
        get
        {
            lock (Sync)
            {
                return currentDensity;
            }
        }
    }

    public static event Action<WpfThemePreset, WpfDensityPreset>? ThemeChanged;

    public static void SetTheme(WpfThemePreset theme, WpfDensityPreset density)
    {
        List<Window> activeWindows;
        lock (Sync)
        {
            currentTheme = theme;
            currentDensity = density;
            activeWindows = CollectLiveWindowsLocked();
        }

        foreach (Window window in activeWindows)
        {
            try
            {
                if (!window.Dispatcher.HasShutdownStarted)
                {
                    _ = window.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ApplyTheme(window, theme, density);
                        if (window is Controls.ModernWindow modern)
                        {
                            modern.RefreshWindowChromeAppearance();
                        }
                    }));
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        ThemeChanged?.Invoke(theme, density);
    }

    public static void ApplyTheme(
        FrameworkElement element,
        WpfThemePreset? themeOverride = null,
        WpfDensityPreset? densityOverride = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        WpfBootstrap.EnsureInitialized();

        WpfThemePreset theme = themeOverride ?? CurrentTheme;
        WpfDensityPreset density = densityOverride ?? CurrentDensity;
        ResourceDictionary bundle = CreateThemeBundle(theme, density);

        var merged = element.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i].Contains(ThemeBundleMarkerKey))
            {
                merged.RemoveAt(i);
            }
        }

        merged.Insert(0, bundle);

        if (element is Window window)
        {
            TrackWindow(window);
        }
    }

    public static ResourceDictionary CreateThemeBundle(
        WpfThemePreset? themeOverride = null,
        WpfDensityPreset? densityOverride = null)
    {
        WpfBootstrap.EnsureInitialized();
        WpfThemePreset theme = themeOverride ?? CurrentTheme;
        WpfDensityPreset density = densityOverride ?? CurrentDensity;

        var bundle = new ResourceDictionary
        {
            [ThemeBundleMarkerKey] = true
        };
        bundle.MergedDictionaries.Add(LoadDictionary("Controls.xaml"));
        bundle.MergedDictionaries.Add(LoadDictionary(GetColorFileName(theme)));
        bundle.MergedDictionaries.Add(LoadDictionary(GetDensityFileName(density)));
        return bundle;
    }

    public static WpfColor ResolveDwmBorderColor(FrameworkElement element)
    {
        if (element.TryFindResource("ThemeDwmBorderColor") is WpfColor color)
        {
            return color;
        }

        return WpfColor.FromRgb(37, 45, 54);
    }

    private static ResourceDictionary LoadDictionary(string fileName)
    {
        return new ResourceDictionary
        {
            Source = new Uri(AssemblyComponentPrefix + fileName, UriKind.Absolute)
        };
    }

    private static string GetColorFileName(WpfThemePreset theme) => theme switch
    {
        WpfThemePreset.FluentMica => "Colors.Fluent.xaml",
        WpfThemePreset.ShimmerCraft => "Colors.Shimmer.xaml",
        WpfThemePreset.BentoEsports => "Colors.Bento.xaml",
        _ => "Colors.Obsidian.xaml"
    };

    private static string GetDensityFileName(WpfDensityPreset density) => density switch
    {
        WpfDensityPreset.Balanced => "Density.Balanced.xaml",
        _ => "Density.Compact.xaml"
    };

    private static void TrackWindow(Window window)
    {
        lock (Sync)
        {
            for (int i = TrackedWindows.Count - 1; i >= 0; i--)
            {
                if (!TrackedWindows[i].TryGetTarget(out Window? existing))
                {
                    TrackedWindows.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(existing, window))
                {
                    return;
                }
            }

            TrackedWindows.Add(new WeakReference<Window>(window));
        }
    }

    private static List<Window> CollectLiveWindowsLocked()
    {
        var live = new List<Window>(TrackedWindows.Count);
        for (int i = TrackedWindows.Count - 1; i >= 0; i--)
        {
            if (TrackedWindows[i].TryGetTarget(out Window? window))
            {
                live.Add(window);
            }
            else
            {
                TrackedWindows.RemoveAt(i);
            }
        }

        return live;
    }
}
