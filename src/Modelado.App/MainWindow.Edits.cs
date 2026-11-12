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
    public static readonly RoutedUICommand RotateXCommand = new(Strings.TurnAboutX, "RotateX", typeof(MainWindow));

    /// <summary>A quarter turn about the vertical axis.</summary>
    public static readonly RoutedUICommand RotateYCommand = new(Strings.TurnAboutY, "RotateY", typeof(MainWindow));

    /// <summary>A quarter turn about the Z axis.</summary>
    public static readonly RoutedUICommand RotateZCommand = new(Strings.TurnAboutZ, "RotateZ", typeof(MainWindow));

    /// <summary>A mirror left to right; no shortcut, as in most editors.</summary>
    public static readonly RoutedUICommand MirrorCommand = new(Strings.Mirror, "Mirror", typeof(MainWindow));

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
            SimplifyText.Text = Ui.Format(Strings.SimplifyLabel, e.NewValue);
        }
    }

    // MOD16: the sliders hold whole passes and whole percent; the share is that integer over 100, so it
    // is the double (or float) the CLI parses from the same "0.06".
    private int SmoothPasses => (int)SmoothAmount.Value;

    private int PiecesPercent => (int)PiecesAmount.Value;

    private int FlattenPercent => (int)FlattenAmount.Value;

    private void OnEditAmountChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SmoothText is null || PiecesText is null || FlattenText is null)
        {
            return;
        }

        SmoothText.Text = SmoothPasses == 1 ? Strings.PassOne : Ui.Format(Strings.PassMany, SmoothPasses);
        PiecesText.Text = Ui.Format(Strings.PiecesUnder, PiecesPercent);
        FlattenText.Text = $"{FlattenPercent}%";
    }

    private async void OnSimplify(object sender, RoutedEventArgs e)
    {
        double share = SimplifyAmount.Value;
        await EditAsync(Ui.Format(Strings.SimplifyStep, share), (m, _, token) => MeshEdit.Simplify(m, Math.Max(4, (int)(m.TriangleCount * share)), token)).ConfigureAwait(true);
    }

    private async void OnSubdivide(object sender, RoutedEventArgs e)
    {
        if (mesh is { TriangleCount: > 1_000_000 }
            && MessageBox.Show(this, Ui.Format(Strings.SubdivideConfirm, mesh.TriangleCount * 4), Strings.SubdivideStep, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        await EditAsync(Strings.SubdivideStep, (m, _, _) => MeshEdit.Subdivide(m)).ConfigureAwait(true);
    }

    private async void OnSmooth(object sender, RoutedEventArgs e)
    {
        int passes = SmoothPasses;
        await EditAsync(passes == 1 ? Strings.SmoothStepOne : Ui.Format(Strings.SmoothStepMany, passes), (m, _, _) => MeshCleanup.Smooth(m, passes)).ConfigureAwait(true);
    }

    private async void OnFillHoles(object sender, RoutedEventArgs e)
    {
        int filled = 0;
        await EditAsync(Strings.FillHolesStep, (m, _, _) =>
        {
            (Mesh closed, filled) = MeshEdit.FillHoles(m);
            return closed;
        }).ConfigureAwait(true);
        LastRunSaid(filled == 0 ? Strings.FillHolesNone : Ui.Format(mesh!.DoubleSided ? Strings.FillHolesOpen : Strings.FillHolesClosed, filled));
    }

    private async void OnRemovePieces(object sender, RoutedEventArgs e)
    {
        int percent = PiecesPercent;
        await EditAsync(Ui.Format(Strings.RemovePiecesStep, percent), (m, _, _) => MeshCleanup.RemoveSmallPieces(m, percent / 100.0)).ConfigureAwait(true);
    }

    private async void OnFlatten(object sender, RoutedEventArgs e)
    {
        int percent = FlattenPercent;
        await EditAsync(Ui.Format(Strings.FlatBaseStep, percent), (m, _, _) => MeshEdit.FlattenBase(m, percent / 100f)).ConfigureAwait(true);
    }

    private async void OnMirror(object sender, ExecutedRoutedEventArgs e) =>
        await EditAsync(Strings.Mirror, (m, _, _) => MeshEdit.Mirror(m)).ConfigureAwait(true);

    private async void OnRotate(object sender, ExecutedRoutedEventArgs e)
    {
        Axis axis = e.Command == RotateXCommand ? Axis.X : e.Command == RotateYCommand ? Axis.Y : Axis.Z;
        await EditAsync(Ui.Format(Strings.TurnStep, axis), (m, _, _) => MeshEdit.Rotate(m, axis, 90)).ConfigureAwait(true);
    }

    private async void OnUpscaleTexture(object sender, RoutedEventArgs e)
    {
        if (busy || await OfferFirstUseAsync(textureFirstUse!).ConfigureAwait(true))
        {
            return;
        }

        runningModels = [Upscaling.Model];

        await EditAsync(Strings.SharperTextureStep, (m, progress, token) => Upscaling.UpscaleTexture(runner, m, progress, token)).ConfigureAwait(true);
    }

    private async void OnReproject(object sender, RoutedEventArgs e)
    {
        RgbaImageReference? from = reference is null ? null : new(reference);
        if (from is null)
        {
            return;
        }

        await EditAsync(Strings.PhotoColoursStep, (m, _, _) => ColorProjection.Project(m, from.Image).Mesh).ConfigureAwait(true);
    }

    private void OnUndo(object sender, ExecutedRoutedEventArgs e)
    {
        if (!busy && history.Undo() is { } previous)
        {
            Display(previous);
            LastRunSaid(Ui.Format(Strings.UndoneTo, history.CurrentLabel));
        }
    }

    private void OnRedo(object sender, ExecutedRoutedEventArgs e)
    {
        if (!busy && history.Redo() is { } next)
        {
            Display(next);
            LastRunSaid(Ui.Format(Strings.RedoneTo, history.CurrentLabel));
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
        ProgressText.Text = Ui.Format(Strings.TrianglesCount, before.TriangleCount);
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

            LastRunSaid(Ui.Format(Strings.EditDone, label, before.TriangleCount, after.TriangleCount, clock.Elapsed.TotalSeconds), Strings.UndoHint);
        }
        catch (OperationCanceledException)
        {
            LastRunSaid(Ui.Format(Strings.StepCancelled, label));
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or FileNotFoundException or Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException)
        {
            LastRunSaid(Ui.Format(Strings.StepFailed, label), error.Message);
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
