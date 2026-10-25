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
            ExportDimensions.Text = "Enter a size above 0.";
            return;
        }

        if (mesh is null)
        {
            ExportDimensions.Text = "";
            return;
        }

        (System.Numerics.Vector3 min, System.Numerics.Vector3 max) = mesh.Placed(size).Bounds();
        System.Numerics.Vector3 box = max - min;
        ExportDimensions.Text = string.Create(CultureInfo.CurrentCulture, $"{box.X:0.##} × {box.Y:0.##} × {box.Z:0.##} {unit} (width × height × depth)");
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

        ShowExportDimensions(format == "stl" ? "mm" : "units");
        List<string> notes = [];
        bool coloured = mesh is not null && (mesh.Colors is not null || mesh.Texture is not null);
        if (format == "stl")
        {
            notes.Add("STL is read in millimetres, so the size above is the print's.");
            if (coloured)
            {
                notes.Add("STL keeps the shape alone, so the colours are dropped.");
            }

            if (mesh is { DoubleSided: true })
            {
                notes.Add("The mesh has open edges, so it is not watertight for printing: Fill holes closes them.");
            }
        }
        else if (format == "ply" && mesh?.Texture is not null)
        {
            notes.Add("PLY has no texture, so its colours are sampled at each vertex.");
        }
        else if (mesh is not null && !coloured)
        {
            notes.Add("The model has no colours, so it is saved grey.");
        }

        ExportNote.Text = string.Join(" ", notes);
        ExportNote.Visibility = notes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnExport(object sender, ExecutedRoutedEventArgs e)
    {
        if (mesh is null || busy || ExportSizeValue is not { } size)
        {
            return;
        }

        string format = ExportExtension;
        SaveFileDialog dialog = new()
        {
            Title = "Save the 3D model",
            FileName = Path.GetFileNameWithoutExtension(photoPath ?? "model"),
            Filter = format switch
            {
                "obj" => "OBJ|*.obj",
                "ply" => "PLY|*.ply",
                "stl" => "STL, in millimetres|*.stl",
                _ => "GLB, binary glTF|*.glb",
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
        ExportOptions choices = new(ObjVertexColours: ExportObjVertexColours.IsChecked == true, PlyAscii: ExportPlyAscii.IsChecked == true);
        try
        {
            // Scaled at export only, through Mesh.Placed as the CLI's --size is, so the two write the
            // same bytes; the history keeps the model as it was.
            IReadOnlyList<string> written = MeshExport.Save(mesh.Placed(size), path, choices);
            string names = string.Join(", ", written.Select(Path.GetFileName));
            ExportStatus.Text = $"Saved {names}";
            savedVersion = shownVersion;
            LastRunSaid(string.Create(CultureInfo.CurrentCulture, $"Saved {names} · {size:0.##} {(format == "stl" ? "mm" : "units")} on the longest side"));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ExportStatus.Text = $"{Path.GetFileName(path)} could not be saved: {error.Message}";
        }

        ExportStatus.Visibility = Visibility.Visible;
    }
}
