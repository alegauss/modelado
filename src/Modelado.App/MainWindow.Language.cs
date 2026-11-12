using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// About's interface language (MOD86): Windows' display language, or one the build ships a
/// translation for, each named in its own words. The choice is saved per user and applies at the
/// next start, which the note under it says, rather than re-binding the window that is open.
/// </summary>
public partial class MainWindow
{
    private bool fillingLanguage;

    /// <summary>One entry of the list; null stands for Windows' own language.</summary>
    private sealed record LanguageChoice(string? Name, string Label)
    {
        // What the list item announces, which is what a case and a screen reader read.
        public override string ToString() => Label;
    }

    private void FillLanguage()
    {
        List<LanguageChoice> choices =
        [
            new(null, Ui.Format(Strings.LanguageWindows, InterfaceLanguage.Native(InterfaceLanguage.Shown(null)))),
            .. InterfaceLanguage.Available.Select(name => new LanguageChoice(name, InterfaceLanguage.Native(name))),
        ];
        string? chosen = LanguageSetting.Load(SettingsFile.DefaultPath);
        fillingLanguage = true;
        AboutLanguage.ItemsSource = choices;
        AboutLanguage.SelectedItem = choices.FirstOrDefault(c => c.Name == chosen) ?? choices[0];
        fillingLanguage = false;
        ShowLanguageNote(chosen);
    }

    // What the window is in, or what it will be in once restarted when the choice differs.
    private void ShowLanguageNote(string? chosen)
    {
        string next = InterfaceLanguage.Shown(chosen);
        AboutLanguageNote.Text = string.Equals(next, CultureInfo.CurrentUICulture.Name, StringComparison.OrdinalIgnoreCase)
            ? Ui.Format(Strings.LanguageNow, InterfaceLanguage.Native(next))
            : Ui.Format(Strings.LanguageNextStart, InterfaceLanguage.Native(next));
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (fillingLanguage || AboutLanguage.SelectedItem is not LanguageChoice choice)
        {
            return;
        }

        try
        {
            LanguageSetting.Save(SettingsFile.DefaultPath, choice.Name);
            ShowLanguageNote(choice.Name);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            AboutLanguageNote.Text = Ui.Format(Strings.ChoiceNotSaved, error.Message);
        }
    }
}
