using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Modelado.Geometry;
using Modelado.Inference.Pipelines;

namespace Modelado.App;

/// <summary>
/// Editing and improving the model shown: geometry edits (simplify, subdivide, smooth, holes,
/// pieces, base, turn, mirror) and the AI ones (a sharper texture with Real-ESRGAN, the photo's
/// colours laid on again). Every edit is a new version in <see cref="history"/>, so it can be undone.
/// </summary>
public partial class MainWindow
{
    /// <summary>A quarter turn about the X axis, for a model lying down (MOD5).</summary>
    public static readonly RoutedUICommand RotateXCommand = new("Turn about X", "RotateX", typeof(MainWindow));

    /// <summary>A quarter turn about the vertical axis.</summary>
    public static readonly RoutedUICommand RotateYCommand = new("Turn about Y", "RotateY", typeof(MainWindow));

    /// <summary>A quarter turn about the Z axis.</summary>
    public static readonly RoutedUICommand RotateZCommand = new("Turn about Z", "RotateZ", typeof(MainWindow));

    /// <summary>A mirror left to right; no shortcut, as in most editors.</summary>
    public static readonly RoutedUICommand MirrorCommand = new("Mirror", "Mirror", typeof(MainWindow));

    private void UpdateEditButtons()
    {
        bool can = !busy && mesh is not null;
        foreach (Button button in (Button[])[SimplifyButton, SubdivideButton, SmoothButton, FillHolesButton, PiecesButton, FlattenButton])
        {
            button.IsEnabled = can;
        }

        UpscaleTextureButton.IsEnabled = can && mesh!.Texture is not null;
        ReprojectButton.IsEnabled = can && reference is not null && mesh!.TexCoords is null;

        // The toolbar's commands ask their CanExecute again, now rather than at the next input.
        CommandManager.InvalidateRequerySuggested();
    }

    private void CanEdit(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy && mesh is not null;

    private void CanUndo(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy && history.CanUndo;

    private void CanRedo(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy && history.CanRedo;

    private void OnSimplifyAmountChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SimplifyText is not null)
        {
            SimplifyText.Text = $"{e.NewValue:P0}";
        }
    }

    private async void OnSimplify(object sender, RoutedEventArgs e)
    {
        double share = SimplifyAmount.Value;
        await EditAsync($"Simplify to {share:P0}", (m, _, token) => MeshEdit.Simplify(m, Math.Max(4, (int)(m.TriangleCount * share)), token)).ConfigureAwait(true);
    }

    private async void OnSubdivide(object sender, RoutedEventArgs e)
    {
        if (mesh is { TriangleCount: > 1_000_000 }
            && MessageBox.Show(this, $"The model would grow to {mesh.TriangleCount * 4:N0} triangles, heavy to view and save. Continue?", "Subdivide", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        await EditAsync("Subdivide", (m, _, _) => MeshEdit.Subdivide(m)).ConfigureAwait(true);
    }

    private async void OnSmooth(object sender, RoutedEventArgs e) =>
        await EditAsync("Smooth, 3 passes", (m, _, _) => MeshCleanup.Smooth(m, 3)).ConfigureAwait(true);

    private async void OnFillHoles(object sender, RoutedEventArgs e)
    {
        int filled = 0;
        await EditAsync("Fill holes", (m, _, _) =>
        {
            (Mesh closed, filled) = MeshEdit.FillHoles(m);
            return closed;
        }).ConfigureAwait(true);
        LastRunSaid(filled == 0 ? "Fill holes · no holes to fill" : $"Fill holes · {filled} hole(s) filled{(mesh!.DoubleSided ? " · some open edges remain" : " · the mesh is closed")}");
    }

    private async void OnRemovePieces(object sender, RoutedEventArgs e) =>
        await EditAsync("Remove pieces under 5%", (m, _, _) => MeshCleanup.RemoveSmallPieces(m, 0.05)).ConfigureAwait(true);

    private async void OnFlatten(object sender, RoutedEventArgs e) =>
        await EditAsync("Flat base at 3%", (m, _, _) => MeshEdit.FlattenBase(m, 0.03f)).ConfigureAwait(true);

    private async void OnMirror(object sender, ExecutedRoutedEventArgs e) =>
        await EditAsync("Mirror", (m, _, _) => MeshEdit.Mirror(m)).ConfigureAwait(true);

    private async void OnRotate(object sender, ExecutedRoutedEventArgs e)
    {
        Axis axis = e.Command == RotateXCommand ? Axis.X : e.Command == RotateYCommand ? Axis.Y : Axis.Z;
        await EditAsync($"Turn 90° about {axis}", (m, _, _) => MeshEdit.Rotate(m, axis, 90)).ConfigureAwait(true);
    }

    private async void OnUpscaleTexture(object sender, RoutedEventArgs e)
    {
        if (busy || await OfferFirstUseAsync(textureFirstUse!).ConfigureAwait(true))
        {
            return;
        }

        await EditAsync("Sharper texture", (m, progress, token) => Upscaling.UpscaleTexture(runner, m, progress, token)).ConfigureAwait(true);
    }

    private async void OnReproject(object sender, RoutedEventArgs e)
    {
        RgbaImageReference? from = reference is null ? null : new(reference);
        if (from is null)
        {
            return;
        }

        await EditAsync("Photo colours", (m, _, _) => ColorProjection.Project(m, from.Image).Mesh).ConfigureAwait(true);
    }

    private void OnUndo(object sender, ExecutedRoutedEventArgs e)
    {
        if (!busy && history.Undo() is { } previous)
        {
            Display(previous);
            LastRunSaid($"Undone: back to \"{history.CurrentLabel}\"");
        }
    }

    private void OnRedo(object sender, ExecutedRoutedEventArgs e)
    {
        if (!busy && history.Redo() is { } next)
        {
            Display(next);
            LastRunSaid($"Redone: {history.CurrentLabel}");
        }
    }

    // Runs an edit off the window's thread, with the progress card, and records it in the history.
    private async Task EditAsync(string label, Func<Mesh, IProgress<double>, CancellationToken, Mesh> edit)
    {
        if (busy || mesh is null)
        {
            return;
        }

        Mesh before = mesh;
        using CancellationTokenSource cancel = new();
        work = cancel;
        SetBusy(true, label, download: false);
        ProgressText.Text = $"{before.TriangleCount:N0} triangles";
        Progress<double> progress = new(f => ProgressBar.Value = f);
        Stopwatch clock = Stopwatch.StartNew();
        try
        {
            Mesh after = await Task.Run(() => edit(before, progress, cancel.Token), cancel.Token).ConfigureAwait(true);
            if (!ReferenceEquals(after, before))
            {
                history.Push(after, label);
                Display(after);
            }

            LastRunSaid($"{label} · {before.TriangleCount:N0} → {after.TriangleCount:N0} triangles · {clock.Elapsed.TotalSeconds:0.0} s", "Ctrl+Z undoes it.");
        }
        catch (OperationCanceledException)
        {
            LastRunSaid($"{label}: cancelled");
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or FileNotFoundException or Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException)
        {
            LastRunSaid($"{label}: failed", error.Message);
            MessageBox.Show(this, error.Message, label, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            work = null;
            SetBusy(false, label, download: false);
        }
    }

    /// <summary>Holds the reference for a closure that runs on another thread.</summary>
    private sealed record RgbaImageReference(Imaging.RgbaImage Image);
}
