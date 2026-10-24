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

    private static string Describe(ReconstructionStage stage) => stage switch
    {
        ReconstructionStage.RemovingBackground => "Removendo o fundo",
        ReconstructionStage.Encoding => "Lendo a foto (TripoSR)",
        ReconstructionStage.SamplingDensity => "Calculando a forma",
        ReconstructionStage.ExtractingSurface => "Extraindo a superfície",
        ReconstructionStage.Colouring => "Colorindo",
        ReconstructionStage.EstimatingDepth => "Estimando a profundidade",
        _ => "Montando o relevo",
    };

    private static int TagOf(ComboBox box) => int.Parse((string)((ComboBoxItem)box.SelectedItem).Tag, System.Globalization.CultureInfo.InvariantCulture);

    private async void OnGenerateObject(object sender, RoutedEventArgs e)
    {
        ObjectOptions settings = new(
            Resolution: TagOf(ObjectResolution),
            RemoveBackground: ObjectRemoveBackground.IsChecked == true,
            DensityThreshold: (float)ObjectThreshold.Value);
        await RunAsync("Objeto 3D", ObjectReconstruction.Models, (photo, progress, token) => ObjectReconstruction.Run(runner, photo, settings, progress, token)).ConfigureAwait(true);
    }

    private async void OnGenerateRelief(object sender, RoutedEventArgs e)
    {
        bool cutOut = ReliefRemoveBackground.IsChecked == true;
        ReliefRunOptions settings = new(
            new ReliefOptions(GridLongSide: TagOf(ReliefResolution), Depth: (float)ReliefDepth.Value, BaseThickness: (float)ReliefBase.Value),
            RemoveBackground: cutOut);
        await RunAsync("Relevo", ReliefReconstruction.Models(cutOut), (photo, progress, token) => ReliefReconstruction.Run(runner, photo, settings, progress, token)).ConfigureAwait(true);
    }

    private async Task RunAsync(string title, IReadOnlyList<string> models, Func<Imaging.RgbaImage, IProgress<ReconstructionProgress>, CancellationToken, ReconstructionResult> operation)
    {
        if (busy || photo is null)
        {
            return;
        }

        // First use: the models are fetched, after the user agrees to the size.
        if (runner.Missing(models).Count > 0 && !await DownloadAsync(models).ConfigureAwait(true))
        {
            return;
        }

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
            ShowMesh(result.Mesh, result.Input);
            TimeSpan total = TimeSpan.FromTicks(result.Timings.Sum(t => t.Took.Ticks));
            string steps = string.Join(" · ", result.Timings.Select(t => $"{Describe(t.Stage).ToLowerInvariant()} {t.Took.TotalSeconds:0.0} s"));
            Status($"{title} pronto em {total.TotalSeconds:0.0} s: {steps}.{(runner.Notice is { } notice ? " " + notice : "")}");
        }
        catch (OperationCanceledException)
        {
            Status($"{title}: cancelado.");
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or FileNotFoundException or Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException)
        {
            Status($"{title}: falhou.");
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
        ProgressTitle.Text = title;
        ProgressText.Text = "";
        ProgressBar.Value = 0;
        PauseButton.Visibility = download ? Visibility.Visible : Visibility.Collapsed;
        PauseButton.Content = "Pausar";
        CancelButton.IsEnabled = true;
        bool idle = !on && photo is not null;
        ObjectButton.IsEnabled = ReliefButton.IsEnabled = idle;
        OpenButton.IsEnabled = !on;
        ExportButton.IsEnabled = !on && mesh is not null;
        QualityStandard.IsEnabled = QualityHigh.IsEnabled = !on;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        ProgressText.Text = "Cancelando…";
        pausing = false;
        work?.Cancel();
    }

    private void OnThresholdChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdText is not null)
        {
            ThresholdText.Text = $"{e.NewValue:0}";
        }
    }

    private void OnReliefSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DepthText is null || BaseText is null)
        {
            return;
        }

        DepthText.Text = $"{ReliefDepth.Value:P0}";
        BaseText.Text = ReliefBase.Value < 0.005 ? "sem base" : $"{ReliefBase.Value:P0} da largura";
    }

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
