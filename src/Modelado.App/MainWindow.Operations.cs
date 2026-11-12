using System.IO;
using System.Windows;
using System.Windows.Controls;
using Modelado.Geometry;
using Modelado.Inference;
using Modelado.Inference.Pipelines;

namespace Modelado.App;

/// <summary>The two operations: the object (TripoSR) and the relief (Depth Anything V2).</summary>
public partial class MainWindow
{
    private CancellationTokenSource? work;
    private bool busy;

    // The models the running operation reads, which About will not remove under it (MOD23).
    private IReadOnlyList<string> runningModels = [];

    private static string Describe(ReconstructionStage stage) => stage switch
    {
        ReconstructionStage.RemovingBackground => Strings.StageRemovingBackground,
        ReconstructionStage.Encoding => Strings.StageEncoding,
        ReconstructionStage.SamplingDensity => Strings.StageSamplingDensity,
        ReconstructionStage.ExtractingSurface => Strings.StageExtractingSurface,
        ReconstructionStage.Colouring => Strings.StageColouring,
        ReconstructionStage.EnhancingPhoto => Strings.StageEnhancingPhoto,
        ReconstructionStage.ProjectingPhoto => Strings.StageProjectingPhoto,
        ReconstructionStage.EstimatingDepth => Strings.StageEstimatingDepth,
        _ => Strings.StageBuildingRelief,
    };

    private static int TagOf(ComboBox box) => int.Parse((string)((ComboBoxItem)box.SelectedItem).Tag, System.Globalization.CultureInfo.InvariantCulture);

    private async void OnGenerateObject(object sender, RoutedEventArgs e)
    {
        bool enhance = ObjectEnhancePhoto.IsChecked == true;
        ObjectOptions settings = new(
            Resolution: TagOf(ObjectResolution),
            RemoveBackground: ObjectRemoveBackground.IsChecked == true,
            DensityThreshold: (float)ObjectThreshold.Value,
            EnhancePhoto: enhance,
            ProjectPhoto: ObjectProjectPhoto.IsChecked == true,
            CoarseStep: ObjectEveryPoint.IsChecked == true ? 1 : 2,
            SmoothingIterations: (int)ObjectSmoothing.Value,
            KeepPieces: (int)ObjectPieces.Value / 100.0);
        await RunAsync(objectFirstUse!, (photo, progress, token) => ObjectReconstruction.Run(runner, photo, settings, progress, token)).ConfigureAwait(true);
    }

