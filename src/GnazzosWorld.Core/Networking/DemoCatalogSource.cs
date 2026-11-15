using System.Net;
using System.Net.Http.Headers;
using GnazzosWorld.Core.Catalog;

namespace GnazzosWorld.Core.Networking;

// Explicit in-process transport: no socket, DNS lookup, credentials, or service contact.
public sealed class DemoCatalogSource : ICatalogSource, IDisposable
{
    private readonly HttpClient client = new(new FixtureHandler()) { Timeout = Timeout.InfiniteTimeSpan };
    private readonly HttpCatalogSource source;
    public DemoCatalogSource()
        => source = new(client, new Uri("https://catalog.example.invalid/catalog/items"), TimeProvider.System);
    public Task<IReadOnlyList<CatalogItem>> LoadAsync(CancellationToken cancellationToken = default)
        => source.LoadAsync(cancellationToken);
    public void Dispose() => client.Dispose();

    private sealed class FixtureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (request.Method != HttpMethod.Get || request.RequestUri?.Host != "catalog.example.invalid")
                throw new InvalidOperationException("The demo handler accepts only its fixture request.");
            var json = typeof(CatalogLoader).Assembly.GetManifestResourceStream("GnazzosWorld.Core.Data.catalog.json")
                ?? throw new InvalidOperationException("Missing demo catalog.");
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(json) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }
    }
}
