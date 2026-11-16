using System.Globalization;
using System.IO;
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
/// top on a slider. MOD106: a name or a date raised on the relief, along the bottom or around a
/// medallion's arc, with its letters' height in mm, as the CLI's --text, --text-mm and --text-on do.
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
        ReliefTextMm.Text = Millimetres(ReliefTextHeightDefault);
        StartLithophane();
    }

    private const float ReliefTextHeightDefault = 6;

    /// <summary>
    /// MOD106: the text the card raises, or null for none. Letters the font lacks, a height that is not
    /// one, or text with no room on a relief this wide is refused here rather than by the run. The
    /// relief's printed height follows the outline the card crops to: a medallion is as tall as wide.
    /// </summary>
    private (bool Valid, ReliefText? Text) TextSettings(float printWidth, float border)
    {
        if (string.IsNullOrWhiteSpace(ReliefTextWords.Text))
        {
            return (true, null);
        }

        if (Typed(ReliefTextMm.Text) is not { } height || height <= 0)
        {
            return (false, null);
        }

        TextPlacement placement = (ReliefTextOn.SelectedItem as ComboBoxItem)?.Tag as string == "arc" ? TextPlacement.Arc : TextPlacement.Line;
        ReliefText text = new(ReliefTextWords.Text, height, Placement: placement, Font: (ReliefTextFont?.SelectedItem as ComboBoxItem)?.Tag as string);
        float aspect = photo is null ? 1 : (float)photo.Height / photo.Width;
        float tall = KeychainChosen is null && ShapeChosen == OutlineShape.Circle ? 1 : aspect / Outline.Width(ShapeChosen, aspect);
        try
        {
            ReliefTextStamp.Check(text, printWidth, printWidth * tall, border);
            return (true, text);
        }
        catch (ArgumentException)
        {
            return (false, null);
        }
    }

    /// <summary>
    /// MOD142: the installed TrueType fonts, listed the first time the font list opens: the system's
    /// and the user's own font folders, each file by its full name, CFF fonts and collections left out
    /// since <see cref="TrueTypeFont"/> reads glyph outlines only.
    /// </summary>
    private void OnReliefTextFontOpened(object? sender, EventArgs e)
    {
        if (ReliefTextFont.Items.Count > 1)
        {
            return;
        }

        string user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
        IEnumerable<(string Name, string Path)> fonts = new[] { Environment.GetFolderPath(Environment.SpecialFolder.Fonts), user }
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.ttf"))
            .Select(path => (Name: TrueTypeFont.FullName(path) ?? "", Path: path))
            .Where(font => font.Name.Length > 0)
            .DistinctBy(font => font.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(font => font.Name, StringComparer.CurrentCultureIgnoreCase);
        foreach ((string name, string path) in fonts)
        {
            ReliefTextFont.Items.Add(new ComboBoxItem { Content = name, Tag = path });
        }
    }

    // MOD142: the character the chosen font lacks, named in the card's language.
    private string TextRefusal()
    {
        if (ReliefTextFont?.SelectedItem is ComboBoxItem { Tag: string font, Content: string name }
            && ReliefTextStamp.Lacking(new ReliefText(ReliefTextWords.Text, Font: font)) is { } missing)
        {
            return Ui.Format(Strings.ReliefTextFontLacks, missing, name);
        }

        return Strings.ReliefTextInvalid;
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
        "cutout" => KeychainShape.CutOut,
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

        // MOD141: a new keychain's hole starts near the top again, until it is dragged.
        holeDown = null;
        ShowHolePlace();

        // MOD141: shaped like the object means cutting the object out.
        if (KeychainChosen == KeychainShape.CutOut)
        {
            ReliefRemoveBackground.IsChecked = true;
        }

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

        KeychainOptions options = new(shape, hole, (float)ReliefHoleAcross.Value, shape == KeychainShape.CutOut ? null : holeDown);

        // MOD141: where a cut-out has room for the hole is known only once the object is cut out.
        if (shape == KeychainShape.CutOut)
        {
            return (true, options);
        }

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
        ShowHolePlace();
        ReliefMillimetresPanel.Visibility = ReliefInMillimetres ? Visibility.Visible : Visibility.Collapsed;
        ShowReliefSizeError();
    }

    private void OnReliefMillimetresTyped(object sender, TextChangedEventArgs e) => ShowReliefSizeError();

    // MOD106: the shape and the text's placement decide whether the text fits.
    private void OnReliefChoiceChanged(object sender, SelectionChangedEventArgs e) => ShowReliefSizeError();

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

        // MOD104: the sizes may all be sizes and the keychain's hole still not fit the tag;
        // MOD106: or the text not be one the relief can carry.
        bool sizesFine = Typed(ReliefWidthMm.Text) is > 0 && Typed(ReliefDepthMm.Text) is > 0 && Typed(ReliefBaseMm.Text) is >= 0 && BorderSize() is not null;
        ReliefSizeError.Text = !wrong || !sizesFine ? Strings.ReliefSizeInvalid
            : !KeychainSettings(Typed(ReliefWidthMm.Text)!.Value).Valid ? Strings.ReliefHoleInvalid
            : TextRefusal();
    }

    /// <summary>
    /// The width, depth and base typed, or null when one is not a size: the width and depth above
    /// 0 mm, the base 0 mm or more (0 leaves the surface open). A decimal comma reads as a point, as
    /// the Export card's size does.
    /// </summary>
    private (float Width, float Depth, float Base)? PrintedSize()
    {
        if (ReliefWidthMm is null || ReliefDepthMm is null || ReliefBaseMm is null || ReliefBorderMm is null || ReliefTextMm is null)
        {
            return null;
        }

        return Typed(ReliefWidthMm.Text) is { } width && width > 0
            && Typed(ReliefDepthMm.Text) is { } depth && depth > 0
            && Typed(ReliefBaseMm.Text) is { } baseMm && baseMm >= 0
            && BorderSize() is not null
            && KeychainSettings(width).Valid
            && TextSettings(width, BorderSize()!.Value.Width).Valid
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
            // MOD113: the base the label names, so "none" is an open relief.
            return new ReliefOptions(GridLongSide: grid, Depth: (float)ReliefDepth.Value, BaseThickness: Relief.BaseShare((float)ReliefBase.Value));
        }

        return PrintedSize() is { } size && BorderSize() is { } border
            ? new ReliefOptions(GridLongSide: grid, Depth: size.Depth, BaseThickness: size.Base, PrintWidth: size.Width, BorderWidth: border.Width, BorderHeight: border.Height,
                Text: TextSettings(size.Width, border.Width).Text)
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
