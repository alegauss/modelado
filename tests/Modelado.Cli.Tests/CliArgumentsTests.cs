namespace Modelado.Cli.Tests;

/// <summary>MOD48: modelado-cli refuses what it cannot run, as ERROR and an exit code, before anything loads.</summary>
public sealed class CliArgumentsTests
{
    [Fact]
    public void An_option_missing_its_value_is_a_usage_error()
    {
        (CliArguments? parsed, string? error, int code) = CliArguments.Parse(["edit", "x.glb", "-o", "y.glb", "--smooth"]);

        Assert.Null(parsed);
        Assert.Equal("--smooth needs a value.", error);
        Assert.Equal(CliArguments.MissingValue, code);
        Assert.Equal(1, code);
    }

    [Fact]
    public void A_misspelt_option_is_refused_rather_than_skipped()
    {
        (CliArguments? parsed, string? error, int code) = CliArguments.Parse(["edit", "x.glb", "--simplfy", "0.25", "-o", "y.glb"]);

        Assert.Null(parsed);
        Assert.Equal("Unknown option for edit: --simplfy.", error);
        Assert.Equal(2, code);
    }

    [Fact]
    public void An_option_of_another_command_is_refused()
    {
        (CliArguments? parsed, string? error, int code) = CliArguments.Parse(["relief", "photo.png", "-o", "r.glb", "--resolution", "256"]);

        Assert.Null(parsed);
        Assert.Equal("Unknown option for relief: --resolution.", error);
        Assert.Equal(2, code);
    }

    [Fact]
    public void An_unknown_command_is_refused()
    {
        (CliArguments? parsed, string? error, int code) = CliArguments.Parse(["sculpt", "x.glb"]);

        Assert.Null(parsed);
        Assert.Equal("Unknown command: sculpt.", error);
        Assert.Equal(2, code);
    }

    [Fact]
    public void Flags_take_no_value_and_options_take_the_next_argument()
    {
        (CliArguments? parsed, string? error, _) = CliArguments.Parse(
            ["edit", "x.glb", "--mirror", "-o", "y.stl", "--smooth", "3", "--size", "80", "--provider", "cpu", "--preview", "v.png"]);

        Assert.Null(error);
        Assert.NotNull(parsed);
        Assert.Equal("edit", parsed.Command);
        Assert.Equal(["x.glb"], parsed.Positional);
        Assert.Equal("true", parsed.Options["--mirror"]);
        Assert.Equal("y.stl", parsed.Options["--out"]);
        Assert.Equal("3", parsed.Options["--smooth"]);
        Assert.Equal("80", parsed.Options["--size"]);
        Assert.Equal("cpu", parsed.Options["--provider"]);
        Assert.Equal("v.png", parsed.Options["--preview"]);
    }

    [Fact]
    public void Every_option_the_usage_text_lists_is_accepted_by_its_command()
    {
        // The usage text's own options, so a new one added there and not here is caught.
        string[] objectRun = ["object", "p.png", "-o", "o.glb", "--resolution", "256", "--keep-background", "--threshold", "25", "--step", "2", "--smooth", "2", "--enhance", "--no-project", "--high", "--models", "m", "--manifest", "u"];
        string[] reliefRun = ["relief", "p.png", "-o", "r.glb", "--grid", "256", "--depth", "0.25", "--base", "0.1", "--remove-background"];
        string[] editRun = ["edit", "x.glb", "-o", "y.glb", "--rotate", "x:90", "--remove-pieces", "0.05", "--fill-holes", "--simplify", "0.25", "--subdivide", "--flatten", "0.03", "--upscale-texture"];

        string[] cloudRun = ["cloud", "remesh", "m.glb", "-o", "c.glb", "--service", "meshy", "--estimate", "--polycount", "9000", "--quads", "--cloud-api", "meshy=http://x/"];
        string[] retextureRun = ["cloud", "retexture", "m.glb", "-o", "c.glb", "--prompt", "rust", "--keep-uv", "--no-texture", "--pbr"];

        foreach (string[] run in new[] { objectRun, reliefRun, editRun, cloudRun, retextureRun, ["cloud", "key", "meshy"], ["download", "all"], ["models"] })
        {
            Assert.Null(CliArguments.Parse(run).Error);
        }
    }
}
