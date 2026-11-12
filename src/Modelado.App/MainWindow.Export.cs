using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Modelado.Geometry;

namespace Modelado.App;

/// <summary>
/// The Export card (MOD13, after PhotoBoost's PB25): the format chosen up front, only the options
/// that format has, what the model will lose in it, and Save as, shared with the toolbar's button
/// and Ctrl+S. What was written is said on the card.
/// </summary>
public partial class MainWindow
{
    // MOD15: the longest side each kind of file is written at, as last typed: STL in millimetres
    // (100, what slicers expect), every other format in its own units (1, a metre in glTF).
    private string stlSize = "100";
    private string unitSize = "1";

    private string ExportExtension => (string)((ComboBoxItem)ExportFormat.SelectedItem).Tag;

    /// <summary>
    /// The longest side typed, or null when it is not a size above zero. A decimal comma is read
    /// as a decimal point, since the window formats in en-US (MOD49) while a person on a comma
    /// machine types "1,5"; a size has no thousands to separate.
    /// </summary>
    private float? ExportSizeValue =>
        double.TryParse(ExportSize.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double size)
        && size > 0 && size <= 1e6 ? (float)size : null;

    private void OnExportFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ExportSize is not null)
        {
            ExportSize.Text = ExportExtension == "stl" ? stlSize : unitSize;
        }

        UpdateExportCard();
    }

    private void OnExportOptionChanged(object sender, RoutedEventArgs e) => UpdateExportCard();

    private void OnExportSizeChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized || ExportFormat?.SelectedItem is null)
        {
            return;
        }

        if (ExportExtension == "stl")
        {
            stlSize = ExportSize.Text;
        }
        else
        {
            unitSize = ExportSize.Text;
        }

        UpdateExportCard();
        CommandManager.InvalidateRequerySuggested();
    }

    // The box the typed size gives the model shown, as it will be written.
    private void ShowExportDimensions(string unit)
    {
        ExportSizeUnit.Text = unit;
        if (ExportSizeValue is not { } size)
        {
            ExportDimensions.Text = Strings.ExportSizeInvalid;
            return;
        }

        if (mesh is null)
        {
            ExportDimensions.Text = "";
            return;
        }

        (System.Numerics.Vector3 min, System.Numerics.Vector3 max) = mesh.Placed(size).Bounds();
        System.Numerics.Vector3 box = max - min;
        ExportDimensions.Text = Ui.Format(Strings.ExportBox, box.X, box.Y, box.Z, unit);
    }

    // Shows the options the chosen format has and what the model shown would lose in it.
    private void UpdateExportCard()
    {
        if (!IsInitialized || ExportNote is null)
        {
            return;
        }

        string format = ExportExtension;
        ExportObjOptions.Visibility = format == "obj" && mesh?.Texture is not null ? Visibility.Visible : Visibility.Collapsed;
        ExportPlyOptions.Visibility = format == "ply" ? Visibility.Visible : Visibility.Collapsed;
        ExportGlbOptions.Visibility = format == "glb" ? Visibility.Visible : Visibility.Collapsed;

        ShowExportDimensions(SizeUnit(format));
        List<string> notes = [];
        bool coloured = mesh is not null && (mesh.Colors is not null || mesh.Texture is not null);
        if (format == "stl")
        {
            notes.Add(Strings.ExportNoteStlScale);
            if (coloured)
            {
                notes.Add(Strings.ExportNoteStlColours);
            }

            if (mesh is { DoubleSided: true })
            {
                notes.Add(Strings.ExportNoteStlOpen);
            }
        }
        else if (format == "ply" && mesh?.Texture is not null)
        {
            notes.Add(Strings.ExportNotePlyTexture);
        }
        else if (mesh is not null && !coloured)
        {
            notes.Add(Strings.ExportNoteGrey);
        }

        ExportNote.Text = string.Join(" ", notes);
        ExportNote.Visibility = notes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // The unit the longest side is typed in: millimetres for STL, the format's own units otherwise.
    private static string SizeUnit(string format) => format == "stl" ? Strings.UnitMillimetres : Strings.UnitUnits;

    private void OnExport(object sender, ExecutedRoutedEventArgs e)
    {
        if (mesh is null || busy || ExportSizeValue is not { } size)
        {
            return;
        }

        string format = ExportExtension;
        SaveFileDialog dialog = new()
        {
            Title = Strings.SaveModelDialogTitle,
            FileName = Path.GetFileNameWithoutExtension(photoPath ?? Strings.DefaultModelName),
            Filter = format switch
            {
                "obj" => Strings.SaveFilterObj,
                "ply" => Strings.SaveFilterPly,
                "stl" => Strings.SaveFilterStl,
                _ => Strings.SaveFilterGlb,
            },
            DefaultExt = "." + format,
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        // The card's format, whatever extension was typed.
        string path = Path.ChangeExtension(dialog.FileName, "." + format);
        ExportOptions choices = new(ObjVertexColours: ExportObjVertexColours.IsChecked == true, PlyAscii: ExportPlyAscii.IsChecked == true, GlbCompact: ExportGlbCompact.IsChecked == true);
        try
        {
            // Scaled at export only, through Mesh.Placed as the CLI's --size is, so the two write the
            // same bytes; the history keeps the model as it was.
            IReadOnlyList<string> written = MeshExport.Save(mesh.Placed(size), path, choices);
            string names = string.Join(", ", written.Select(Path.GetFileName));
            string? texture = format == "glb" ? MeshExport.GlbTextureFormat(mesh) : null;
            bool compact = format == "glb" && choices.GlbCompact;
            ExportStatus.Text = (texture, compact) switch
            {
                (null, false) => Ui.Format(Strings.ExportSaved, names),
                (null, true) => Ui.Format(Strings.ExportSavedCompact, names),
                (_, false) => Ui.Format(Strings.ExportSavedTexture, names, texture),
                _ => Ui.Format(Strings.ExportSavedCompactTexture, names, texture),
            };
            savedVersion = shownVersion;
            Remember(path);
            LastRunSaid(Ui.Format(Strings.ExportSavedSize, names, size, SizeUnit(format)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ExportStatus.Text = Ui.Format(Strings.NotSavedBecause, Path.GetFileName(path), error.Message);
        }

        ExportStatus.Visibility = Visibility.Visible;
    }
}
