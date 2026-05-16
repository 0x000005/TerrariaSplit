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
    ("SettingsNormalizer clamps text effects", TestSettingsNormalizeTextEffects),
    ("Hotkey validator rejects reserved keys", TestHotkeyValidatorRejectsReservedKeys),
    ("AppSettings falls back from invalid hotkeys", TestAppSettingsInvalidHotkeyFallback),
    ("AppSettings uses PB as reference time", TestAppSettingsUsesPersonalBestAsReferenceTime),
    ("Settings form orders moved pages", TestSettingsFormOrdersMovedPages),
    ("Settings form applies global scale from General page", TestSettingsFormAppliesGlobalScaleFromGeneralPage),
    ("Settings form applies dynamic delta units from UI page", TestSettingsFormAppliesDynamicDeltaUnitsFromUiPage),
    ("Settings form applies text effects from UI page", TestSettingsFormAppliesTextEffectsFromUiPage),
    ("Settings form locks reference controls when PB reference is enabled", TestSettingsFormLocksReferenceControlsForPersonalBestReference),
    ("Settings form applies text outline and shadow colors", TestSettingsFormAppliesTextOutlineAndShadowColors),
    ("Main form preserves size when applying non-layout settings", TestMainFormPreservesSizeWhenApplyingNonLayoutSettings),
    ("Main form scales size when global scale changes", TestMainFormScalesSizeWhenGlobalScaleChanges),
    ("Main form adjusts width when split columns change", TestMainFormAdjustsWidthWhenSplitColumnsChange),
    ("Settings form applies current delta gradient option", TestSettingsFormAppliesCurrentDeltaGradientOption),
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

static void TestSettingsNormalizeTextEffects()
{
    var settings = new AppSettings
    {
        TextEffects = new UiTextEffectSettings
        {
            TimeShadowPercent = -1,
            TimeOutlineThicknessPercent = 101,
            DeltaShadowPercent = -99,
            DeltaOutlineThicknessPercent = 900,
            TimerShadowPercent = -1,
            TimerOutlineThicknessPercent = 101,
            TimerMillisecondsShadowPercent = 42,
            TimerMillisecondsOutlineThicknessPercent = 77
        }
    };

    SettingsNormalizer.Normalize(settings);
    AssertEqual(0, settings.TextEffects.TimeShadowPercent);
    AssertEqual(100, settings.TextEffects.TimeOutlineThicknessPercent);
    AssertEqual(0, settings.TextEffects.DeltaShadowPercent);
    AssertEqual(100, settings.TextEffects.DeltaOutlineThicknessPercent);
    AssertEqual(0, settings.TextEffects.TimerShadowPercent);
    AssertEqual(100, settings.TextEffects.TimerOutlineThicknessPercent);
    AssertEqual(42, settings.TextEffects.TimerMillisecondsShadowPercent);
    AssertEqual(77, settings.TextEffects.TimerMillisecondsOutlineThicknessPercent);

    settings.TextEffects = null!;
    SettingsNormalizer.Normalize(settings);
    AssertEqual(0, settings.TextEffects.TimerShadowPercent);
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

static void TestAppSettingsUsesPersonalBestAsReferenceTime()
{
    var settings = new AppSettings
    {
        UsePersonalBestAsReferenceTime = true,
        ReferenceSplitSets =
        [
            AppSettings.CreateReferenceSet("WR", new Dictionary<string, string>
            {
                ["Skeletron"] = "01:00"
            })
        ],
        PersonalBestTimes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Skeletron"] = "00:30"
        }
    };
    SettingsNormalizer.Normalize(settings);

    var definition = new BossSplitDefinition(
        "Skeletron",
        "Skeletron",
        Array.Empty<BossFlag>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        ["Skeletron"]);

    AssertEqual(AppSettings.PersonalBestReferenceSetName, settings.GetActiveReferenceSet().Name);
    AssertEqual("00:30", settings.GetReferenceText("Skeletron"));
    AssertEqual(true, settings.TryGetReferenceSplit(definition, out TimeSpan split));
    AssertEqual(TimeSpan.FromSeconds(30), split);

    settings.SetReferenceText("Skeletron", "05:00");
    AssertEqual("00:30", settings.GetReferenceText("Skeletron"));
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

static void TestSettingsFormAppliesTextEffectsFromUiPage()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        InvokePrivate(form, "EnsurePageCreated", 3);
        GetPrivateField<TextBox>(form, "timeShadowBox").Text = "25";
        GetPrivateField<TextBox>(form, "timeOutlineThicknessBox").Text = "30";
        GetPrivateField<TextBox>(form, "deltaShadowBox").Text = "35";
        GetPrivateField<TextBox>(form, "deltaOutlineThicknessBox").Text = "40";
        GetPrivateField<TextBox>(form, "timerShadowBox").Text = "25";
        GetPrivateField<TextBox>(form, "timerOutlineThicknessBox").Text = "30";
        GetPrivateField<TextBox>(form, "timerMillisecondsShadowBox").Text = "45";
        GetPrivateField<TextBox>(form, "timerMillisecondsOutlineThicknessBox").Text = "50";

        InvokePrivate(form, "ApplyToSettings");

        AssertEqual(25, form.Result.TextEffects.TimeShadowPercent);
        AssertEqual(30, form.Result.TextEffects.TimeOutlineThicknessPercent);
        AssertEqual(35, form.Result.TextEffects.DeltaShadowPercent);
        AssertEqual(40, form.Result.TextEffects.DeltaOutlineThicknessPercent);
        AssertEqual(25, form.Result.TextEffects.TimerShadowPercent);
        AssertEqual(30, form.Result.TextEffects.TimerOutlineThicknessPercent);
        AssertEqual(45, form.Result.TextEffects.TimerMillisecondsShadowPercent);
        AssertEqual(50, form.Result.TextEffects.TimerMillisecondsOutlineThicknessPercent);
    });
}

