using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class MainForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(18, 18, 18);
    private static readonly Color RowColor = Color.FromArgb(24, 24, 24);
    private static readonly Color CurrentRowColor = Color.FromArgb(30, 58, 95);
    private static readonly Color CompletedRowColor = Color.FromArgb(22, 38, 28);
    private static readonly Color SkippedRowColor = Color.FromArgb(32, 32, 32);
    private static readonly Color TextColor = Color.Gainsboro;
    private static readonly Color MutedTextColor = Color.FromArgb(145, 145, 145);
    private static readonly Color TimerColor = Color.FromArgb(234, 234, 234);
    private static readonly Color SplitTimeColor = Color.FromArgb(166, 235, 166);

    private readonly SplitTimer runTimer = new();
    private readonly BossSplitTracker splitTracker = new();
    private readonly List<BossSplitRecord> splitRecords = new();
    private readonly TerrariaWorldWatcher watcher = new();
    private readonly System.Windows.Forms.Timer uiTimer = new();

    private readonly Label timerLabel = new();
    private readonly TableLayoutPanel splitTable = new();
    private readonly List<Label> nameLabels = new();
    private readonly List<Label> timeLabels = new();

    private TerrariaWatchSnapshot snapshot =
        new(false, null, false, null, TerrariaBossStates.Unknown, false, "waiting for Terraria.exe");

    public MainForm()
    {
        Text = "TerrariaSplit";
        BackColor = BackgroundColor;
        ForeColor = TextColor;
        MinimumSize = new Size(320, 420);
        Size = new Size(360, 520);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        BuildLayout();

        uiTimer.Interval = 50;
        uiTimer.Tick += (_, _) => Tick();
        uiTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        uiTimer.Stop();
        watcher.Dispose();
        base.OnFormClosed(e);
    }

    private void BuildLayout()
    {
        timerLabel.Dock = DockStyle.Bottom;
        timerLabel.Height = 86;
        timerLabel.Padding = new Padding(8, 0, 12, 8);
        timerLabel.TextAlign = ContentAlignment.MiddleRight;
        timerLabel.Font = new Font("Segoe UI", 34f, FontStyle.Bold);
        timerLabel.ForeColor = TimerColor;
        timerLabel.BackColor = Color.Black;
        Controls.Add(timerLabel);

        splitTable.Dock = DockStyle.Fill;
        splitTable.BackColor = BackgroundColor;
        splitTable.ColumnCount = 2;
        splitTable.RowCount = BossSplitDefinitions.All.Count;
        splitTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
        splitTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
        splitTable.Padding = new Padding(0, 4, 0, 4);
        Controls.Add(splitTable);

        for (int i = 0; i < BossSplitDefinitions.All.Count; i++)
        {
            splitTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

            Label nameLabel = CreateSplitLabel(ContentAlignment.MiddleLeft);
            nameLabel.Padding = new Padding(10, 0, 4, 0);
            nameLabel.Text = BossSplitDefinitions.All[i].DisplayName;

            Label timeLabel = CreateSplitLabel(ContentAlignment.MiddleRight);
            timeLabel.Padding = new Padding(4, 0, 10, 0);

            splitTable.Controls.Add(nameLabel, 0, i);
            splitTable.Controls.Add(timeLabel, 1, i);
            nameLabels.Add(nameLabel);
            timeLabels.Add(timeLabel);
        }
    }

    private static Label CreateSplitLabel(ContentAlignment alignment)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = alignment,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = TextColor,
            BackColor = RowColor,
            Margin = Padding.Empty
        };
    }

    private void Tick()
    {
        snapshot = watcher.Poll();

        if (Keyboard.PollRPressed())
        {
            runTimer.TogglePause();
        }

        if (Keyboard.PollTPressed() && CanReset(snapshot))
        {
            runTimer.Reset();
            splitTracker.Reset();
            splitRecords.Clear();
        }

        if (snapshot.EnteredWorld && runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            runTimer.Start();
            splitTracker.OnRunStarted(snapshot);
        }

        if (runTimer.Phase == SplitTimerPhase.Running)
        {
            BossSplitRecord? split = splitTracker.Update(snapshot, runTimer.Elapsed);
            if (split is BossSplitRecord record)
            {
                splitRecords.Add(record);
            }
        }

        UpdateView();
    }

    private void UpdateView()
    {
        timerLabel.Text = SplitTimerFormatter.Format(runTimer.Elapsed);
        Text = $"TerrariaSplit - {FormatTimerPhase()} - {FormatWorldState()}";

        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        for (int i = 0; i < statuses.Count; i++)
        {
            BossSplitStatus status = statuses[i];
            bool isCurrent = i == splitTracker.CurrentIndex && runTimer.Phase != SplitTimerPhase.NotStarted;

            Color backColor = RowColor;
            Color foreColor = TextColor;
            Color timeColor = MutedTextColor;

            if (status.IsCompleted)
            {
                backColor = CompletedRowColor;
                timeColor = SplitTimeColor;
            }
            else if (status.IsSkipped)
            {
                backColor = SkippedRowColor;
                foreColor = MutedTextColor;
            }
            else if (isCurrent)
            {
                backColor = CurrentRowColor;
            }

            nameLabels[i].BackColor = backColor;
            nameLabels[i].ForeColor = foreColor;
            timeLabels[i].BackColor = backColor;
            timeLabels[i].ForeColor = timeColor;
            timeLabels[i].Text = FormatSplitTime(status);
        }
    }

    private static string FormatSplitTime(BossSplitStatus status)
    {
        if (status.Time is TimeSpan splitTime)
        {
            return SplitTimerFormatter.Format(splitTime);
        }

        return status.IsSkipped ? "--" : string.Empty;
    }

    private static bool CanReset(TerrariaWatchSnapshot snapshot)
    {
        return snapshot.IsGameMenu != false;
    }

    private string FormatTimerPhase()
    {
        return runTimer.Phase switch
        {
            SplitTimerPhase.NotStarted => "READY",
            SplitTimerPhase.Running => "RUNNING",
            SplitTimerPhase.Paused => "PAUSED",
            _ => "UNKNOWN"
        };
    }

    private string FormatWorldState()
    {
        return snapshot.IsGameMenu switch
        {
            true => "menu",
            false => FormatBossSummary(),
            null => "unknown"
        };
    }

    private string FormatBossSummary()
    {
        return $"Skl:{FormatFlag(snapshot.BossStates.Skeletron)} " +
            $"WoF:{FormatFlag(snapshot.BossStates.WallOfFlesh)} " +
            $"ML:{FormatFlag(snapshot.BossStates.MoonLord)}";
    }

    private static string FormatFlag(bool? value)
    {
        return value switch
        {
            true => "down",
            false => "up",
            null => "?"
        };
    }
}
