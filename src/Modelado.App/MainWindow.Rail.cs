using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// The Operations rail (MOD98): an icon per card on the panel's edge beside the viewport. The panel
/// shows the chosen card alone, so its settings and its Generate button are not read past the
/// others', while the model stays in view to tune against (a modal was weighed and set aside for
/// that). The choice is kept for the next start. Cloud 3D, Export and History have an icon only
/// while they are available.
/// </summary>
public partial class MainWindow
{
    private bool fillingRail;

    // Each card sits in a host shown only while its card is the one chosen; the card's own
    // Visibility keeps saying whether it is available at all, which its rail icon follows.
    private readonly Dictionary<string, Border> cardHosts = new(StringComparer.Ordinal);

    private static readonly string[] CardNames = ["Object", "Relief", "Cloud", "Edit", "Export", "History", "Quality"];

    private void WrapCards()
    {
        StackPanel stack = (StackPanel)OperationsScroll.Content;
        foreach (string name in CardNames)
        {
            Border card = CardOf(name)!;
            int at = stack.Children.IndexOf(card);
            stack.Children.RemoveAt(at);
            Border host = new() { Child = card, Visibility = name == "Object" ? Visibility.Visible : Visibility.Collapsed };
            cardHosts[name] = host;
            stack.Children.Insert(at, host);
        }

        // 3D Object until the panel opens on the card kept from the last start.
        fillingRail = true;
        RailObject.IsSelected = true;
        fillingRail = false;
    }

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

    // The chosen card alone, from its top; a card that is not available falls back to 3D Object.
    private void ShowCard(string? name)
    {
        if (name is null || CardOf(name) is not { Visibility: Visibility.Visible })
        {
            name = "Object";
        }

        foreach ((string card, Border host) in cardHosts)
        {
            host.Visibility = card == name ? Visibility.Visible : Visibility.Collapsed;
        }

        OperationsScroll.ScrollToTop();
    }
}
