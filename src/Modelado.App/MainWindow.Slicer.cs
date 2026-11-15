using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Modelado.Geometry;
using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// The Export card's "Open in slicer" (MOD110), beside Open in Blender: shown only where a slicer is
/// found (<see cref="SlicerLauncher"/>), it hands the slicer the model shown as a 3MF at the size the
/// card would save it for printing. With more than one slicer a list picks which, and the one picked
/// is remembered in settings.json for the next session.
/// </summary>
public partial class MainWindow
{
    private const string SlicerKey = "slicer";

    // Looked for once, as Blender is: slicers do not come and go while the window is open.
    private readonly Lazy<IReadOnlyList<Slicer>> slicers = new(SlicerLauncher.Find);

    private bool slicerListFilled;

    private Slicer? SlicerChosen => (ExportSlicer.SelectedItem as ComboBoxItem)?.Tag as Slicer;

    private void UpdateOpenInSlicer()
    {
        IReadOnlyList<Slicer> found = slicers.Value;
        if (!slicerListFilled)
        {
            // Filled before it is marked filled, so the selection made here is not written back.
            string? kept = SettingsFile.Read(SettingsFile.DefaultPath, SlicerKey)?.GetValue<string>();
            foreach (Slicer slicer in found)
            {
                ExportSlicer.Items.Add(new ComboBoxItem { Content = slicer.Name, Tag = slicer });
            }

            ExportSlicer.SelectedIndex = found.Count == 0 ? -1 : Math.Max(0, found.ToList().FindIndex(s => s.Name == kept));
            slicerListFilled = true;
        }

        bool offered = mesh is not null && found.Count > 0;
        OpenInSlicer.Visibility = offered ? Visibility.Visible : Visibility.Collapsed;
        ExportSlicer.Visibility = offered && found.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        OpenInSlicer.Content = SlicerChosen is { } chosen ? Ui.Format(Strings.OpenInSlicer, chosen.Name) : null;
    }

    private void OnExportSlicerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!slicerListFilled || SlicerChosen is not { } chosen)
        {
            return;
        }

        SettingsFile.Write(SettingsFile.DefaultPath, SlicerKey, JsonValue.Create(chosen.Name));
        UpdateOpenInSlicer();
    }

    private void OnOpenInSlicer(object sender, RoutedEventArgs e)
    {
        if (busy || mesh is null || SlicerChosen is not { } slicer)
        {
            return;
        }

        // The size Save as writes an STL or a 3MF at; one not typed as a size keeps the model's own.
        float? size = double.TryParse(stlSize.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double typed) && typed > 0
            ? (float)typed
            : null;
        string name = Path.GetFileNameWithoutExtension(blenderSession?.ObjectName ?? photoPath ?? Strings.DefaultModelName);
        try
        {
            string model = SlicerLauncher.Open(slicer, mesh.OnPlate(size), name);
            ExportStatus.Text = Ui.Format(Strings.OpenedInSlicer, slicer.Name, Path.GetFileName(model));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            ExportStatus.Text = Ui.Format(Strings.SlicerNotStarted, slicer.Name, error.Message);
        }

        ExportStatus.Visibility = Visibility.Visible;
    }
}
