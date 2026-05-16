using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using TerrariaSplit;

var tests = new (string Name, Action Test)[]
{
    ("SignaturePattern matches wildcard bytes", TestSignaturePatternWildcard),
    ("SplitTimerFormatter formats minute and hour values", TestSplitTimerFormatter),
    ("Rolling performance counter keeps a bounded window", TestRollingPerformanceCounter),
    ("SplitTimer clamps practice time at zero", TestSplitTimerPracticeClamp),
    ("BossRouteGroups groups enabled entries by segment", TestBossRouteGroups),
    ("TerrariaMenuGeometry maps 900p menu coordinates", TestTerrariaMenuGeometry),
    ("Localizer returns English fallback and Chinese Crimson", TestLocalizer),
    ("SettingsNormalizer clamps auto-create timings", TestSettingsNormalize),
    ("Hotkey validator rejects reserved keys", TestHotkeyValidatorRejectsReservedKeys),
    ("AppSettings falls back from invalid hotkeys", TestAppSettingsInvalidHotkeyFallback),
    ("Settings form orders moved pages", TestSettingsFormOrdersMovedPages),
    ("Settings form applies global scale from General page", TestSettingsFormAppliesGlobalScaleFromGeneralPage),
    ("Settings form applies dynamic delta units from UI page", TestSettingsFormAppliesDynamicDeltaUnitsFromUiPage),
    ("Settings form applies advanced UI scale patch option", TestSettingsFormAppliesAdvancedUiScalePatchOption),
    ("Settings form keeps uncreated animation fields unchanged", TestSettingsFormKeepsUncreatedAnimationFieldsUnchanged),
    ("Terraria UI scale patch rewrites target IL constants", TestTerrariaUiScalePatchPlan)
};

