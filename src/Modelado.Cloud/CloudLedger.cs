using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modelado.Cloud;

/// <summary>One job this app ran through a service.</summary>
/// <param name="Provider">The provider's id.</param>
/// <param name="Action">What it was asked to do.</param>
/// <param name="Amount">What it cost, in <paramref name="Unit"/>; 0 for a job that made nothing.</param>
/// <param name="Unit">The service's unit, e.g. "credits".</param>
/// <param name="At">When it finished.</param>
/// <param name="Outcome">succeeded, failed or cancelled.</param>
public sealed record LedgerEntry(string Provider, CloudCapability Action, double Amount, string Unit, DateTimeOffset At, string Outcome);

/// <summary>
/// What this app spent through each service (MOD41): every job's provider, action, cost, time and
/// outcome, one JSON line each in cloud-ledger.jsonl beside settings.json, appended as each job
/// ends, so it survives a restart and a crash loses at most the job running. Where a service
/// reports no balance, this is the only figure there is, and the window says it is this app's own.
/// </summary>
public sealed class CloudLedger
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };
    private readonly string path;

    /// <summary>A ledger in a file.</summary>
    /// <param name="path">The .jsonl file; created on the first entry.</param>
    public CloudLedger(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        this.path = path;
    }

    /// <summary>%LOCALAPPDATA%\Modelado\cloud-ledger.jsonl.</summary>
    public static string DefaultPath =>
        Path.Combine(
            Environment.GetEnvironmentVariable("LOCALAPPDATA") is { Length: > 0 } local ? local : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Modelado",
            "cloud-ledger.jsonl");

    /// <summary>Every entry, oldest first; a line that does not read is skipped rather than lost with the rest.</summary>
    public IReadOnlyList<LedgerEntry> Entries
    {
        get
        {
            if (!File.Exists(path))
            {
                return [];
            }

            List<LedgerEntry> entries = [];
            foreach (string line in File.ReadLines(path))
            {
                try
                {
                    if (line.Length > 0 && JsonSerializer.Deserialize<LedgerEntry>(line, Json) is { } entry)
                    {
                        entries.Add(entry);
                    }
                }
                catch (JsonException)
                {
                    // A torn last line from a crash; the entries before it stand.
                }
            }

            return entries;
        }
    }

    /// <summary>Adds an entry.</summary>
    /// <param name="entry">The job.</param>
    public void Record(LedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.AppendAllText(path, JsonSerializer.Serialize(entry, Json) + "\n");
    }

    /// <summary>What was spent through a service since a moment.</summary>
    /// <param name="provider">The provider's id.</param>
    /// <param name="since">The start; null for all time.</param>
    /// <returns>The sum, in the service's unit.</returns>
    public double Spent(string provider, DateTimeOffset? since = null) =>
        Entries.Where(e => e.Provider == provider && (since is null || e.At >= since)).Sum(e => e.Amount);

    /// <summary>The first moment of the month <paramref name="now"/> is in, in its own offset.</summary>
    /// <param name="now">A moment.</param>
    /// <returns>The month's start.</returns>
    public static DateTimeOffset MonthStart(DateTimeOffset now) => new(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
}

/// <summary>A user's monthly limit for one service, and when to warn before it.</summary>
/// <param name="Monthly">The limit, in the service's unit.</param>
/// <param name="WarnAt">The share of it that warns; 80% unless set.</param>
public sealed record CloudBudget(double Monthly, double WarnAt = 0.8)
{
    /// <summary>Where this month's spend stands against the limit.</summary>
    /// <param name="spent">This month's spend.</param>
    /// <returns>Ok, Warning past <see cref="WarnAt"/>, Exceeded at the limit.</returns>
    public BudgetState StateFor(double spent) =>
        Monthly <= 0 ? BudgetState.Exceeded : spent >= Monthly ? BudgetState.Exceeded : spent >= Monthly * WarnAt ? BudgetState.Warning : BudgetState.Ok;
}

/// <summary>How close a service is to the user's limit.</summary>
public enum BudgetState
{
    /// <summary>No limit set.</summary>
    None,

    /// <summary>Under the warning share.</summary>
    Ok,

    /// <summary>Past it: the chip turns amber and the consent line says so.</summary>
    Warning,

    /// <summary>At or past the limit: jobs to the service are refused until it is raised.</summary>
    Exceeded,
}

/// <summary>A job refused because its service's monthly budget is spent.</summary>
public sealed class BudgetExceededException : InvalidOperationException
{
    /// <summary>A refusal with its words.</summary>
    /// <param name="message">What a person reads.</param>
    public BudgetExceededException(string message)
        : base(message)
    {
    }

    /// <summary>A refusal with no words of its own.</summary>
    public BudgetExceededException()
    {
    }

    /// <summary>A refusal wrapping another.</summary>
    /// <param name="message">What a person reads.</param>
    /// <param name="inner">The cause.</param>
    public BudgetExceededException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// The ledger and the budgets a job runs under (MOD41): where it is recorded, and whether its
/// service may take another one this month.
/// </summary>
/// <param name="Ledger">Where jobs are recorded.</param>
/// <param name="Budgets">The user's limits by provider id; a provider with none is unlimited.</param>
/// <param name="Clock">Now; the system clock unless a test sets one.</param>
public sealed record CloudSpending(CloudLedger Ledger, IReadOnlyDictionary<string, CloudBudget> Budgets, Func<DateTimeOffset>? Clock = null)
{
    private DateTimeOffset Now => (Clock ?? (() => DateTimeOffset.Now))();

    /// <summary>What a service's budget stands at this month.</summary>
    /// <param name="provider">The provider's id.</param>
    /// <returns>The state, and the month's spend.</returns>
    public (BudgetState State, double Spent) StateOf(string provider)
    {
        double spent = Ledger.Spent(provider, CloudLedger.MonthStart(Now));
        return (Budgets.TryGetValue(provider, out CloudBudget? budget) ? budget.StateFor(spent) : BudgetState.None, spent);
    }

    /// <summary>Records a finished job at the time it finished.</summary>
    /// <param name="provider">The provider's id.</param>
    /// <param name="action">What it did.</param>
    /// <param name="cost">What it cost, or null where nothing was spent.</param>
    /// <param name="outcome">succeeded, failed or cancelled.</param>
    public void Record(string provider, CloudCapability action, CloudCost? cost, string outcome) =>
        Ledger.Record(new LedgerEntry(provider, action, cost?.Amount ?? 0, cost?.Unit ?? "", Now, outcome));
}
