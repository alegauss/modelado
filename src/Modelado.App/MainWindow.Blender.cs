using System.IO;
using System.Windows;
using Modelado.Geometry;

namespace Modelado.App;

/// <summary>
/// The round trip with Blender (MOD46): started with --blender-session &lt;folder&gt; by the add-on's
/// "Edit in Modelado", the window opens the object's mesh, names the object in its caption and
/// source chip, and offers "Send to Blender" on the toolbar. Each send hands the version shown to
/// the add-on through <see cref="BlenderSession"/>, which puts it on the same object as one undo
/// step there; the history here goes on as before, so a send can follow any number of edits.
/// </summary>
public partial class MainWindow
{
    private BlenderSession? blenderSession;

    private async Task OpenBlenderSessionAsync(string folder)
    {
        try
        {
            blenderSession = BlenderSession.Open(folder);
        }
        catch (InvalidDataException error)
        {
            ShowOpenError(error.Message);
            return;
        }

        await OpenModelAsync(blenderSession.Model).ConfigureAwait(true);
        if (mesh is null)
        {
            blenderSession = null;
            return;
        }

        SendToBlenderButton.Visibility = Visibility.Visible;
        Title = $"{blenderSession.ObjectName} from Blender · Modelado";
        ShowSource($"{blenderSession.ObjectName} from Blender");
    }

    // Looked for once: Blender does not come and go while the window is open.
    private readonly Lazy<string?> blender = new(BlenderLauncher.Find);

    private void UpdateOpenInBlender() =>
        OpenInBlender.Visibility = mesh is not null && blender.Value is not null ? Visibility.Visible : Visibility.Collapsed;

    private void OnOpenInBlender(object sender, RoutedEventArgs e)
    {
        if (busy || mesh is null || blender.Value is not { } program)
        {
            return;
        }

        string name = Path.GetFileNameWithoutExtension(blenderSession?.ObjectName ?? photoPath ?? "model");
        try
        {
            string glb = BlenderLauncher.Open(program, mesh, name);
            ExportStatus.Text = $"Opened in Blender: {Path.GetFileName(glb)}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            ExportStatus.Text = $"Blender could not be started: {error.Message}";
        }

        ExportStatus.Visibility = Visibility.Visible;
    }

    private void OnSendToBlender(object sender, RoutedEventArgs e)
    {
        if (busy || blenderSession is null || mesh is null)
        {
            return;
        }

        try
        {
            int sequence = blenderSession.Send(mesh, history.CurrentLabel ?? "Edited in Modelado");
            LastRunSaid($"Sent to Blender: {blenderSession.ObjectName}, version {sequence}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            LastRunSaid("Not sent to Blender", error.Message);
        }
    }
}