int failures = 0;
foreach ((string name, Action test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failures > 0)
{
    Environment.ExitCode = 1;
}

static void TestSignaturePatternWildcard()
{
    SignaturePattern pattern = SignaturePattern.Parse("AA ?? CC");
    AssertEqual(1, pattern.FindIn([0x00, 0xAA, 0xBB, 0xCC, 0xDD]));
    AssertEqual(-1, pattern.FindIn([0xAA, 0xBB, 0xCD]));
}

static void TestSplitTimerFormatter()
{
    AssertEqual("01:02.34", SplitTimerFormatter.Format(new TimeSpan(0, 0, 1, 2, 340)));
    AssertEqual("00:00.01", SplitTimerFormatter.Format(TimeSpan.FromMilliseconds(10)));
    AssertEqual("1:02:03.04", SplitTimerFormatter.Format(new TimeSpan(0, 1, 2, 3, 40)));
}

static void TestRollingPerformanceCounter()
{
    var counter = new RollingPerformanceCounter(capacity: 3);
    counter.Record(1);
    counter.Record(2);
    counter.Record(5);
    counter.Record(8);

    AssertEqual(4, counter.TotalCount);
    AssertEqual(3, counter.SampleCount);
    AssertEqual(8d, counter.LastMilliseconds);
    AssertEqual(5d, counter.AverageMilliseconds);
    AssertEqual(8d, counter.MaxMilliseconds);
}

static void TestSplitTimerPracticeClamp()
{
    var timer = new SplitTimer();
    timer.SetPracticeElapsed(TimeSpan.FromSeconds(-5));
    AssertEqual(TimeSpan.Zero, timer.Elapsed);
}

static void TestBossRouteGroups()
{
    var settings = new AppSettings
    {
        Route =
        [
            new BossRouteEntry { BossId = "skeletron", Enabled = true, Segment = 1 },
            new BossRouteEntry { BossId = "wallofflesh", Enabled = false, Segment = 1 },
            new BossRouteEntry { BossId = "destroyer", Enabled = true, Segment = 2 },
            new BossRouteEntry { BossId = "twins", Enabled = true, Segment = 2 }
        ]
    };

    List<RouteGroup> groups = BossRouteGroups.Build(settings);
    AssertEqual(2, groups.Count);
    AssertEqual("skeletron", groups[0].Key);
    AssertEqual("destroyer+twins", groups[1].Key);
}

static void TestTerrariaMenuGeometry()
{
    TerrariaMenuGeometry geometry = TerrariaMenuGeometry.From(new Size(900, 900));
    AssertEqual(new Point(450, 245), geometry.MainMenuSinglePlayer());
    AssertEqual(new Point(580, 534), geometry.CreatePlayerButton());
}

static void TestLocalizer()
{
    AssertEqual("Crimson", Localizer.Get("Crimson", new AppSettings { Language = "English" }));
    AssertEqual("\u7329\u7EA2", Localizer.Get("Crimson", new AppSettings { Language = "\u4E2D\u6587" }));
}

static void TestSettingsNormalize()
{
    var settings = new AppSettings
    {
        AutoCreate = new AutoCreateWorldSettings
        {
            ShortActionDelayMilliseconds = -1,
            MenuActionDelayMilliseconds = 6000,
            WindowActivationDelayMilliseconds = 6000,
            ClickFocusDelayMilliseconds = -10,
            InputPressDurationMilliseconds = 0
        }
    };

    SettingsNormalizer.Normalize(settings);
    AssertEqual(0, settings.AutoCreate.ShortActionDelayMilliseconds);
    AssertEqual(5000, settings.AutoCreate.MenuActionDelayMilliseconds);
    AssertEqual(5000, settings.AutoCreate.WindowActivationDelayMilliseconds);
    AssertEqual(0, settings.AutoCreate.ClickFocusDelayMilliseconds);
    AssertEqual(1, settings.AutoCreate.InputPressDurationMilliseconds);

    settings.Advanced = null!;
    SettingsNormalizer.Normalize(settings);
    AssertEqual(false, settings.Advanced.EnableTerrariaUiScalePatch);
}

static void TestHotkeyValidatorRejectsReservedKeys()
{
    AssertEqual(false, HotkeyKeyValidator.IsAllowed(Keys.ControlKey));
    AssertEqual(false, HotkeyKeyValidator.IsAllowed(Keys.LShiftKey));
    AssertEqual(false, HotkeyKeyValidator.IsAllowed(Keys.RMenu));
    AssertEqual(false, HotkeyKeyValidator.IsAllowed(Keys.LWin));
    AssertEqual(false, HotkeyKeyValidator.IsAllowed(Keys.CapsLock));
    AssertEqual(true, HotkeyKeyValidator.IsAllowed(Keys.A));
    AssertEqual(true, HotkeyKeyValidator.IsAllowed(Keys.F6));
    AssertEqual(false, HotkeyKeyValidator.IsAllowed(Keys.Control | Keys.F6));
}

static void TestAppSettingsInvalidHotkeyFallback()
{
    var settings = new AppSettings
    {
        PauseResumeKey = Keys.ControlKey.ToString(),
        ResetKey = Keys.LShiftKey.ToString(),
        MouseClickThroughKey = Keys.RMenu.ToString(),
        CreateWorldKey = Keys.LWin.ToString()
    };

    AssertEqual(Keys.F12, settings.PauseResumeKeys);
    AssertEqual(Keys.F6, settings.ResetKeys);
    AssertEqual(Keys.F9, settings.MouseClickThroughKeys);
    AssertEqual(Keys.F7, settings.CreateWorldKeys);
}

static void TestSettingsFormAppliesGlobalScaleFromGeneralPage()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        GetPrivateField<TextBox>(form, "globalScaleBox").Text = "175";

        InvokePrivate(form, "ApplyToSettings");

        AssertEqual(175, form.Result.Columns.ScalePercent);
    });
}

static void TestSettingsFormOrdersMovedPages()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        var pages = (System.Collections.IEnumerable)GetPrivateField<object>(form, "pages");
        var labels = new List<string>();

        foreach (object page in pages)
        {
            PropertyInfo navProperty = page.GetType().GetProperty(
                    "Nav",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing settings page nav property.");
            var nav = (Button)(navProperty.GetValue(page)
                ?? throw new InvalidOperationException("Settings page nav is null."));
            labels.Add(nav.Text);
        }

        AssertEqual(
            "General|BOSS|Data|UI|Effects|Create World|Sounds|Colors|Advanced|Debug",
            string.Join('|', labels));
    });
}

static void TestSettingsFormAppliesDynamicDeltaUnitsFromUiPage()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings { EnableDynamicDeltaTimeUnits = true });
        InvokePrivate(form, "EnsurePageCreated", 3);
        GetPrivateField<CheckBox>(form, "enableDynamicDeltaTimeUnitsBox").Checked = false;

        InvokePrivate(form, "ApplyToSettings");

        AssertEqual(false, form.Result.EnableDynamicDeltaTimeUnits);
    });
}

