using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class SettingsForm : Form
{

    internal void AddColorSection(TableLayoutPanel parent)
    {
        TableLayoutPanel textSection = CreateSection("Text Colors");
        TableLayoutPanel textGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f),
            ColumnStyleAbsolute(64f));

        AddColorRow(textGrid, "Reference text", nameof(settings.Colors.ReferenceText), settings.Colors.ReferenceText);
        AddColorRow(textGrid, "Active reference text", nameof(settings.Colors.ActiveReferenceText), settings.Colors.ActiveReferenceText);
        AddColorRow(textGrid, "Completed split text", nameof(settings.Colors.SplitText), settings.Colors.SplitText);
        AddColorRow(textGrid, "Delta ahead text", nameof(settings.Colors.DeltaAheadText), settings.Colors.DeltaAheadText);
        AddColorRow(textGrid, "Delta behind text", nameof(settings.Colors.DeltaBehindText), settings.Colors.DeltaBehindText);
        AddColorRow(textGrid, "Timer text", nameof(settings.Colors.TimerText), settings.Colors.TimerText);
        AddColorRow(textGrid, "Timer ahead text", nameof(settings.Colors.TimerAheadText), settings.Colors.TimerAheadText);
        AddColorRow(textGrid, "Timer behind text", nameof(settings.Colors.TimerBehindText), settings.Colors.TimerBehindText);
        AddColorRow(textGrid, "Timer record text", nameof(settings.Colors.TimerRecordText), settings.Colors.TimerRecordText);
        AddColorRow(textGrid, "Timer no record text", nameof(settings.Colors.TimerNoRecordText), settings.Colors.TimerNoRecordText);
        AddColorRow(textGrid, "Timer paused text", nameof(settings.Colors.TimerPausedText), settings.Colors.TimerPausedText);

        AddSectionControl(textSection, textGrid);
        AddSection(parent, textSection);
    }


    internal void AddSoundSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Sounds");
        TableLayoutPanel grid = CreateGrid(
            ColumnStyleAbsolute(360f),
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(152f),
            ColumnStyleAbsolute(144f));

        AddSoundRow(grid, "Pause sound", nameof(settings.Sounds.Pause), settings.Sounds.Pause);
        AddSoundRow(grid, "Reset sound", nameof(settings.Sounds.Reset), settings.Sounds.Reset);
        AddSoundRow(grid, "Split: total slower, segment slower", nameof(settings.Sounds.SplitBehindReferenceBehindSegment), settings.Sounds.SplitBehindReferenceBehindSegment);
        AddSoundRow(grid, "Split: total slower, segment not slower", nameof(settings.Sounds.SplitBehindReferenceAheadSegment), settings.Sounds.SplitBehindReferenceAheadSegment);
        AddSoundRow(grid, "Split: total not slower, segment slower", nameof(settings.Sounds.SplitAheadReferenceBehindSegment), settings.Sounds.SplitAheadReferenceBehindSegment);
        AddSoundRow(grid, "Split: total not slower, segment not slower", nameof(settings.Sounds.SplitAheadReferenceAheadSegment), settings.Sounds.SplitAheadReferenceAheadSegment);

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }
}
