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
    private string ExportExtension => (string)((ComboBoxItem)ExportFormat.SelectedItem).Tag;

    private void OnExportFormatChanged(object sender, SelectionChangedEventArgs e) => UpdateExportCard();

    private void OnExportOptionChanged(object sender, RoutedEventArgs e) => UpdateExportCard();

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

        List<string> notes = [];
        bool coloured = mesh is not null && (mesh.Colors is not null || mesh.Texture is not null);
        if (format == "stl")
        {
            notes.Add("Saved at 100 mm on its longest side; the slicer can scale it.");
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
        if (mesh is null || busy)
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
            // Slicers read STL as millimetres: 100 mm on the largest side. Elsewhere 1 unit (a metre in glTF).
            bool stl = format == "stl";
            IReadOnlyList<string> written = MeshExport.Save(mesh.Placed(stl ? 100 : 1), path, choices);
            string names = string.Join(", ", written.Select(Path.GetFileName));
            ExportStatus.Text = $"Saved {names}";
            LastRunSaid($"Saved {names}{(stl ? " · 100 mm on the longest side" : "")}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ExportStatus.Text = $"{Path.GetFileName(path)} could not be saved: {error.Message}";
        }

        ExportStatus.Visibility = Visibility.Visible;
    }
}
