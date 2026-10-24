using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
    private void UpdateEditButtons()
    {
        bool can = !busy && mesh is not null;
        foreach (Button button in (Button[])[SimplifyButton, SubdivideButton, SmoothButton, FillHolesButton, PiecesButton, FlattenButton, MirrorButton, RotateXButton, RotateYButton, RotateZButton])
        {
            button.IsEnabled = can;
        }

        UpscaleTextureButton.IsEnabled = can && mesh!.Texture is not null;
        ReprojectButton.IsEnabled = can && reference is not null && mesh!.TexCoords is null;
        UndoButton.IsEnabled = !busy && history.CanUndo;
        RedoButton.IsEnabled = !busy && history.CanRedo;
    }

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
        await EditAsync("Simplificar", (m, _, token) => MeshEdit.Simplify(m, Math.Max(4, (int)(m.TriangleCount * share)), token)).ConfigureAwait(true);
    }

    private async void OnSubdivide(object sender, RoutedEventArgs e)
    {
        if (mesh is { TriangleCount: > 1_000_000 }
            && MessageBox.Show(this, $"O modelo passaria a {mesh.TriangleCount * 4:N0} triângulos, pesado para ver e salvar. Continuar?", "Subdividir", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        await EditAsync("Subdividir", (m, _, _) => MeshEdit.Subdivide(m)).ConfigureAwait(true);
    }

    private async void OnSmooth(object sender, RoutedEventArgs e) =>
        await EditAsync("Suavizar", (m, _, _) => MeshCleanup.Smooth(m, 3)).ConfigureAwait(true);

    private async void OnFillHoles(object sender, RoutedEventArgs e)
    {
        int filled = 0;
        await EditAsync("Fechar buracos", (m, _, _) =>
        {
            (Mesh closed, filled) = MeshEdit.FillHoles(m);
            return closed;
        }).ConfigureAwait(true);
        Status(filled == 0 ? "Nenhum buraco para fechar." : $"{filled} buraco(s) fechado(s){(mesh!.DoubleSided ? "; ainda há bordas abertas" : "; a malha está fechada")}.");
    }

    private async void OnRemovePieces(object sender, RoutedEventArgs e) =>
        await EditAsync("Remover pedaços soltos", (m, _, _) => MeshCleanup.RemoveSmallPieces(m, 0.05)).ConfigureAwait(true);

    private async void OnFlatten(object sender, RoutedEventArgs e) =>
        await EditAsync("Base plana", (m, _, _) => MeshEdit.FlattenBase(m, 0.03f)).ConfigureAwait(true);

    private async void OnMirror(object sender, RoutedEventArgs e) =>
        await EditAsync("Espelhar", (m, _, _) => MeshEdit.Mirror(m)).ConfigureAwait(true);

    private async void OnRotate(object sender, RoutedEventArgs e)
    {
        Axis axis = (string)((Button)sender).Tag switch { "X" => Axis.X, "Y" => Axis.Y, _ => Axis.Z };
        await EditAsync($"Girar em {axis}", (m, _, _) => MeshEdit.Rotate(m, axis, 90)).ConfigureAwait(true);
    }

    private async void OnUpscaleTexture(object sender, RoutedEventArgs e)
    {
        if (runner.Missing([Upscaling.Model]).Count > 0 && !await DownloadAsync([Upscaling.Model]).ConfigureAwait(true))
        {
            return;
        }

        await EditAsync("Textura mais nítida", (m, progress, token) => Upscaling.UpscaleTexture(runner, m, progress, token)).ConfigureAwait(true);
    }

    private async void OnReproject(object sender, RoutedEventArgs e)
    {
        RgbaImageReference? from = reference is null ? null : new(reference);
        if (from is null)
        {
            return;
        }

        await EditAsync("Cores da foto", (m, _, _) => ColorProjection.Project(m, from.Image).Mesh).ConfigureAwait(true);
    }

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (!busy && history.Undo() is { } previous)
        {
            Display(previous);
            Status($"Desfeito: voltou para \"{history.CurrentLabel}\".");
        }
    }

    private void OnRedo(object sender, RoutedEventArgs e)
    {
        if (!busy && history.Redo() is { } next)
        {
            Display(next);
            Status($"Refeito: {history.CurrentLabel}.");
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
        ProgressText.Text = $"{before.TriangleCount:N0} triângulos";
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

            Status($"{label}: {before.TriangleCount:N0} → {after.TriangleCount:N0} triângulos em {clock.Elapsed.TotalSeconds:0.0} s. Ctrl+Z desfaz.");
        }
        catch (OperationCanceledException)
        {
            Status($"{label}: cancelado.");
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or FileNotFoundException or Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException)
        {
            Status($"{label}: falhou.");
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
