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
/// The CLI takes the same as --width-mm, --depth-mm and --base-mm. MOD104: in millimetres the card
/// can also cut the relief into a keychain, a disc or a rounded tag with a hole near the top
/// (<see cref="Keychain"/>), as the CLI's --keychain and --hole-mm do; a tag's hole slides along its
/// top on a slider.
/// </summary>
public partial class MainWindow
{
    private void StartReliefSize()
    {
        ReliefWidthMm.Text = Millimetres(Relief.PrintWidthDefault);
        ReliefDepthMm.Text = Millimetres(Relief.PrintDepthDefault);
        ReliefBaseMm.Text = Millimetres(Relief.PrintBaseDefault);
        ReliefHoleMm.Text = Millimetres(Keychain.HoleDiameterDefault);
        ReliefBorderMm.Text = "0";
        ReliefBorderHeightMm.Text = Millimetres(Relief.PrintDepthDefault + 1);
    }

    // MOD105: the outline the card crops the relief to.
    private OutlineShape ShapeChosen =>
        Enum.TryParse((ReliefShape.SelectedItem as ComboBoxItem)?.Tag as string, out OutlineShape shape) ? shape : OutlineShape.Rectangle;

    // MOD105: the raised border's width and height, or null when one is not a size.
    private (float Width, float Height)? BorderSize() =>
        Typed(ReliefBorderMm.Text) is { } width && width >= 0 && Typed(ReliefBorderHeightMm.Text) is { } height && height > 0
            ? (width, height)
            : null;

    // MOD104: the keychain the card cuts, or null for a plain relief.
    private KeychainShape? KeychainChosen => (ReliefKeychain?.SelectedItem as ComboBoxItem)?.Tag switch
    {
        "circle" => KeychainShape.Circle,
        "tag" => KeychainShape.Tag,
        _ => null,
    };

    private void OnReliefKeychainChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReliefHoleMm is null || ReliefHoleAcross is null)
        {
            return;
        }

        Visibility hole = KeychainChosen is null ? Visibility.Collapsed : Visibility.Visible;
        ReliefHoleLabel.Visibility = ReliefHoleMm.Visibility = ReliefHoleUnit.Visibility = hole;

        // Only a tag's hole slides along its top; a disc's sits at the top centre.
        Visibility across = KeychainChosen == KeychainShape.Tag ? Visibility.Visible : Visibility.Collapsed;
        ReliefHoleAcrossLabel.Visibility = ReliefHoleAcross.Visibility = across;
        ShowReliefSizeError();
    }

    /// <summary>
    /// The keychain chosen and its hole, or null for none. A hole that is not a size, or one too large
    /// for the tag the photo makes at that width, is refused here rather than by the run.
    /// </summary>
    private (bool Valid, KeychainOptions? Options) KeychainSettings(float printWidth)
    {
        if (KeychainChosen is not { } shape)
        {
            return (true, null);
        }

        if (Typed(ReliefHoleMm.Text) is not { } hole || hole <= 0)
        {
            return (false, null);
        }

        KeychainOptions options = new(shape, hole, (float)ReliefHoleAcross.Value);
        float aspect = photo is null ? 1 : (float)photo.Height / photo.Width;
        try
        {
            _ = Keychain.Mask(8, 8, aspect, printWidth, options);
            return (true, options);
        }
        catch (ArgumentException)
        {
            return (false, null);
        }
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

    /// <summary>The keychain the Relief card's millimetres cut, or null (MOD104).</summary>
    private KeychainOptions? ReliefKeychainOptions() =>
        ReliefInMillimetres && PrintedSize() is { } size ? KeychainSettings(size.Width).Options : null;

    private void ShowReliefSizeError()
    {
        if (ReliefSizeError is null || ReliefHoleMm is null)
        {
            return;
        }

        bool wrong = ReliefInMillimetres && PrintedSize() is null;
        ReliefSizeError.Visibility = wrong ? Visibility.Visible : Visibility.Collapsed;

        // MOD104: the sizes may all be sizes and the keychain's hole still not fit the tag.
        bool sizesFine = Typed(ReliefWidthMm.Text) is > 0 && Typed(ReliefDepthMm.Text) is > 0 && Typed(ReliefBaseMm.Text) is >= 0 && BorderSize() is not null;
        ReliefSizeError.Text = wrong && sizesFine ? Strings.ReliefHoleInvalid : Strings.ReliefSizeInvalid;
    }

    /// <summary>
    /// The width, depth and base typed, or null when one is not a size: the width and depth above
    /// 0 mm, the base 0 mm or more (0 leaves the surface open). A decimal comma reads as a point, as
    /// the Export card's size does.
    /// </summary>
    private (float Width, float Depth, float Base)? PrintedSize()
    {
        if (ReliefWidthMm is null || ReliefDepthMm is null || ReliefBaseMm is null || ReliefBorderMm is null)
        {
            return null;
        }

        return Typed(ReliefWidthMm.Text) is { } width && width > 0
            && Typed(ReliefDepthMm.Text) is { } depth && depth > 0
            && Typed(ReliefBaseMm.Text) is { } baseMm && baseMm >= 0
            && BorderSize() is not null
            && KeychainSettings(width).Valid
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

        return PrintedSize() is { } size && BorderSize() is { } border
            ? new ReliefOptions(GridLongSide: grid, Depth: size.Depth, BaseThickness: size.Base, PrintWidth: size.Width, BorderWidth: border.Width, BorderHeight: border.Height)
            : null;
    }

    // The relief came out in millimetres: the STL is saved at the size it already has.
    private void KeepPrintedSize(Mesh printed)
    {
        (System.Numerics.Vector3 min, System.Numerics.Vector3 max) = printed.Bounds();
        System.Numerics.Vector3 extent = max - min;
        stlSize = Millimetres(Math.Max(extent.X, Math.Max(extent.Y, extent.Z)));
        if (ForPrinting(ExportExtension))
        {
            ExportSize.Text = stlSize;
        }
    }
}
