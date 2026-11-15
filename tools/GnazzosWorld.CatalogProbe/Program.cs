using GnazzosWorld.Core.Contracts;
using GnazzosWorld.Core.Networking;

if (args.Length != 1 || !Uri.TryCreate(args[0], UriKind.Absolute, out var endpoint)
    || endpoint.Scheme != Uri.UriSchemeHttps || endpoint.UserInfo.Length != 0 || endpoint.Fragment.Length != 0)
{
    Console.Error.WriteLine("Usage: CatalogProbe <HTTPS catalog document endpoint>"); return 2;
}
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
try
{
    var source = new HttpCatalogDocumentSource(client, endpoint, TimeProvider.System);
    var document = await source.LoadAsync(cancellation.Token);
    Console.WriteLine($"Schema {document.SchemaVersion}; revision {document.Revision}; " +
        $"{document.Items.Count} items; {document.Creators.Count} creators.");
    return 0;
}
catch (CatalogFetchException ex)
{ Console.Error.WriteLine($"Catalog load failed: {ex.Problem}; status {(int?)ex.Status}."); return 1; }
catch (OperationCanceledException)
{ Console.Error.WriteLine("Catalog load canceled."); return 130; }