    private void OnObjectAdvancedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ObjectSmoothingText is null || ObjectPiecesText is null)
        {
            return;
        }

        ObjectSmoothingText.Text = Ui.Format(Strings.ObjectSmoothingLabel, (int)ObjectSmoothing.Value);
        ObjectPiecesText.Text = Ui.Format(Strings.ObjectPiecesLabel, (int)ObjectPieces.Value);
    }

    // MOD85: each slider's label is one sentence with its value in it, written here rather than in
    // XAML, so it reads right from the start and not only after the first change.
    private void ShowSliderLabels()
    {
        ThresholdText.Text = Ui.Format(Strings.ObjectThresholdLabel, ObjectThreshold.Value);
        OnObjectAdvancedChanged(this, new RoutedPropertyChangedEventArgs<double>(0, 0));
        OnReliefSliderChanged(this, new RoutedPropertyChangedEventArgs<double>(0, 0));
        SimplifyText.Text = Ui.Format(Strings.SimplifyLabel, SimplifyAmount.Value);
    }

    private async void OnGenerateRelief(object sender, RoutedEventArgs e)
    {
        bool cutOut = ReliefRemoveBackground.IsChecked == true;
        ReliefRunOptions settings = new(
            new ReliefOptions(GridLongSide: TagOf(ReliefResolution), Depth: (float)ReliefDepth.Value, BaseThickness: (float)ReliefBase.Value),
            RemoveBackground: cutOut,
            DepthModel: DepthModel);
        await RunAsync(reliefFirstUse!, (photo, progress, token) => ReliefReconstruction.Run(runner, photo, settings, progress, token)).ConfigureAwait(true);
    }

    private async Task RunAsync(FirstUseCard card, Func<Imaging.RgbaImage, IProgress<ReconstructionProgress>, CancellationToken, ReconstructionResult> operation)
    {
        if (busy || photo is null)
        {
            return;
        }

        // First use (MOD22): the card offers its download, and this runs by itself once it is in.
        if (await OfferFirstUseAsync(card).ConfigureAwait(true))
        {
            return;
        }

        string title = card.Title;
        runningModels = card.Models();
        Imaging.RgbaImage input = photo;
        using CancellationTokenSource cancel = new();
        work = cancel;
        SetBusy(true, title, download: false);
        Progress<ReconstructionProgress> progress = new(p =>
        {
            ProgressText.Text = Describe(p.Stage);
            ProgressBar.Value = p.Fraction;
        });

        try
        {
            ReconstructionResult result = await Task.Run(() => operation(input, progress, cancel.Token), cancel.Token).ConfigureAwait(true);
            ShowMesh(result.Mesh, result.Input, result.Reference, title);
            TimeSpan total = TimeSpan.FromTicks(result.Timings.Sum(t => t.Took.Ticks));
            string steps = string.Join(Strings.ListSeparator, result.Timings.Select(t => Ui.Format(Strings.StageTook, Describe(t.Stage), t.Took.TotalSeconds)));

            // The provider it ran on: a model the GPU refused was run again on the CPU (Notice).
            string where = runner.Notice is null ? ProviderLabel : "CPU";
            LastRunSaid(Ui.Format(Strings.RunDone, title, total.TotalSeconds, where), runner.Notice is { } notice ? $"{steps}{Environment.NewLine}{notice}" : steps);
        }
        catch (OperationCanceledException)
        {
            LastRunSaid(Ui.Format(Strings.StepCancelled, title));
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or FileNotFoundException or Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException)
        {
            LastRunSaid(Ui.Format(Strings.StepFailed, title), error.Message);
            MessageBox.Show(this, error.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            work = null;
            SetBusy(false, title, download: false);
        }
    }

    private void SetBusy(bool on, string title, bool download)
    {
        busy = on;
        ProgressCard.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

        // MOD9: the same run in the status bar, seen however far the cards have scrolled.
        JobChip.Visibility = on && !download ? Visibility.Visible : Visibility.Collapsed;
        DownloadChip.Visibility = (on && download) || FirstUseCards.Any(card => card.Downloading is not null) ? Visibility.Visible : Visibility.Collapsed;
        ProgressTitle.Text = title;
        ProgressText.Text = "";
        ProgressBar.Value = 0;
        PauseButton.Visibility = download ? Visibility.Visible : Visibility.Collapsed;
        PauseButton.Content = Strings.PauseButton;
        CancelButton.IsEnabled = true;
        bool idle = !on && photo is not null;
        ObjectButton.IsEnabled = ReliefButton.IsEnabled = idle;
        UpdateEditButtons();
        QualityStandard.IsEnabled = QualityHigh.IsEnabled = !on;
        ProviderAuto.IsEnabled = ProviderDirectML.IsEnabled = ProviderCpu.IsEnabled = !on;
        UpdateCloudCard();
        if (!on)
        {
            RunPending(); // An operation whose download finished while this ran (MOD22).
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        ProgressText.Text = Strings.Cancelling;
        pausing = false;
        work?.Cancel();
    }

    private void OnThresholdChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdText is not null)
        {
            ThresholdText.Text = Ui.Format(Strings.ObjectThresholdLabel, e.NewValue);
        }
    }

    private void OnReliefSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DepthText is null || BaseText is null)
        {
            return;
        }

        DepthText.Text = Ui.Format(Strings.ReliefDepthLabel, ReliefDepth.Value);
        BaseText.Text = Ui.Format(Strings.ReliefBaseLabel, ReliefBase.Value < 0.005 ? Strings.BaseNone : Ui.Format(Strings.BaseShare, ReliefBase.Value));
    }

    private static IReadOnlyList<string> ObjectModels(bool enhance) =>
        enhance ? [.. ObjectReconstruction.Models, Upscaling.Model] : ObjectReconstruction.Models;

    private async void OnEnhanceChanged(object sender, RoutedEventArgs e) => await RefreshModelStatusAsync().ConfigureAwait(true);

    private async void OnReliefCutOutChanged(object sender, RoutedEventArgs e) => await RefreshModelStatusAsync().ConfigureAwait(true);

    private async void OnQualityChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || busy)
        {
            return;
        }

        runner.Dispose();
        runner = new ModelRunner(library, provider, QualityHigh.IsChecked == true);
        await RefreshModelStatusAsync().ConfigureAwait(true);
    }
}
