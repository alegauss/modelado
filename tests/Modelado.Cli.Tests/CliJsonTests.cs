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

    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        ProcessStartInfo start = new("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(typeof(CliArguments).Assembly.Location);
        foreach (string arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using Process cli = Process.Start(start)!;
        Task<string> err = cli.StandardError.ReadToEndAsync();
        string output = cli.StandardOutput.ReadToEnd();
        cli.WaitForExit();
        return (cli.ExitCode, output, err.Result);
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
    public void A_failure_is_an_error_in_the_json_and_exit_one()
    {
        (int code, string output, _) = Run("edit", Path.Combine(folder, "missing.obj"), "-o", Path.Combine(folder, "out.stl"), "--json", "--provider", "cpu");

        Assert.Equal(1, code);
        JsonObject report = Assert.IsType<JsonObject>(JsonNode.Parse(output));
        Assert.Equal(1, report["exitCode"]!.GetValue<int>());
        Assert.Contains("missing.obj", report["error"]!.GetValue<string>(), StringComparison.Ordinal);
    }
}
