using System.Windows;
using Modelado.Geometry;

namespace Modelado.App;

/// <summary>
/// MOD141: a disc's or a rounded tag's hole dragged into place on the photo, instead of only slid
/// along the top. While the photo is shown and the Relief card cuts a disc or a tag in millimetres, a
/// press and a drag on the viewport put the hole where the pointer is, in the photo's 0..1
/// coordinates (<see cref="KeychainOptions.HoleX"/> and <see cref="KeychainOptions.HoleY"/>), and the
/// slider follows across. <see cref="Keychain.Mask"/> keeps it a wall inside the outline wherever it
/// is dropped. A cut-out keychain places its own hole where the object has room.
/// </summary>
public partial class MainWindow
{
    private bool placingHole;

    // Down the photo, 0 at the top, once the hole was dragged; null keeps it near the top edge.
    private float? holeDown;

    private bool PlacingHole =>
        !ShowingModel && photo is not null && ReliefCard.Visibility == Visibility.Visible && ReliefInMillimetres
        && KeychainChosen is KeychainShape.Circle or KeychainShape.Tag;

    // Where the pointer is over the photo, which the image shows whole and centred (Stretch Uniform).
    private void PlaceHole(Point at)
    {
        if (ViewImage.ActualWidth < 1 || ViewImage.ActualHeight < 1)
        {
            return;
        }

        float across = (float)Math.Clamp(at.X / ViewImage.ActualWidth, 0, 1);
        holeDown = (float)Math.Clamp(at.Y / ViewImage.ActualHeight, 0, 1);
        ReliefHoleAcross.Value = across;
        ShowHolePlace();
        ShowReliefSizeError();
    }

    private void ShowHolePlace()
    {
        if (ReliefHolePlace is null || ReliefHoleAcross is null)
        {
            return;
        }

        bool shown = ReliefInMillimetres && KeychainChosen is KeychainShape.Circle or KeychainShape.Tag;
        ReliefHolePlace.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        ReliefHolePlace.Text = holeDown is { } down
            ? Ui.Format(Strings.ReliefHolePlace, ReliefHoleAcross.Value * 100, down * 100)
            : Strings.ReliefHoleDragHint;
    }
}
