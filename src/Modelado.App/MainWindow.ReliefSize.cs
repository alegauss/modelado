using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Modelado.Geometry;

namespace Modelado.App;

/// <summary>
/// The Relief card in millimetres (MOD102). A printer thinks in a width and thicknesses, not in
/// shares of the width, so "Size it in millimetres" swaps the depth and base sliders for a printed
/// width, a relief depth and a solid base in mm. The relief is built at that size
/// (<see cref="ReliefOptions.PrintWidth"/>), and the Export card's STL size is set to the longest
/// side it came out at, so Save as writes exactly what was asked for rather than the 100 mm default.
/// The CLI takes the same as --width-mm, --depth-mm and --base-mm.
/// </summary>
public partial class MainWindow
{
    private void StartReliefSize()
    {
        ReliefWidthMm.Text = Millimetres(Relief.PrintWidthDefault);
        ReliefDepthMm.Text = Millimetres(Relief.PrintDepthDefault);
        ReliefBaseMm.Text = Millimetres(Relief.PrintBaseDefault);
    }

    private static string Millimetres(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private bool ReliefInMillimetres => ReliefMillimetres.IsChecked == true;

    // Checked and Unchecked, not Click: UI Automation's Toggle (a screen reader, a case) changes
    // IsChecked without a click, and the card must follow it all the same.
    private void OnReliefMillimetresChanged(object sender, RoutedEventArgs e)
    {
        if (ReliefSharePanel is null || ReliefMillimetresPanel is null)
        {
            return;
        }

        ReliefSharePanel.Visibility = ReliefInMillimetres ? Visibility.Collapsed : Visibility.Visible;
        ReliefMillimetresPanel.Visibility = ReliefInMillimetres ? Visibility.Visible : Visibility.Collapsed;
        ShowReliefSizeError();
    }

    private void OnReliefMillimetresTyped(object sender, TextChangedEventArgs e) => ShowReliefSizeError();

    private void ShowReliefSizeError()
    {
        if (ReliefSizeError is not null)
        {
            ReliefSizeError.Visibility = ReliefInMillimetres && PrintedSize() is null ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// The width, depth and base typed, or null when one is not a size: the width and depth above
    /// 0 mm, the base 0 mm or more (0 leaves the surface open). A decimal comma reads as a point, as
    /// the Export card's size does.
    /// </summary>
    private (float Width, float Depth, float Base)? PrintedSize()
    {
        if (ReliefWidthMm is null || ReliefDepthMm is null || ReliefBaseMm is null)
        {
            return null;
        }

        return Typed(ReliefWidthMm.Text) is { } width && width > 0
            && Typed(ReliefDepthMm.Text) is { } depth && depth > 0
            && Typed(ReliefBaseMm.Text) is { } baseMm && baseMm >= 0
            ? (width, depth, baseMm)
            : null;
    }

    private static float? Typed(string text) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
        && value <= 10_000 ? (float)value : null;

    /// <summary>The Relief card's options, or null when the millimetres typed are not sizes.</summary>
    private ReliefOptions? ReliefSettings()
    {
        int grid = TagOf(ReliefResolution);
        if (!ReliefInMillimetres)
        {
            return new ReliefOptions(GridLongSide: grid, Depth: (float)ReliefDepth.Value, BaseThickness: (float)ReliefBase.Value);
        }

        return PrintedSize() is { } size
            ? new ReliefOptions(GridLongSide: grid, Depth: size.Depth, BaseThickness: size.Base, PrintWidth: size.Width)
            : null;
    }

    // The relief came out in millimetres: the STL is saved at the size it already has.
    private void KeepPrintedSize(Mesh printed)
    {
        (System.Numerics.Vector3 min, System.Numerics.Vector3 max) = printed.Bounds();
        System.Numerics.Vector3 extent = max - min;
        stlSize = Millimetres(Math.Max(extent.X, Math.Max(extent.Y, extent.Z)));
        if (ExportExtension == "stl")
        {
            ExportSize.Text = stlSize;
        }
    }
}