static void TestSettingsFormLocksReferenceControlsForPersonalBestReference()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings
        {
            UsePersonalBestAsReferenceTime = true,
            PersonalBestTimes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Skeletron"] = "00:30"
            }
        });
        InvokePrivate(form, "EnsurePageCreated", 2);

        AssertEqual(true, GetPrivateField<CheckBox>(form, "usePersonalBestAsReferenceTimeBox").Checked);
        AssertEqual(false, GetPrivateField<ComboBox>(form, "referenceSetBox").Enabled);
        AssertEqual(false, GetPrivateField<TextBox>(form, "newReferenceSetNameBox").Enabled);

        Dictionary<string, TextBox> splitTextBoxes = GetPrivateField<Dictionary<string, TextBox>>(form, "splitTextBoxes");
        AssertEqual("00:30", splitTextBoxes["Skeletron"].Text);
        AssertEqual(true, splitTextBoxes["Skeletron"].ReadOnly);
    });
}

static void TestSettingsFormAppliesTextOutlineAndShadowColors()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        InvokePrivate(form, "EnsurePageCreated", 7);
        Dictionary<string, TextBox> colorTextBoxes = GetPrivateField<Dictionary<string, TextBox>>(form, "colorTextBoxes");
        colorTextBoxes[nameof(UiColorSettings.ReferenceTextOutline)].Text = "#112233";
        colorTextBoxes[nameof(UiColorSettings.ReferenceTextShadow)].Text = "#445566";
        colorTextBoxes[nameof(UiColorSettings.TimerPausedTextOutline)].Text = "#778899";
        colorTextBoxes[nameof(UiColorSettings.TimerPausedTextShadow)].Text = "#AABBCC";
        colorTextBoxes[nameof(UiColorSettings.SplitCompletionLabelText)].Text = "#DDEEFF";
        colorTextBoxes[nameof(UiColorSettings.SplitCompletionTimeText)].Text = "#FEDCBA";

        InvokePrivate(form, "ApplyToSettings");

        AssertEqual("#112233", form.Result.Colors.ReferenceTextOutline);
        AssertEqual("#445566", form.Result.Colors.ReferenceTextShadow);
        AssertEqual("#778899", form.Result.Colors.TimerPausedTextOutline);
        AssertEqual("#AABBCC", form.Result.Colors.TimerPausedTextShadow);
        AssertEqual("#DDEEFF", form.Result.Colors.SplitCompletionLabelText);
        AssertEqual("#FEDCBA", form.Result.Colors.SplitCompletionTimeText);
    });
}

