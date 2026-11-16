using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Modelado.App;

/// <summary>
/// Examples on the drop zone (MOD152): the inputs the site demonstrates ship beside the exe in
/// Samples, so someone with no photo to hand can try each operation in one press. TripoSR's chair
/// and burger (MIT, its licence in Samples\NOTICE.txt), the Met's bust of Herodotus, Syracuse
/// Cathedral and the Grand Canyon (CC0 from Wikimedia Commons), and the chair model the site turns,
/// so Edit can be tried without running a reconstruction. Each opens like a drop, with the card the
/// site shows for it chosen: 3D Object for the object photos, Relief for the others, Edit for the
/// model. That choice is not kept for the next start, and a sample never joins Recent: it is always
/// one press away already, and would push the user's own files down.
/// </summary>
public partial class MainWindow
{
    private sealed record Sample(string File, string Label, string Card, string Credit);

    /// <summary>Where the examples are: beside the exe.</summary>
    private static string SamplesFolder => Path.Combine(AppContext.BaseDirectory, "Samples");

    private static Sample[] Samples =>
    [
        new("chair.png", Strings.SampleChair, "Object", Strings.SampleCreditTripoSR),
        new("burger.png", Strings.SampleBurger, "Object", Strings.SampleCreditTripoSR),
        new("bust.jpg", Strings.SampleBust, "Relief", Strings.SampleCreditBust),
        new("cathedral.jpg", Strings.SampleCathedral, "Relief", Strings.SampleCreditCathedral),
        new("canyon.jpg", Strings.SampleCanyon, "Relief", Strings.SampleCreditCanyon),
        new("chair.glb", Strings.SampleChairModel, "Edit", Strings.SampleCreditModel),
    ];

    /// <summary>The drop zone's examples, those whose file is there; none at all hides the strip.</summary>
    private void FillSamples()
    {
        SamplesStrip.Children.Clear();
        foreach (Sample sample in Samples)
        {
            string path = Path.Combine(SamplesFolder, sample.File);
            if (!File.Exists(path))
            {
                continue;
            }

            bool model = Path.GetExtension(path) == ".glb";
            UIElement picture = model
                ? new TextBlock
                {
                    Style = (Style)FindResource("ToolbarIcon"),
                    Text = "",
                    FontSize = 28,
                    Width = 56,
                    Height = 56,
                    TextAlignment = TextAlignment.Center,
                    Padding = new Thickness(0, 12, 0, 0),
                }
                : new Image { Source = SmallPicture(path), Width = 56, Height = 56, Stretch = System.Windows.Media.Stretch.UniformToFill };
            StackPanel content = new() { Width = 64 };
            content.Children.Add(picture);
            content.Children.Add(new TextBlock { Text = sample.Label, FontSize = 11, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0) });

            Button open = new()
            {
                Content = content,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(2),
                ToolTip = Ui.Format(Strings.SampleTip, CardTitle(sample.Card), sample.Credit),
            };
            AutomationProperties.SetAutomationId(open, $"Sample-{Path.GetFileNameWithoutExtension(sample.File)}{(model ? "-model" : "")}");
            AutomationProperties.SetName(open, sample.Label);
            open.Click += async (_, _) => await OpenSampleAsync(path, sample.Card).ConfigureAwait(true);
            SamplesStrip.Children.Add(open);
        }

        SamplesPanel.Visibility = SamplesStrip.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string CardTitle(string card) => card switch
    {
        "Relief" => Strings.ReliefCardTitle,
        "Edit" => Strings.EditCardTitle,
        _ => Strings.ObjectCardTitle,
    };

    // Opened like a drop, then its card chosen after the one RestoreRail brings back on a first open.
    private async Task OpenSampleAsync(string path, string card)
    {
        if (busy)
        {
            return;
        }

        await OpenAsync(path).ConfigureAwait(true);
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (Rail.Items.OfType<ListBoxItem>().FirstOrDefault(item => (string)item.Tag == card && item.Visibility == Visibility.Visible) is not { } item)
            {
                return;
            }

            fillingRail = true;
            Rail.SelectedItem = item;
            fillingRail = false;
            ShowCard(card);
        }, DispatcherPriority.Loaded);
    }

    // A file in Samples is not the user's own, so Recent leaves it out.
    private static bool IsSample(string file) =>
        Path.GetFullPath(file).StartsWith(Path.GetFullPath(SamplesFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
