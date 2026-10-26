using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Modelado.Cloud;
using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// What each cloud service has cost, and the limit set on it (MOD55), over MOD41's ledger and
/// budgets and MOD42's prices: in About, a row per service with this app's spend, the balance the
/// service reports, the monthly budget the user sets, estimate against actual and the price
/// table's age; on the status bar, a chip once a key is stored, which says when a budget is nearly
/// spent; and on every consent line, what the budget leaves, with Send held when it cannot cover
/// the action.
/// </summary>
public partial class MainWindow
{
    private const string BudgetsKey = CloudBudget.SettingsKey;

    private static Dictionary<string, CloudBudget> LoadBudgets() => CloudBudget.FromSettings(SettingsFile.ReadObject(SettingsFile.DefaultPath, BudgetsKey));

    private static void SaveBudget(string id, double? monthly)
    {
        JsonObject kept = SettingsFile.ReadObject(SettingsFile.DefaultPath, BudgetsKey)?.DeepClone().AsObject() ?? [];
        if (monthly is { } limit)
        {
            kept[id] = new JsonObject { ["monthly"] = limit, ["warnAt"] = 0.8 };
        }
        else
        {
            kept.Remove(id);
        }

        SettingsFile.Write(SettingsFile.DefaultPath, BudgetsKey, kept);
    }

    private static CloudSpending Spending() => new(new CloudLedger(CloudLedger.DefaultPath), LoadBudgets());

    // What the budget leaves for an action, said on its consent line; null to send is held.
    private static (string Note, bool Blocked) BudgetNote(ICloudProvider service, CloudJobRequest request) => Spending().Note(service, request);

    // The chip: the keyed service closest to its budget, or this month's spend where none is set.
    private void UpdateCloudChip()
    {
        if (CloudChip is null)
        {
            return;
        }

        List<ICloudProvider> keyed = [.. CloudServices.Where(p => cloudKeys.Has(p.Id))];
        CloudChip.Visibility = keyed.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (keyed.Count == 0)
        {
            return;
        }

        CloudSpending spending = Spending();
        var standing = keyed
            .Select(p => (Service: p, State: spending.StateOf(p.Id), Budget: spending.Budgets.GetValueOrDefault(p.Id)))
            .OrderByDescending(s => s.Budget is null ? -1 : s.State.Spent / s.Budget.Monthly)
            .First();
        string unit = standing.Service.Prices?.Unit ?? "spent";
        string spent = standing.Budget is { } budget
            ? string.Create(CultureInfo.InvariantCulture, $"{standing.State.Spent:0.##} of {budget.Monthly:0.##} {unit} this month")
            : string.Create(CultureInfo.InvariantCulture, $"{standing.State.Spent:0.##} {unit} this month");
        string warning = standing.State.State switch
        {
            BudgetState.Warning => " · nearly spent",
            BudgetState.Exceeded => " · spent",
            _ => "",
        };
        CloudChip.Content = $"{standing.Service.DisplayName} · {spent}{warning}";
        CloudChip.Style = standing.State.State is BudgetState.Warning or BudgetState.Exceeded
            ? (Style)FindResource("AccentButtonStyle")
            : null;
    }

    private void OnCloudChip(object sender, RoutedEventArgs e)
    {
        if (!AboutOpen)
        {
            ShowAbout(true);
        }

        AboutCloudServices.BringIntoView();
    }

    // About's usage half of a service's row: what it cost, its balance, its budget and its prices.
    private StackPanel CloudUsageRow(ICloudProvider service)
    {
        StackPanel usage = new() { Margin = new Thickness(0, 6, 0, 0) };
        TextBlock spent = new() { Style = (Style)FindResource("StatCaption"), TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAutomationId(spent, $"CloudUsage-{service.Id}");
        TextBox budget = new() { Width = 90, Margin = new Thickness(0, 0, 8, 0) };
        Identify(budget, $"CloudBudget-{service.Id}", $"{service.DisplayName} monthly budget");
        Button save = new() { Content = "Set the monthly budget", Padding = new Thickness(12, 2, 12, 2) };
        Identify(save, $"CloudBudgetSave-{service.Id}", $"Set the {service.DisplayName} monthly budget");
        if (LoadBudgets().TryGetValue(service.Id, out CloudBudget? set))
        {
            budget.Text = set.Monthly.ToString(CultureInfo.InvariantCulture);
        }

        save.Click += (_, _) =>
        {
            string typed = budget.Text.Trim().Replace(',', '.');
            double? limit = typed.Length == 0 ? null
                : double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value > 0 ? value : -1;
            if (limit is < 0)
            {
                spent.Text = "A budget is a number above 0, or empty for none.";
                return;
            }

            try
            {
                SaveBudget(service.Id, limit);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                spent.Text = $"The budget could not be saved: {error.Message}";
                return;
            }

            _ = ShowCloudUsageAsync(service, spent);
            UpdateCloudChip();
        };

        StackPanel limitRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        limitRow.Children.Add(budget);
        limitRow.Children.Add(save);
        usage.Children.Add(spent);
        usage.Children.Add(limitRow);
        _ = ShowCloudUsageAsync(service, spent);
        return usage;
    }

    private async Task ShowCloudUsageAsync(ICloudProvider service, TextBlock spent)
    {
        CloudLedger ledger = new(CloudLedger.DefaultPath);
        DateTimeOffset now = DateTimeOffset.Now;
        string unit = service.Prices?.Unit ?? "";
        double month = ledger.Spent(service.Id, CloudLedger.MonthStart(now));
        double today = ledger.Spent(service.Id, new DateTimeOffset(now.Date, now.Offset));
        List<string> lines =
        [
            string.Create(CultureInfo.InvariantCulture, $"This month {month:0.##} {unit} (today {today:0.##}, in all {ledger.Spent(service.Id):0.##}), as this app counts it."),
        ];
        if (LoadBudgets().TryGetValue(service.Id, out CloudBudget? budget))
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"Budget {budget.Monthly:0.##} a month: {budget.StateFor(month) switch { BudgetState.Warning => "nearly spent", BudgetState.Exceeded => "spent", _ => "within it" }}."));
        }

        foreach ((CloudCapability action, int jobs, double estimated, double actual) in ledger.Drift(service.Id))
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"{CloudJobs.Label(service, action)}: {jobs} job(s), estimated {estimated:0.##}, charged {actual:0.##}."));
        }

        if (service.Prices is { } prices)
        {
            lines.Add(prices.IsStale(DateOnly.FromDateTime(now.Date))
                ? $"Its prices were read on {prices.Read:yyyy-MM-dd}, over 90 days ago, so the estimates may be off."
                : $"Prices read on {prices.Read:yyyy-MM-dd}.");
        }

        spent.Text = string.Join(Environment.NewLine, lines);
        if (cloudKeys.Load(service.Id) is not { } key)
        {
            return;
        }

        try
        {
            if (await service.UsageAsync(key, CancellationToken.None).ConfigureAwait(true) is { } usage)
            {
                lines.Insert(1, $"{service.DisplayName} reports {usage.Summary} left.");
                spent.Text = string.Join(Environment.NewLine, lines);
            }
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException or InvalidDataException)
        {
            lines.Insert(1, $"{service.DisplayName} could not be asked for its balance: {error.Message}");
            spent.Text = string.Join(Environment.NewLine, lines);
        }
    }
}
