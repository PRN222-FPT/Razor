using Razor.Infrastructure;
using ServiceLayer.DTOs;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class OpenRouterHttpClientConfigurationTests
{
    [Fact]
    public void Configure_AppliesConfiguredTimeout()
    {
        using var client = new HttpClient();

        OpenRouterHttpClientConfiguration.Configure(
            client,
            new OpenRouterOptions
            {
                Timeout = TimeSpan.FromMinutes(5)
            });

        Assert.Equal(TimeSpan.FromMinutes(5), client.Timeout);
    }
}
