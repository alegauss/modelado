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
        string unit = standing.Service.Prices?.Unit ?? Strings.SpentUnit;
        string spent = standing.Budget is { } budget
            ? Ui.Format(Strings.ChipSpentOfBudget, standing.State.Spent, budget.Monthly, unit)
            : Ui.Format(Strings.ChipSpent, standing.State.Spent, unit);
        string chip = standing.State.State switch
        {
            BudgetState.Warning => Strings.ChipServiceNearlySpent,
            BudgetState.Exceeded => Strings.ChipServiceSpent,
            _ => Strings.ChipService,
        };
        CloudChip.Content = Ui.Format(chip, standing.Service.DisplayName, spent);
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
        Identify(budget, $"CloudBudget-{service.Id}", Ui.Format(Strings.MonthlyBudgetName, service.DisplayName));
        Button save = new() { Content = Strings.SetMonthlyBudget, Padding = new Thickness(12, 2, 12, 2) };
        Identify(save, $"CloudBudgetSave-{service.Id}", Ui.Format(Strings.SetMonthlyBudgetName, service.DisplayName));
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
                spent.Text = Strings.BudgetInvalid;
                return;
            }

            try
            {
                SaveBudget(service.Id, limit);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                spent.Text = Ui.Format(Strings.BudgetNotSaved, error.Message);
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
            Ui.Format(Strings.UsageMonth, month, unit, today, ledger.Spent(service.Id)),
        ];
        if (LoadBudgets().TryGetValue(service.Id, out CloudBudget? budget))
        {
            string state = budget.StateFor(month) switch
            {
                BudgetState.Warning => Strings.BudgetNearlySpent,
                BudgetState.Exceeded => Strings.BudgetSpent,
                _ => Strings.BudgetWithin,
            };
            lines.Add(Ui.Format(state, budget.Monthly));
        }

        foreach ((CloudCapability action, int jobs, double estimated, double actual) in ledger.Drift(service.Id))
        {
            lines.Add(Ui.Format(Strings.UsageDrift, CloudJobs.Label(service, action), jobs, estimated, actual));
        }

        if (service.Prices is { } prices)
        {
            lines.Add(Ui.Format(prices.IsStale(DateOnly.FromDateTime(now.Date)) ? Strings.PricesStale : Strings.PricesRead, prices.Read));
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
                lines.Insert(1, Ui.Format(Strings.BalanceLeft, service.DisplayName, usage.Summary));
                spent.Text = string.Join(Environment.NewLine, lines);
            }
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException or InvalidDataException)
        {
            lines.Insert(1, Ui.Format(Strings.BalanceUnavailable, service.DisplayName, error.Message));
            spent.Text = string.Join(Environment.NewLine, lines);
        }
    }
}
