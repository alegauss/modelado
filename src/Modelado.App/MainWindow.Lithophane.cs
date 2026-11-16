using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Modelado.Geometry;

namespace Modelado.App;

/// <summary>
/// The lithophane on the Relief card (MOD108): a thin panel whose thickness follows the photo's
/// darkness, so it shows the photo when lit from behind. It needs no model, so it runs straight away
/// on the photo, never through a first-use download, and comes out sized in millimetres, which the
/// Export card's STL size keeps, as a relief sized in millimetres does. The CLI's lithophane command
/// builds the same panel through <see cref="Lithophane.Build"/>.
/// </summary>
public partial class MainWindow
{
    private void StartLithophane()
    {
        LithophaneWidthMm.Text = Millimetres(Lithophane.WidthDefault);
        LithophaneMinMm.Text = Millimetres(Lithophane.MinThicknessDefault);
        LithophaneMaxMm.Text = Millimetres(Lithophane.MaxThicknessDefault);
        LithophaneFrameMm.Text = "0";
    }

    /// <summary>The width and thicknesses typed, or null when they are not a panel that can be printed.</summary>
    private LithophaneOptions? LithophaneSettings()
    {
        if (LithophaneWidthMm is null || LithophaneMinMm is null || LithophaneMaxMm is null || LithophaneFrameMm is null)
        {
            return null;
        }

        return Typed(LithophaneWidthMm.Text) is { } width && width > 0
            && Typed(LithophaneMinMm.Text) is { } thinnest && thinnest > 0
            && Typed(LithophaneMaxMm.Text) is { } thickest && thickest > thinnest
            && LithophaneFrame(width) is { } frame
            ? new LithophaneOptions(width, thinnest, thickest, Shape: LithophaneShapeChosen, Frame: frame)
            : null;
    }

    // MOD145: the rim typed, or null when it is not a size or leaves no panel inside it.
    private float? LithophaneFrame(float width)
    {
        float tall = photo is null ? width : width * photo.Height / photo.Width;
        return Typed(LithophaneFrameMm.Text) is { } frame && frame >= 0 && frame * 2 < MathF.Min(width, tall) ? frame : null;
    }

    private LithophaneShape LithophaneShapeChosen =>
        Enum.TryParse((LithophaneForm?.SelectedItem as ComboBoxItem)?.Tag as string, out LithophaneShape shape) ? shape : LithophaneShape.Flat;

    private void OnLithophaneTyped(object sender, TextChangedEventArgs e) => ShowLithophaneError();

    private void OnLithophaneShapeChanged(object sender, SelectionChangedEventArgs e) => ShowLithophaneError();

    private void ShowLithophaneError()
    {
        if (LithophaneSizeError is null || LithophaneFrameMm is null)
        {
            return;
        }

        LithophaneSizeError.Visibility = LithophaneSettings() is null ? Visibility.Visible : Visibility.Collapsed;

        // MOD145: the sizes may all be sizes and the rim still leave no panel inside it.
        bool sizesFine = Typed(LithophaneWidthMm.Text) is > 0 && Typed(LithophaneMinMm.Text) is { } thinnest && thinnest > 0
            && Typed(LithophaneMaxMm.Text) is { } thickest && thickest > thinnest;
        LithophaneSizeError.Text = sizesFine ? Strings.LithophaneFrameInvalid : Strings.LithophaneSizeInvalid;
    }

    private async void OnMakeLithophane(object sender, RoutedEventArgs e)
    {
        if (busy || photo is null || LithophaneSettings() is not { } size)
        {
            ShowLithophaneError();
            return;
        }

        string title = Strings.LithophaneTitle;
        Imaging.RgbaImage input = photo;
        SetBusy(true, title, download: false);
        try
        {
            Stopwatch took = Stopwatch.StartNew();
            Mesh panel = await Task.Run(() => Lithophane.Build(input, size)).ConfigureAwait(true);
            ShowMesh(panel, input, null, title);
            KeepPrintedSize(panel);
            LastRunSaid(Ui.Format(Strings.RunDone, title, took.Elapsed.TotalSeconds, "CPU"));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OutOfMemoryException)
        {
            LastRunSaid(Ui.Format(Strings.StepFailed, title), error.Message);
        }
        finally
        {
            SetBusy(false, title, download: false);
        }
    }
}
