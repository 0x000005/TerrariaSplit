using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class SettingsForm : Form
{

    internal void AddColorSection(TableLayoutPanel parent)
    {
        TableLayoutPanel textSection = CreateSection("UI Colors");
        TableLayoutPanel textGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(214f),
            ColumnStyleAbsolute(214f),
            ColumnStyleAbsolute(214f));

        AddHeaderRow(textGrid, "Text type", "Text", "Outline", "Shadow");
        AddTextColorRow(textGrid, "Reference text",
            nameof(settings.Colors.ReferenceText), settings.Colors.ReferenceText,
            nameof(settings.Colors.ReferenceTextOutline), settings.Colors.ReferenceTextOutline,
            nameof(settings.Colors.ReferenceTextShadow), settings.Colors.ReferenceTextShadow);
        AddTextColorRow(textGrid, "Active reference text",
            nameof(settings.Colors.ActiveReferenceText), settings.Colors.ActiveReferenceText,
            nameof(settings.Colors.ActiveReferenceTextOutline), settings.Colors.ActiveReferenceTextOutline,
            nameof(settings.Colors.ActiveReferenceTextShadow), settings.Colors.ActiveReferenceTextShadow);
        AddTextColorRow(textGrid, "Completed split text",
            nameof(settings.Colors.SplitText), settings.Colors.SplitText,
            nameof(settings.Colors.SplitTextOutline), settings.Colors.SplitTextOutline,
            nameof(settings.Colors.SplitTextShadow), settings.Colors.SplitTextShadow);
        AddTextColorRow(textGrid, "Delta ahead text",
            nameof(settings.Colors.DeltaAheadText), settings.Colors.DeltaAheadText,
            nameof(settings.Colors.DeltaAheadTextOutline), settings.Colors.DeltaAheadTextOutline,
            nameof(settings.Colors.DeltaAheadTextShadow), settings.Colors.DeltaAheadTextShadow);
        AddTextColorRow(textGrid, "Delta behind text",
            nameof(settings.Colors.DeltaBehindText), settings.Colors.DeltaBehindText,
            nameof(settings.Colors.DeltaBehindTextOutline), settings.Colors.DeltaBehindTextOutline,
            nameof(settings.Colors.DeltaBehindTextShadow), settings.Colors.DeltaBehindTextShadow);
        AddTextColorRow(textGrid, "Timer text",
            nameof(settings.Colors.TimerText), settings.Colors.TimerText,
            nameof(settings.Colors.TimerTextOutline), settings.Colors.TimerTextOutline,
            nameof(settings.Colors.TimerTextShadow), settings.Colors.TimerTextShadow);
        AddTextColorRow(textGrid, "Timer ahead text",
            nameof(settings.Colors.TimerAheadText), settings.Colors.TimerAheadText,
            nameof(settings.Colors.TimerAheadTextOutline), settings.Colors.TimerAheadTextOutline,
            nameof(settings.Colors.TimerAheadTextShadow), settings.Colors.TimerAheadTextShadow);
        AddTextColorRow(textGrid, "Timer behind text",
            nameof(settings.Colors.TimerBehindText), settings.Colors.TimerBehindText,
            nameof(settings.Colors.TimerBehindTextOutline), settings.Colors.TimerBehindTextOutline,
            nameof(settings.Colors.TimerBehindTextShadow), settings.Colors.TimerBehindTextShadow);
        AddTextColorRow(textGrid, "Timer record text",
            nameof(settings.Colors.TimerRecordText), settings.Colors.TimerRecordText,
            nameof(settings.Colors.TimerRecordTextOutline), settings.Colors.TimerRecordTextOutline,
            nameof(settings.Colors.TimerRecordTextShadow), settings.Colors.TimerRecordTextShadow);
        AddTextColorRow(textGrid, "Timer no record text",
            nameof(settings.Colors.TimerNoRecordText), settings.Colors.TimerNoRecordText,
            nameof(settings.Colors.TimerNoRecordTextOutline), settings.Colors.TimerNoRecordTextOutline,
            nameof(settings.Colors.TimerNoRecordTextShadow), settings.Colors.TimerNoRecordTextShadow);
        AddTextColorRow(textGrid, "Timer paused text",
            nameof(settings.Colors.TimerPausedText), settings.Colors.TimerPausedText,
            nameof(settings.Colors.TimerPausedTextOutline), settings.Colors.TimerPausedTextOutline,
            nameof(settings.Colors.TimerPausedTextShadow), settings.Colors.TimerPausedTextShadow);

        AddSectionControl(textSection, textGrid);
        AddSection(parent, textSection);

        TableLayoutPanel animationSection = CreateSection("Animation Colors");
        TableLayoutPanel animationGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(214f));

        AddHeaderRow(animationGrid, "Text type", "Text");
        AddColorRow(animationGrid, "Animation text",
            nameof(settings.Colors.SplitCompletionLabelText), settings.Colors.SplitCompletionLabelText);
        AddColorRow(animationGrid, "Animation main time",
            nameof(settings.Colors.SplitCompletionTimeText), settings.Colors.SplitCompletionTimeText);

        AddSectionControl(animationSection, animationGrid);
        AddSection(parent, animationSection);
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
