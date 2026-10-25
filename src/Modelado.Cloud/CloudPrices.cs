using System.Globalization;
using System.Text.Json;

namespace Modelado.Cloud;

/// <summary>One action's price: a base, and what each option value adds.</summary>
/// <param name="Base">What the action costs with no option that changes it.</param>
/// <param name="Options">Option name, then value, then what it adds, e.g. texture: { true: 10 }.</param>
public sealed record PriceRule(double Base, IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Options);

/// <summary>
/// A service's prices (MOD42), kept as a versioned JSON file beside its provider with the date they
/// were read off the service's pricing page, so an estimate can say how old it is. A provider's
/// EstimateCost reads it: the action's base plus what each chosen option adds, so every option
/// change moves the estimate before anything runs. A table older than 90 days is stale, and the
/// window flags it; the ledger's estimate against actual shows the drift either way.
/// <code>
/// { "provider": "meshy", "unit": "credits", "read": "2026-10-01", "source": "https://…/pricing",
///   "actions": { "ImageTo3D": { "base": 20, "options": { "texture": { "true": 10 } } } } }
/// </code>
/// </summary>
/// <param name="Provider">The provider's id.</param>
/// <param name="Unit">"credits", "USD" and so on.</param>
/// <param name="Read">When the prices were read.</param>
/// <param name="Source">Where they were read from.</param>
/// <param name="Actions">Each action's rule.</param>
public sealed record CloudPrices(string Provider, string Unit, DateOnly Read, string? Source, IReadOnlyDictionary<CloudCapability, PriceRule> Actions)
{
    /// <summary>How old a table may be before it is flagged.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(90);

    /// <summary>Reads a price table.</summary>
    /// <param name="json">Its text.</param>
    /// <returns>The table.</returns>
    /// <exception cref="InvalidDataException">It is not one.</exception>
    public static CloudPrices Parse(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            Dictionary<CloudCapability, PriceRule> actions = [];
            foreach (JsonProperty action in root.GetProperty("actions").EnumerateObject())
            {
                if (!Enum.TryParse(action.Name, out CloudCapability capability) || capability == CloudCapability.None)
                {
                    throw new InvalidDataException($"The price table names an action this build does not know: {action.Name}.");
                }

                Dictionary<string, IReadOnlyDictionary<string, double>> options = new(StringComparer.Ordinal);
                if (action.Value.TryGetProperty("options", out JsonElement optionsOf))
                {
                    foreach (JsonProperty option in optionsOf.EnumerateObject())
                    {
                        options[option.Name] = option.Value.EnumerateObject().ToDictionary(v => v.Name, v => v.Value.GetDouble(), StringComparer.Ordinal);
                    }
                }

                actions[capability] = new PriceRule(action.Value.GetProperty("base").GetDouble(), options);
            }

            return new CloudPrices(
                root.GetProperty("provider").GetString()!,
                root.GetProperty("unit").GetString()!,
                DateOnly.ParseExact(root.GetProperty("read").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                root.TryGetProperty("source", out JsonElement source) ? source.GetString() : null,
                actions);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"The price table does not read: {error.Message}", error);
        }
    }

    /// <summary>What a request would cost.</summary>
    /// <param name="request">The action and its options.</param>
    /// <returns>The estimate, or null for an action the table does not price.</returns>
    public CloudCost? Estimate(CloudJobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Actions.TryGetValue(request.Action, out PriceRule? rule))
        {
            return null;
        }

        double amount = rule.Base;
        foreach ((string name, string value) in request.Options ?? new Dictionary<string, string>())
        {
            if (rule.Options.TryGetValue(name, out IReadOnlyDictionary<string, double>? values) && values.TryGetValue(value, out double extra))
            {
                amount += extra;
            }
        }

        return new CloudCost(amount, Unit);
    }

    /// <summary>Whether the prices are older than <see cref="StaleAfter"/>.</summary>
    /// <param name="today">Today.</param>
    /// <returns>True when they are.</returns>
    public bool IsStale(DateOnly today) => today.DayNumber - Read.DayNumber > StaleAfter.TotalDays;
}
