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
        "--mirror", "--fill-holes", "--subdivide", "--upscale-texture", "--json",
    };

    private static readonly string[] Common = ["--provider", "--high", "--models", "--manifest", "--source", "--size", "--preview", "--json"];

    private static readonly Dictionary<string, HashSet<string>> Accepted = new(StringComparer.Ordinal)
    {
        ["object"] = Takes("--out", "--resolution", "--keep-background", "--threshold", "--step", "--smooth", "--enhance", "--no-project"),
        ["relief"] = Takes("--out", "--grid", "--depth", "--base", "--remove-background", "--depth-model"),
        ["edit"] = Takes("--out", "--rotate", "--mirror", "--remove-pieces", "--fill-holes", "--simplify", "--subdivide", "--smooth", "--flatten", "--upscale-texture"),
        ["download"] = Takes(),
        ["models"] = Takes(),
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

        return (new CliArguments(command, positional, options), null, 0);
    }

    private static HashSet<string> Takes(params string[] own) => new(own.Concat(Common), StringComparer.Ordinal);
}
