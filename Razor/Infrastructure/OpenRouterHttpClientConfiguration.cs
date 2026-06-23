using ServiceLayer.DTOs;

namespace Razor.Infrastructure;

public static class OpenRouterHttpClientConfiguration
{
    public static void Configure(HttpClient client, OpenRouterOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        client.Timeout = options.Timeout;
    }
}
