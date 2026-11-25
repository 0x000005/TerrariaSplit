using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using TerrariaSplit.UI.Wpf.Theme;
using TerrariaSplit.UI.Wpf.Interop;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;

namespace TerrariaSplit.UI.Wpf.Controls;

internal class ModernWindow : Window
{
    public static readonly DependencyProperty HeaderSubtitleProperty = DependencyProperty.Register(
        nameof(HeaderSubtitle),
        typeof(string),
        typeof(ModernWindow),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HeaderContentProperty = DependencyProperty.Register(
        nameof(HeaderContent),
        typeof(object),
        typeof(ModernWindow),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ShowTitleBarProperty = DependencyProperty.Register(
        nameof(ShowTitleBar),
        typeof(bool),
        typeof(ModernWindow),
        new PropertyMetadata(true, OnChromePropertyChanged));

    public static readonly DependencyProperty ShowMinimizeButtonProperty = DependencyProperty.Register(
        nameof(ShowMinimizeButton),
        typeof(bool),
        typeof(ModernWindow),
        new PropertyMetadata(true));

    public static readonly DependencyProperty ShowMaximizeButtonProperty = DependencyProperty.Register(
        nameof(ShowMaximizeButton),
        typeof(bool),
        typeof(ModernWindow),
        new PropertyMetadata(true));

    public static readonly DependencyProperty BackdropKindProperty = DependencyProperty.Register(
        nameof(BackdropKind),
        typeof(WindowBackdropKind),
        typeof(ModernWindow),
        new PropertyMetadata(WindowBackdropKind.Mica, OnChromePropertyChanged));

    public static readonly DependencyProperty CloseOnEscapeProperty = DependencyProperty.Register(
        nameof(CloseOnEscape),
        typeof(bool),
        typeof(ModernWindow),
        new PropertyMetadata(false));

    private Border? rootChromeBorder;
    private WpfButton? minimizeButton;
    private WpfButton? maximizeButton;
    private WpfButton? closeButton;
    private bool usingNativeBackdrop;

    public ModernWindow()
    {
        WpfThemeManager.ApplyTheme(this);
        SetResourceReference(StyleProperty, "ModernWindowStyle");
        ApplyWindowChrome();
        StateChanged += OnWindowStateChanged;
        PreviewKeyDown += OnWindowPreviewKeyDown;
        Loaded += (_, _) =>
        {
            EnsureNativeTitlebarContentsRemoved();
            WpfWindowInterop.FitToWorkingArea(this);
        };
    }

    public string HeaderSubtitle
    {
        get => (string)GetValue(HeaderSubtitleProperty);
        set => SetValue(HeaderSubtitleProperty, value);
    }

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public bool ShowTitleBar
    {
        get => (bool)GetValue(ShowTitleBarProperty);
        set => SetValue(ShowTitleBarProperty, value);
    }

    public bool ShowMinimizeButton
    {
        get => (bool)GetValue(ShowMinimizeButtonProperty);
        set => SetValue(ShowMinimizeButtonProperty, value);
    }

    public bool ShowMaximizeButton
    {
        get => (bool)GetValue(ShowMaximizeButtonProperty);
        set => SetValue(ShowMaximizeButtonProperty, value);
    }

    public WindowBackdropKind BackdropKind
    {
        get => (WindowBackdropKind)GetValue(BackdropKindProperty);
        set => SetValue(BackdropKindProperty, value);
    }

    public bool CloseOnEscape
    {
        get => (bool)GetValue(CloseOnEscapeProperty);
        set => SetValue(CloseOnEscapeProperty, value);
    }

    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    public override void OnApplyTemplate()
    {
        DetachTemplateButtons();
        base.OnApplyTemplate();

        rootChromeBorder = GetTemplateChild("PART_RootBorder") as Border;
        minimizeButton = GetTemplateChild("PART_MinimizeButton") as WpfButton;
        maximizeButton = GetTemplateChild("PART_MaximizeButton") as WpfButton;
        closeButton = GetTemplateChild("PART_CloseButton") as WpfButton;

        if (minimizeButton is not null)
        {
            minimizeButton.Click += OnMinimizeClick;
        }

        if (maximizeButton is not null)
        {
            maximizeButton.Click += OnMaximizeClick;
        }

        if (closeButton is not null)
        {
            closeButton.Click += OnCloseClick;
        }

        UpdateChromeBorderThickness();
        UpdateMaximizedLayout();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        UpdateTextFormattingForDpi(VisualTreeHelper.GetDpi(this));
        RefreshWindowChromeAppearance();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateTextFormattingForDpi(newDpi);
        if (IsLoaded)
        {
            WpfWindowInterop.FitToWorkingArea(this);
        }
    }

    private void UpdateTextFormattingForDpi(DpiScale dpi)
    {
        // On HiDPI (>100%, e.g. 125%/150%/200%), Ideal DirectWrite subpixel vector rendering is
        // dramatically sharper than legacy 96-DPI GDI Display mode; use Display only at 100% DPI.
        TextFormattingMode mode = dpi.DpiScaleX > 1.05
            ? TextFormattingMode.Ideal
            : TextFormattingMode.Display;
        TextOptions.SetTextFormattingMode(this, mode);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
    }

    internal void RefreshWindowChromeAppearance()
    {
        bool wantBackdrop = ApplyWindowChrome();

        IntPtr hwnd = Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // Strip WS_SYSMENU so Win32 DWM does not draw duplicate native caption buttons over our custom title bar.
        _ = DwmWindowNative.RemoveWindowTitlebarContents(hwnd);
        _ = DwmWindowNative.ApplyDarkMode(hwnd, enabled: true);
        _ = DwmWindowNative.ApplyRoundedCorners(hwnd, rounded: true);

        WpfColor borderColor = WpfThemeManager.ResolveDwmBorderColor(this);
        _ = DwmWindowNative.ApplyBorderColor(hwnd, borderColor.R, borderColor.G, borderColor.B);

        if (wantBackdrop && DwmWindowNative.ApplySystemBackdrop(hwnd, BackdropKind))
        {
            usingNativeBackdrop = true;
            Background = WpfBrushes.Transparent;
        }
        else
        {
            usingNativeBackdrop = false;
            SetResourceReference(BackgroundProperty, "WindowBgBrush");
        }

        UpdateChromeBorderThickness();
    }

    private void EnsureNativeTitlebarContentsRemoved()
    {
        IntPtr hwnd = Handle;
        if (hwnd != IntPtr.Zero)
        {
            _ = DwmWindowNative.RemoveWindowTitlebarContents(hwnd);
        }
    }

    private bool ApplyWindowChrome()
    {
        double captionHeight = ShowTitleBar && TryFindResource("TitleBarHeight") is double h ? h : 0d;
        bool wantBackdrop = BackdropKind != WindowBackdropKind.None && DwmWindowNative.SupportsSystemBackdrop;
        Thickness resizeThickness = ResizeMode == ResizeMode.NoResize ? new Thickness(0) : new Thickness(6);
        Thickness glassThickness = wantBackdrop ? new Thickness(-1) : new Thickness(0, 0, 0, 1);

        WindowChrome? chrome = WindowChrome.GetWindowChrome(this);
        if (chrome is null)
        {
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = captionHeight,
                ResizeBorderThickness = resizeThickness,
                GlassFrameThickness = glassThickness,
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });
        }
        else
        {
            chrome.CaptionHeight = captionHeight;
            chrome.ResizeBorderThickness = resizeThickness;
            chrome.GlassFrameThickness = glassThickness;
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
        }

        return wantBackdrop;
    }

    private void UpdateChromeBorderThickness()
    {
        if (rootChromeBorder is null)
        {
            return;
        }

        // Windows 11 DWM already renders the crisp 1px rounded native border.
        rootChromeBorder.BorderThickness = DwmWindowNative.IsWindows11 && usingNativeBackdrop
            ? new Thickness(0)
            : new Thickness(1);
    }

    private static void OnChromePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModernWindow window)
        {
            window.RefreshWindowChromeAppearance();
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizedLayout();
    }

    private void OnWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (CloseOnEscape && e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void UpdateMaximizedLayout()
    {
        if (rootChromeBorder is null)
        {
            return;
        }

        rootChromeBorder.Padding = WindowState == WindowState.Maximized
            ? new Thickness(7)
            : new Thickness(0);

        if (maximizeButton is not null)
        {
            maximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void DetachTemplateButtons()
    {
        if (minimizeButton is not null)
        {
            minimizeButton.Click -= OnMinimizeClick;
        }

        if (maximizeButton is not null)
        {
            maximizeButton.Click -= OnMaximizeClick;
        }

        if (closeButton is not null)
        {
            closeButton.Click -= OnCloseClick;
        }
    }
}