static void TestMainFormPreservesSizeWhenApplyingNonLayoutSettings()
{
    RunSta(() =>
    {
        using var form = new MainForm();
        var previousSettings = new AppSettings();
        SetMainFormSettings(form, previousSettings);
        InvokePrivate(form, "ApplyLoadedSettings", (object?)null);
        form.Size = new Size(1000, 900);

        var settings = AppSettingsStore.Clone(previousSettings);
        settings.Colors.TimerText = "#123456";
        SetMainFormSettings(form, settings);

        InvokePrivate(form, "ApplyLoadedSettings", previousSettings);

        AssertEqual(new Size(1000, 900), form.Size);
    });
}

static void TestMainFormScalesSizeWhenGlobalScaleChanges()
{
    RunSta(() =>
    {
        using var form = new MainForm();
        var previousSettings = new AppSettings();
        previousSettings.Columns.ScalePercent = 100;
        SetMainFormSettings(form, previousSettings);
        InvokePrivate(form, "ApplyLoadedSettings", (object?)null);
        form.Size = new Size(600, 500);
        Size previousSize = form.Size;

        var settings = AppSettingsStore.Clone(previousSettings);
        settings.Columns.ScalePercent = 150;
        SetMainFormSettings(form, settings);

        InvokePrivate(form, "ApplyLoadedSettings", previousSettings);

        AssertEqual(
            new Size(
                (int)Math.Round(previousSize.Width * 1.5f, MidpointRounding.AwayFromZero),
                (int)Math.Round(previousSize.Height * 1.5f, MidpointRounding.AwayFromZero)),
            form.Size);
    });
}

static void TestMainFormAdjustsWidthWhenSplitColumnsChange()
{
    RunSta(() =>
    {
        using var form = new MainForm();
        var previousSettings = new AppSettings();
        previousSettings.Columns.ScalePercent = 100;
        SetMainFormSettings(form, previousSettings);
        InvokePrivate(form, "ApplyLoadedSettings", (object?)null);

        form.Size = new Size(600, 500);
        var settings = AppSettingsStore.Clone(previousSettings);
        settings.Columns.Time.Width += 100;
        SetMainFormSettings(form, settings);

        InvokePrivate(form, "ApplyLoadedSettings", previousSettings);

        AssertEqual(new Size(700, 500), form.Size);
    });
}

static void TestSettingsFormAppliesCurrentDeltaGradientOption()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings
        {
            EnableDeltaGradientColor = false,
            EnableCurrentDeltaGradientColor = true,
            EnableTimerGradientColor = false
        });
        InvokePrivate(form, "EnsurePageCreated", 4);
        GetPrivateField<CheckBox>(form, "enableCurrentDeltaGradientColorBox").Checked = false;

        InvokePrivate(form, "ApplyToSettings");

        AssertEqual(false, form.Result.EnableDeltaGradientColor);
        AssertEqual(false, form.Result.EnableCurrentDeltaGradientColor);
        AssertEqual(false, form.Result.EnableTimerGradientColor);
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

static void SetMainFormSettings(MainForm form, AppSettings settings)
{
    FieldInfo field = typeof(MainForm).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing MainForm settings field.");
    field.SetValue(form, AppSettingsStore.Clone(settings));
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
