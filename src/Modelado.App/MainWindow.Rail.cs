using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// The Operations rail (MOD98): an icon per card on the panel's edge beside the viewport, so the
/// card in use is one press away however long the others are. Choosing one scrolls its card to the
/// top of the panel, and the choice is kept for the next start. Cloud 3D, Export and History show
/// their icon only while their card is shown.
/// </summary>
public partial class MainWindow
{
    private bool fillingRail;

    private Border? CardOf(string? name) => name switch
    {
        "Object" => ObjectCard,
        "Relief" => ReliefCard,
        "Cloud" => CloudCard,
        "Edit" => EditCard,
        "Export" => ExportCard,
        "History" => HistoryCard,
        "Quality" => QualityCard,
        _ => null,
    };

    // The card chosen at the last start, selected once the panel has been laid out to scroll.
    private void RestoreRail()
    {
        string? kept = CardSetting.Load(SettingsFile.DefaultPath);
        if (Rail.Items.OfType<ListBoxItem>().FirstOrDefault(item => (string)item.Tag == kept && item.Visibility == Visibility.Visible) is not { } item)
        {
            return;
        }

        fillingRail = true;
        Rail.SelectedItem = item;
        fillingRail = false;
        Dispatcher.BeginInvoke(() => ShowCard(kept), DispatcherPriority.Loaded);
    }

    private void OnRailChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Rail.SelectedItem is not ListBoxItem { Tag: string card })
        {
            return;
        }

        ShowCard(card);
        if (fillingRail)
        {
            return;
        }

        try
        {
            CardSetting.Save(SettingsFile.DefaultPath, card);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine($"MOD98: the card chosen was not kept: {error.Message}");
        }
    }

    // The card's top at the panel's top, or as near as the scroll goes.
    private void ShowCard(string? name)
    {
        if (CardOf(name) is not { IsVisible: true } card || OperationsScroll.Content is not System.Windows.Media.Visual content)
        {
            return;
        }

        double top = card.TransformToAncestor(content).Transform(new Point(0, 0)).Y;
        OperationsScroll.ScrollToVerticalOffset(top);
    }
}
