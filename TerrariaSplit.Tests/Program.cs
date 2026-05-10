using System.Drawing;
using TerrariaSplit;

var tests = new (string Name, Action Test)[]
{
    ("SignaturePattern matches wildcard bytes", TestSignaturePatternWildcard),
    ("SplitTimerFormatter formats minute and hour values", TestSplitTimerFormatter),
    ("SplitTimer clamps practice time at zero", TestSplitTimerPracticeClamp),
    ("BossRouteGroups groups enabled entries by segment", TestBossRouteGroups),
    ("TerrariaMenuGeometry maps 900p menu coordinates", TestTerrariaMenuGeometry),
    ("Localizer returns English fallback and Chinese Crimson", TestLocalizer),
    ("SettingsNormalizer clamps auto-create timings", TestSettingsNormalize)
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
    AssertEqual("1:02:03.04", SplitTimerFormatter.Format(new TimeSpan(0, 1, 2, 3, 40)));
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
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}
