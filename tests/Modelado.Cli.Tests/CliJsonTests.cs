using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Modelado.Cli.Tests;

/// <summary>
/// MOD43: modelado-cli --json, what the Blender add-on reads: one JSON object on stdout, and
/// every line a person reads on stderr. Run as the built CLI, the way the add-on runs it.
/// </summary>
public sealed class CliJsonTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"modelado-cli-{Guid.NewGuid():N}");

    public CliJsonTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private static (int Code, string Out, string Err) Run(params string[] args) => RunIn(null, null, args);

    // localAppData: where the key store, settings.json and the ledger are, as the window finds them.
    private static (int Code, string Out, string Err) RunIn(string? localAppData, string? input, params string[] args)
    {
        ProcessStartInfo start = new("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = input is not null, UseShellExecute = false };
        start.ArgumentList.Add(typeof(CliArguments).Assembly.Location);
        foreach (string arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        if (localAppData is not null)
        {
            start.Environment["LOCALAPPDATA"] = localAppData;
        }

        using Process cli = Process.Start(start)!;
        if (input is not null)
        {
            cli.StandardInput.WriteLine(input);
            cli.StandardInput.Close();
        }

        Task<string> err = cli.StandardError.ReadToEndAsync();
        string output = cli.StandardOutput.ReadToEnd();
        cli.WaitForExit();
        return (cli.ExitCode, output, err.Result);
    }

    [Fact]
    public void A_photo_becomes_a_meshy_model_priced_first_and_recorded_where_the_window_reads_it()
    {
        // MOD65, over the UI cases' Meshy stand-in: every image-to-3D job is the cube, for 30 credits.
        string local = Path.Combine(folder, "local");
        string photo = Path.Combine(Repository.Root, "cases", "fixtures", "photo.png");
        string model = Path.Combine(folder, "meshy.glb");
        string api = $"meshy=http://127.0.0.1:{Modelado.Cases.FixtureServer.Port}/meshy/";
        using Modelado.Cases.FixtureServer meshy = new(Path.Combine(Repository.Root, "cases", "fixtures", "download"));
        JsonObject Report(string output) => Assert.IsType<JsonObject>(JsonNode.Parse(output));
        string[] Estimate() => ["cloud", "image-to-3d", photo, "--estimate", "--cloud-api", api, "--json"];
        string[] Send() => ["cloud", "image-to-3d", photo, "-o", model, "--cloud-api", api, "--json"];
        string ledger = Path.Combine(local, "Modelado", "cloud-ledger.jsonl");

        // Priced before anything is sent, in the window's consent words, with no key yet.
        (int code, string output, _) = RunIn(local, null, Estimate());
        JsonObject estimate = Report(output);
        Assert.Equal((0, false, false), (code, estimate["keyStored"]!.GetValue<bool>(), estimate["blocked"]!.GetValue<bool>()));
        Assert.Matches(@"^This photo, \d+ MB, goes to Meshy AI for about 30 credits\.$", estimate["consent"]!.GetValue<string>());
        Assert.Equal(30, estimate["estimate"]!["amount"]!.GetValue<double>());
        (code, output, _) = RunIn(local, null, Send());
        Assert.Equal(1, code);
        Assert.Contains("No Meshy AI key is stored", Report(output)["error"]!.GetValue<string>(), StringComparison.Ordinal);

        // The key goes in on standard input, kept only once the service accepts it.
        Assert.Equal(1, RunIn(local, "msy_wrong", "cloud", "key", "meshy", "--cloud-api", api, "--json").Code);
        Assert.False(File.Exists(Path.Combine(local, "Modelado", "keys", "meshy.key")));
        Assert.Equal(0, RunIn(local, Modelado.Cases.FixtureServer.MeshyKey, "cloud", "key", "meshy", "--cloud-api", api, "--json").Code);

        // The window's budget, set in settings.json, ends the consent line as it does there.
        Modelado.Inference.SettingsFile.Write(Path.Combine(local, "Modelado", "settings.json"), "cloudBudgets", new JsonObject { ["meshy"] = new JsonObject { ["monthly"] = 50, ["warnAt"] = 0.8 } });
        Assert.EndsWith("for about 30 credits. 50 of 50 left this month.", Report(RunIn(local, null, Estimate()).Out)["consent"]!.GetValue<string>(), StringComparison.Ordinal);

        (code, output, string error) = RunIn(local, null, Send());
        JsonObject sent = Report(output);
        Assert.True(code == 0, error);
        Assert.Equal((12, "30 credits"), (sent["triangles"]!.GetValue<int>(), sent["spent"]!.GetValue<string>()));
        Assert.Equal(12, Modelado.Geometry.MeshImport.Load(model).TriangleCount);

        // What About reads for its "1 job(s), estimated 30, charged 30" line.
        Assert.Equal([(Modelado.Cloud.CloudCapability.ImageTo3D, 1, 30.0, 30.0)], new Modelado.Cloud.CloudLedger(ledger).Drift("meshy"));

        // 20 left is too little for another: held before anything is sent, and nothing recorded.
        JsonObject held = Report(RunIn(local, null, Estimate()).Out);
        Assert.True(held["blocked"]!.GetValue<bool>());
        Assert.Contains("too small for it: ~30 with 20 of 50 left", held["consent"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(1, RunIn(local, null, Send()).Code);
        Assert.Single(new Modelado.Cloud.CloudLedger(ledger).Entries);
    }

    [Fact]
    public void An_edit_answers_one_json_object_with_its_outputs_counts_and_each_edit()
    {
        string cube = Path.Combine(Repository.Root, "cases", "fixtures", "cube.obj");
        string written = Path.Combine(folder, "cube.stl");

        (int code, string output, string error) = Run("edit", cube, "-o", written, "--subdivide", "--smooth", "1", "--json", "--provider", "cpu");

        Assert.Equal(0, code);
        JsonObject report = Assert.IsType<JsonObject>(JsonNode.Parse(output));
        Assert.Equal(("edit", 0, 48), (report["command"]!.GetValue<string>(), report["exitCode"]!.GetValue<int>(), report["triangles"]!.GetValue<int>()));
        Assert.Equal(Path.GetFullPath(written), report["written"]![0]!.GetValue<string>());
        Assert.Equal(["--subdivide", "--smooth"], report["edits"]!.AsArray().Select(e => e!["edit"]!.GetValue<string>()));
        Assert.True(File.Exists(written));
        Assert.Contains("cube.obj: 8 vertices", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Counts_are_written_in_one_culture_whatever_the_machine_is_set_to()
    {
        // MOD49: four subdivisions take the cube past a thousand triangles, where a pt-BR machine
        // would write 3.072, which an English reader takes for three.
        string model = Path.Combine(Repository.Root, "cases", "fixtures", "cube.obj");
        string error = "";
        for (int pass = 1; pass <= 4; pass++)
        {
            string next = Path.Combine(folder, $"pass{pass}.glb");
            (int code, _, error) = Run("edit", model, "-o", next, "--subdivide", "--provider", "cpu");
            Assert.Equal(0, code);
            model = next;
        }

        Assert.Contains("3,072 triangles", error + Run("edit", model, "-o", Path.Combine(folder, "last.glb"), "--provider", "cpu").Out, StringComparison.Ordinal);
    }

    [Fact]
    public void A_material_image_colours_a_model_without_texture_coordinates()
    {
        // MOD40: the cube has none, so the photo fixture is projected into its vertex colours.
        string cube = Path.Combine(Repository.Root, "cases", "fixtures", "cube.obj");
        string photo = Path.Combine(Repository.Root, "cases", "fixtures", "photo.png");
        string written = Path.Combine(folder, "stone.ply");

        (int code, string output, _) = Run("edit", cube, "-o", written, "--subdivide", "--texture", photo, "--tiles", "3", "--json", "--provider", "cpu");

        Assert.Equal(0, code);
        JsonObject report = Assert.IsType<JsonObject>(JsonNode.Parse(output));
        Assert.Equal(["--subdivide", "--texture"], report["edits"]!.AsArray().Select(e => e!["edit"]!.GetValue<string>()));
        Assert.Contains("property uchar red", File.ReadAllText(written), StringComparison.Ordinal);
    }

    [Fact]
    public void A_failure_is_an_error_in_the_json_and_exit_one()
    {
        (int code, string output, _) = Run("edit", Path.Combine(folder, "missing.obj"), "-o", Path.Combine(folder, "out.stl"), "--json", "--provider", "cpu");

        Assert.Equal(1, code);
        JsonObject report = Assert.IsType<JsonObject>(JsonNode.Parse(output));
        Assert.Equal(1, report["exitCode"]!.GetValue<int>());
        Assert.Contains("missing.obj", report["error"]!.GetValue<string>(), StringComparison.Ordinal);
    }
}
