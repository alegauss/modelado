namespace Modelado.Cli;

/// <summary>
/// modelado-cli's arguments, split into the command, its positional arguments and its options
/// (MOD48). Each option is checked against the ones its command takes, so a misspelt edit is
/// refused rather than skipped and reported as success, and an option missing its value is a
/// usage error rather than a stack trace.
/// </summary>
/// <param name="Command">The first argument: object, relief, edit, download or models.</param>
/// <param name="Positional">The arguments that are not options, in order.</param>
/// <param name="Options">Each option given, -o read as --out; a flag's value is "true".</param>
public sealed record CliArguments(string Command, IReadOnlyList<string> Positional, IReadOnlyDictionary<string, string?> Options)
{
    /// <summary>The exit code for an option missing its value, as for any other ERROR.</summary>
    public const int MissingValue = 1;

    /// <summary>The exit code for an unknown command or option, the code the usage text goes with.</summary>
    public const int Unknown = 2;

    private static readonly HashSet<string> Flags = new(StringComparer.Ordinal)
    {
        "--keep-background", "--remove-background", "--high", "--enhance", "--no-project",
        "--mirror", "--fill-holes", "--subdivide", "--upscale-texture", "--json", "--dry-run",
        "--estimate", "--no-texture", "--pbr", "--quads", "--keep-uv", "--compact", "--stand", "--lay-flat",
    };

    // MOD106: the relief's text and how it is sized and placed.
    private static readonly string[] TextOptions = ["--text", "--text-mm", "--text-raise-mm", "--text-on"];

    private static readonly string[] Common = ["--provider", "--high", "--models", "--manifest", "--source", "--size", "--preview", "--json", "--compact"];

    private static readonly Dictionary<string, HashSet<string>> Accepted = new(StringComparer.Ordinal)
    {
        ["object"] = Takes("--out", "--resolution", "--keep-background", "--threshold", "--step", "--smooth", "--enhance", "--no-project"),
        ["relief"] = Takes("--out", "--grid", "--depth", "--base", "--width-mm", "--depth-mm", "--base-mm", "--keychain", "--hole-mm", "--shape", "--border-mm", "--border-height-mm", "--text", "--text-mm", "--text-raise-mm", "--text-on", "--stand", "--stand-tilt", "--remove-background", "--depth-model"),
        ["lithophane"] = Takes("--out", "--width-mm", "--min-mm", "--max-mm"),
        ["edit"] = Takes("--out", "--rotate", "--mirror", "--lay-flat", "--remove-pieces", "--fill-holes", "--make-printable", "--simplify", "--subdivide", "--smooth", "--flatten", "--texture", "--tiles", "--upscale-texture"),
        ["download"] = Takes("--dry-run", "--remove-background"),
        ["models"] = Takes(),
        ["cloud"] = Takes("--out", "--service", "--estimate", "--no-texture", "--pbr", "--quads", "--cloud-api", "--polycount", "--prompt", "--keep-uv"),
    };

    /// <summary>The commands modelado-cli knows.</summary>
    public static IReadOnlyCollection<string> Commands => Accepted.Keys;

    /// <summary>Parses the arguments.</summary>
    /// <param name="args">As the shell passed them, the command first.</param>
    /// <returns>The arguments, or why they were refused and the exit code that says so.</returns>
    public static (CliArguments? Arguments, string? Error, int ExitCode) Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0)
        {
            return (null, "Which command?", Unknown);
        }

        string command = args[0];
        if (!Accepted.TryGetValue(command, out HashSet<string>? accepted))
        {
            return (null, $"Unknown command: {command}.", Unknown);
        }

        Dictionary<string, string?> options = new(StringComparer.Ordinal);
        List<string> positional = [];
        for (int i = 1; i < args.Count; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) && args[i] != "-o")
            {
                positional.Add(args[i]);
                continue;
            }

            string key = args[i] == "-o" ? "--out" : args[i];
            if (!accepted.Contains(key))
            {
                return (null, $"Unknown option for {command}: {args[i]}.", Unknown);
            }

            if (Flags.Contains(key))
            {
                options[key] = "true";
            }
            else if (i + 1 < args.Count)
            {
                options[key] = args[++i];
            }
            else
            {
                return (null, $"{args[i]} needs a value.", MissingValue);
            }
        }

        return Conflict(options) is { } conflict
            ? (null, conflict, Unknown)
            : (new CliArguments(command, positional, options), null, 0);
    }

    // MOD102: a relief is sized in shares of its width or in millimetres, never both, since --depth
    // 0.25 read as millimetres (or --depth-mm 3 read as a share) would make a different object.
    private static string? Conflict(Dictionary<string, string?> options)
    {
        bool millimetres = options.ContainsKey("--width-mm");
        if (millimetres && (options.ContainsKey("--depth") || options.ContainsKey("--base")))
        {
            return "--width-mm sizes the relief in millimetres: give its depth and base as --depth-mm and --base-mm, not --depth or --base.";
        }

        if (!millimetres && (options.ContainsKey("--depth-mm") || options.ContainsKey("--base-mm")))
        {
            return "--depth-mm and --base-mm need the relief's width as --width-mm.";
        }

        // MOD105: the border is drawn in millimetres along the outline.
        if (!millimetres && (options.ContainsKey("--border-mm") || options.ContainsKey("--border-height-mm")))
        {
            return "--border-mm and --border-height-mm need the relief's width as --width-mm.";
        }

        // MOD106: letters are sized in millimetres too.
        if (!millimetres && TextOptions.Any(options.ContainsKey))
        {
            return "--text sizes its letters in millimetres: give the relief's width as --width-mm.";
        }

        // MOD107: the stand's slot is cut to the relief's thickness in millimetres, so the relief is
        // sized in them, and saved at that size: --size would rescale it out of the slot.
        bool stand = options.ContainsKey("--stand") || options.ContainsKey("--stand-tilt");
        if (stand && (!millimetres || options.ContainsKey("--size")))
        {
            return "--stand cuts its slot to the relief's thickness: give its width as --width-mm, and no --size.";
        }

        if (options.ContainsKey("--stand-tilt") && !options.ContainsKey("--stand"))
        {
            return "--stand-tilt is the stand's lean: give --stand too.";
        }

        // MOD104: a keychain's hole is sized in millimetres, so the tag is too.
        if (!millimetres && (options.ContainsKey("--keychain") || options.ContainsKey("--hole-mm")))
        {
            return "--keychain sizes the tag and its hole in millimetres: give its width as --width-mm.";
        }

        if (options.ContainsKey("--hole-mm") && !options.ContainsKey("--keychain"))
        {
            return "--hole-mm is the keychain's hole: give --keychain circle or tag too.";
        }

        // MOD105: a keychain cuts its own outline, so a second one would be ignored.
        return options.ContainsKey("--shape") && options.ContainsKey("--keychain")
            ? "--keychain cuts its own outline: give --shape or --keychain, not both."
            : null;
    }

    private static HashSet<string> Takes(params string[] own) => new(own.Concat(Common), StringComparer.Ordinal);
}