static void TestSettingsFormKeepsUncreatedAnimationFieldsUnchanged()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            UndefeatedIconGrayscalePercent = 22,
            UndefeatedIconBrightnessPercent = 73,
            CurrentBossIconGrayscaleWeakenPercent = 11,
            CurrentBossIconBrightnessBoostPercent = 64
        };
        using var form = new SettingsForm(settings);
        InvokePrivate(form, "EnsurePageCreated", 3);

        InvokePrivate(form, "ApplyToSettings");

        AssertEqual(22, form.Result.UndefeatedIconGrayscalePercent);
        AssertEqual(73, form.Result.UndefeatedIconBrightnessPercent);
        AssertEqual(11, form.Result.CurrentBossIconGrayscaleWeakenPercent);
        AssertEqual(64, form.Result.CurrentBossIconBrightnessBoostPercent);
    });
}

static void TestSettingsFormAppliesAdvancedUiScalePatchOption()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        InvokePrivate(form, "EnsurePageCreated", 8);
        GetPrivateField<CheckBox>(form, "enableTerrariaUiScalePatchBox").Checked = true;

        InvokePrivate(form, "ApplyToSettings");

        AssertEqual(true, form.Result.Advanced.EnableTerrariaUiScalePatch);
    });
}

static void TestTerrariaUiScalePatchPlan()
{
    IReadOnlyList<byte[]> originalPatterns = GetUiScalePatchPatterns("OriginalPattern");
    IReadOnlyList<byte[]> patchedPatterns = GetUiScalePatchPatterns("PatchedPattern");
    byte[] buffer = BuildPatternBuffer(originalPatterns);

    TerrariaUiScalePatchPlan plan = TerrariaUiScalePatch.CreatePlan(buffer, IntPtr.Zero);
    AssertEqual(true, plan.CanApply);
    AssertEqual(false, plan.AlreadyApplied);
    AssertEqual(originalPatterns.Count, plan.Writes.Count);

    byte[] patched = TerrariaUiScalePatch.ApplyToBufferForTest(buffer);
    TerrariaUiScalePatchPlan patchedPlan = TerrariaUiScalePatch.CreatePlan(patched, IntPtr.Zero);
    AssertEqual(true, patchedPlan.CanApply);
    AssertEqual(true, patchedPlan.AlreadyApplied);

    foreach (byte[] pattern in patchedPatterns)
    {
        AssertEqual(true, ContainsPattern(patched, pattern));
    }
}

static IReadOnlyList<byte[]> GetUiScalePatchPatterns(string propertyName)
{
    FieldInfo operationsField = typeof(TerrariaUiScalePatch).GetField(
            "Operations",
            BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing Terraria UI scale patch operations.");
    var operations = (Array)(operationsField.GetValue(null)
        ?? throw new InvalidOperationException("Terraria UI scale patch operations are null."));
    var patterns = new List<byte[]>();

    foreach (object operation in operations)
    {
        PropertyInfo property = operation.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing patch operation property {propertyName}.");
        patterns.Add((byte[])(property.GetValue(operation)
            ?? throw new InvalidOperationException($"Patch operation property {propertyName} is null.")));
    }

    return patterns;
}

static byte[] BuildPatternBuffer(IReadOnlyList<byte[]> patterns)
{
    var bytes = new List<byte>();
    for (int index = 0; index < patterns.Count; index++)
    {
        bytes.Add(0x41);
        bytes.Add((byte)index);
        bytes.Add(0x52);
        bytes.AddRange(patterns[index]);
        bytes.Add(0x7F);
    }

    return bytes.ToArray();
}

static bool ContainsPattern(byte[] buffer, byte[] pattern)
{
    if (pattern.Length == 0 || buffer.Length < pattern.Length)
    {
        return false;
    }

    for (int start = 0; start <= buffer.Length - pattern.Length; start++)
    {
        bool matches = true;
        for (int index = 0; index < pattern.Length; index++)
        {
            if (buffer[start + index] != pattern[index])
            {
                matches = false;
                break;
            }
        }

        if (matches)
        {
            return true;
        }
    }

    return false;
}

static void RunSta(Action action)
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
    {
        throw failure;
    }
}

static T GetPrivateField<T>(object target, string name)
{
    FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Missing private field {name}.");
    return (T)(field.GetValue(target) ?? throw new InvalidOperationException($"Private field {name} is null."));
}

static object? InvokePrivate(object target, string name, params object?[] args)
{
    MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Missing private method {name}.");

    try
    {
        return method.Invoke(target, args);
    }
    catch (TargetInvocationException ex) when (ex.InnerException is not null)
    {
        throw ex.InnerException;
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}
