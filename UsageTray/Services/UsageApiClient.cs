using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using UsageTray.Models;

namespace UsageTray.Services;

internal sealed class UsageApiClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public UsageApiClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<UsageResult> GetUsageAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var endpoint = BuildEndpoint(baseUrl);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = body.Length > 240 ? body[..240] + "…" : body;
            throw new UsageApiException(
                $"请求失败：HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n{detail}".Trim());
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return Parse(document.RootElement);
        }
        catch (JsonException ex)
        {
            throw new UsageApiException("接口返回的不是有效 JSON。", ex);
        }
    }

    internal static UsageResult Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new UsageApiException("接口 JSON 根节点必须是对象。");
        }

        var quota = TryGetObject(root, "quota");
        var remainingElement = FirstDefined(
            TryGet(root, "remaining"),
            quota is { } quotaValue ? TryGet(quotaValue, "remaining") : null,
            TryGet(root, "balance"));

        var remaining = ReadDecimal(remainingElement, "remaining / quota.remaining / balance");
        var unit = FirstString(
            TryGet(root, "unit"),
            quota is { } unitQuota ? TryGet(unitQuota, "unit") : null) ?? "USD";
        var isValid = FirstBoolean(
            TryGet(root, "is_active"),
            TryGet(root, "isValid")) ?? true;

        var total = ReadOptionalDecimal(FirstDefined(
            TryGet(root, "total"),
            TryGet(root, "limit"),
            quota is { } totalQuota ? TryGet(totalQuota, "total") : null,
            quota is { } limitQuota ? TryGet(limitQuota, "limit") : null));
        var used = ReadOptionalDecimal(FirstDefined(
            TryGet(root, "used"),
            quota is { } usedQuota ? TryGet(usedQuota, "used") : null));

        if (used is null && total is not null)
        {
            used = total - remaining;
        }

        var planName = FirstString(TryGet(root, "planName"));
        var mode = FirstString(TryGet(root, "mode"));
        var statistics = ReadStatistics(root);

        return new UsageResult(isValid, remaining, unit.Trim(), total, used,
            planName, mode, statistics);
    }

    private static UsageStatistics? ReadStatistics(JsonElement root)
    {
        var usage = TryGetObject(root, "usage");
        var today = usage is { } usageValue
            ? ReadUsagePeriod(TryGetObject(usageValue, "today"))
            : null;
        var allTime = usage is { } allTimeUsage
            ? ReadUsagePeriod(TryGetObject(allTimeUsage, "total"))
            : null;
        var models = ReadModelStats(TryGetArray(root, "model_stats"));

        if (usage is null && models.Count == 0)
        {
            return null;
        }

        return new UsageStatistics(
            today,
            allTime,
            usage is { } durationUsage
                ? ReadOptionalDecimal(TryGet(durationUsage, "average_duration_ms"))
                : null,
            usage is { } rpmUsage
                ? ReadOptionalDecimal(TryGet(rpmUsage, "rpm"))
                : null,
            usage is { } tpmUsage
                ? ReadOptionalDecimal(TryGet(tpmUsage, "tpm"))
                : null,
            models);
    }

    private static UsagePeriod? ReadUsagePeriod(JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var value = element.Value;
        return new UsagePeriod(
            ReadOptionalDecimal(TryGet(value, "cost")),
            ReadOptionalDecimal(TryGet(value, "actual_cost")),
            ReadOptionalInt64(TryGet(value, "requests")),
            ReadOptionalInt64(TryGet(value, "input_tokens")),
            ReadOptionalInt64(TryGet(value, "output_tokens")),
            ReadOptionalInt64(TryGet(value, "cache_creation_tokens")),
            ReadOptionalInt64(TryGet(value, "cache_read_tokens")),
            ReadOptionalInt64(TryGet(value, "total_tokens")));
    }

    private static IReadOnlyList<ModelUsageStat> ReadModelStats(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array })
        {
            return Array.Empty<ModelUsageStat>();
        }

        var models = new List<ModelUsageStat>();
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var model = FirstString(TryGet(item, "model"));
            if (string.IsNullOrWhiteSpace(model))
            {
                continue;
            }

            models.Add(new ModelUsageStat(
                model.Trim(),
                ReadOptionalDecimal(FirstDefined(
                    TryGet(item, "actual_cost"),
                    TryGet(item, "cost"),
                    TryGet(item, "account_cost"))),
                ReadOptionalInt64(TryGet(item, "requests")),
                ReadOptionalInt64(TryGet(item, "input_tokens")),
                ReadOptionalInt64(TryGet(item, "output_tokens")),
                ReadOptionalInt64(TryGet(item, "cache_creation_tokens")),
                ReadOptionalInt64(TryGet(item, "cache_read_tokens")),
                ReadOptionalInt64(TryGet(item, "total_tokens"))));
        }

        return models;
    }

    internal static Uri BuildEndpoint(string baseUrl)
    {
        var value = baseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(value, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new UsageApiException("Base URL 必须是有效的 http 或 https 地址。");
        }

        if (baseUri.AbsolutePath.EndsWith("/v1/usage", StringComparison.OrdinalIgnoreCase))
        {
            return baseUri;
        }

        var suffix = baseUri.AbsolutePath.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? "/usage"
            : "/v1/usage";
        return new Uri(value + suffix, UriKind.Absolute);
    }

    private static JsonElement? TryGet(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static JsonElement? TryGetObject(JsonElement element, string propertyName)
    {
        var value = TryGet(element, propertyName);
        return value is { ValueKind: JsonValueKind.Object } ? value : null;
    }

    private static JsonElement? TryGetArray(JsonElement element, string propertyName)
    {
        var value = TryGet(element, propertyName);
        return value is { ValueKind: JsonValueKind.Array } ? value : null;
    }

    private static JsonElement? FirstDefined(params JsonElement?[] values) =>
        values.FirstOrDefault(value => value is not null &&
            value.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined);

    private static string? FirstString(params JsonElement?[] values)
    {
        foreach (var value in values)
        {
            if (value is { ValueKind: JsonValueKind.String })
            {
                return value.Value.GetString();
            }
        }

        return null;
    }

    private static bool? FirstBoolean(params JsonElement?[] values)
    {
        foreach (var value in values)
        {
            if (value is { ValueKind: JsonValueKind.True })
            {
                return true;
            }

            if (value is { ValueKind: JsonValueKind.False })
            {
                return false;
            }
        }

        return null;
    }

    private static decimal ReadDecimal(JsonElement? value, string fieldName) =>
        ReadOptionalDecimal(value) ??
        throw new UsageApiException($"返回数据中缺少可解析的 {fieldName} 字段。");

    private static decimal? ReadOptionalDecimal(JsonElement? value)
    {
        if (value is { ValueKind: JsonValueKind.Number } && value.Value.TryGetDecimal(out var number))
        {
            return number;
        }

        if (value is { ValueKind: JsonValueKind.String } &&
            decimal.TryParse(value.Value.GetString(), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static long? ReadOptionalInt64(JsonElement? value)
    {
        if (value is { ValueKind: JsonValueKind.Number } && value.Value.TryGetInt64(out var number))
        {
            return number;
        }

        if (value is { ValueKind: JsonValueKind.String } &&
            long.TryParse(value.Value.GetString(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public void Dispose() => _httpClient.Dispose();
}

internal sealed class UsageApiException : Exception
{
    public UsageApiException(string message) : base(message)
    {
    }

    public UsageApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
