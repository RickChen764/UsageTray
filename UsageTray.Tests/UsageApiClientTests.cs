using System.Text.Json;
using UsageTray.Models;
using UsageTray.Services;
using Xunit;

namespace UsageTray.Tests;

public sealed class UsageApiClientTests
{
    [Fact]
    public void Parse_ReadsTopLevelCcswitchFields()
    {
        var result = Parse("""{"is_active":true,"remaining":12.34,"unit":"USD"}""");

        Assert.True(result.IsValid);
        Assert.Equal(12.34m, result.Remaining);
        Assert.Equal("USD", result.Unit);
        Assert.Null(result.Total);
    }

    [Fact]
    public void Parse_ReadsNestedQuotaFieldsAndCalculatesUsed()
    {
        var result = Parse("""{"quota":{"remaining":"25.5","total":100,"unit":"CNY"}}""");

        Assert.True(result.IsValid);
        Assert.Equal(25.5m, result.Remaining);
        Assert.Equal(100m, result.Total);
        Assert.Equal(74.5m, result.Used);
        Assert.Equal("CNY", result.Unit);
    }

    [Fact]
    public void Parse_FallsBackToBalanceAndDefaultUnit()
    {
        var result = Parse("""{"isValid":false,"balance":8}""");

        Assert.False(result.IsValid);
        Assert.Equal(8m, result.Remaining);
        Assert.Equal("USD", result.Unit);
    }

    [Theory]
    [InlineData("https://example.com", "https://example.com/v1/usage")]
    [InlineData("https://example.com/v1/", "https://example.com/v1/usage")]
    [InlineData("https://example.com/v1/usage", "https://example.com/v1/usage")]
    public void BuildEndpoint_NormalizesSupportedBaseUrls(string baseUrl, string expected)
    {
        Assert.Equal(expected, UsageApiClient.BuildEndpoint(baseUrl).AbsoluteUri);
    }

    [Fact]
    public void Parse_RejectsResponseWithoutBalance()
    {
        Assert.Throws<UsageApiException>(() => Parse("""{"unit":"USD"}"""));
    }

    [Fact]
    public void Parse_ReadsTodayTotalRatesAndModelStatistics()
    {
        const string json = """
            {
              "balance": 9701.53,
              "isValid": true,
              "mode": "unrestricted",
              "planName": "钱包余额",
              "unit": "USD",
              "usage": {
                "average_duration_ms": 22524.1,
                "rpm": 2,
                "tpm": 76320,
                "today": {
                  "actual_cost": 0.691258,
                  "input_tokens": 97682,
                  "output_tokens": 2064,
                  "cache_read_tokens": 281856,
                  "requests": 4,
                  "total_tokens": 381602
                },
                "total": {
                  "actual_cost": 298.531,
                  "requests": 2600,
                  "total_tokens": 319051194
                }
              },
              "model_stats": [
                {
                  "model": "gpt-5.6-sol",
                  "requests": 2569,
                  "total_tokens": 318515216,
                  "actual_cost": 297.97
                }
              ]
            }
            """;

        var result = Parse(json);

        Assert.Equal("钱包余额", result.PlanName);
        Assert.Equal("unrestricted", result.Mode);
        Assert.NotNull(result.Statistics);
        Assert.Equal(0.691258m, result.Statistics.Today!.ActualCost);
        Assert.Equal(4, result.Statistics.Today.Requests);
        Assert.Equal(381602, result.Statistics.Today.TotalTokens);
        Assert.Equal(319051194, result.Statistics.AllTime!.TotalTokens);
        Assert.Equal(76320, result.Statistics.TokensPerMinute);
        Assert.Equal("gpt-5.6-sol", Assert.Single(result.Statistics.Models).Model);
    }

    private static UsageResult Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return UsageApiClient.Parse(document.RootElement);
    }
}
