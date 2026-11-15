using System.Net;
using System.Text.Json;
using GnazzosWorld.Api;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Contracts;
using GnazzosWorld.Core.Networking;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GnazzosWorld.Api.Tests;

public sealed class CatalogApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Factory(string environment = "Development", bool fail = false)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/GnazzosWorld.Api")));
            if (fail) builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPublicCatalog>(); services.AddSingleton<IPublicCatalog, FailingCatalog>();
            });
        }
        public HttpClient Client()
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
                { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            client.Timeout = Timeout.InfiniteTimeSpan; return client;
        }
    }
    private sealed class FailingCatalog : IPublicCatalog
    {
        public CatalogDocument Document => throw new InvalidOperationException("PRIVATE_TEST_MARKER");
    }
    [Fact]
    public async Task Chapter18_client_consumes_real_server_pipeline_and_validates_relationships()
    {
        using var factory = new Factory(); using var client = factory.Client();
        var source = new HttpCatalogDocumentSource(client, new Uri(client.BaseAddress!, "/api/v1/catalog"), TimeProvider.System);
        var document = await source.LoadAsync(Ct);
        Assert.Equal(1, document.SchemaVersion); Assert.Equal("fixture-2026-11-15-v1", document.Revision);
        Assert.Equal(5, document.Items.Count); Assert.NotEmpty(document.Creators);
        foreach (var item in document.Items.Where(i => i.CreatorId is not null))
            Assert.Contains(document.Creators, c => c.Id == item.CreatorId);
        Assert.Equal(document.Items.OrderBy(i => i.Id, StringComparer.Ordinal), document.Items);
    }
    [Fact]
    public async Task Chapter17_array_client_remains_compatible()
    {
        using var factory = new Factory(); using var client = factory.Client();
        var source = new HttpCatalogSource(client, new Uri(client.BaseAddress!, "/api/v1/catalog/items"), TimeProvider.System);
        Assert.Equal(5, (await source.LoadAsync(Ct)).Count);
    }
    [Fact]
    public async Task First_page_returns_revision_total_and_continuation()
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/catalog/pages?limit=2", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(5, root.GetProperty("total").GetInt32());
        Assert.Equal(2, root.GetProperty("nextOffset").GetInt32());
        Assert.Equal(2, root.GetProperty("items").GetArrayLength());
    }
    [Fact]
    public async Task Continuing_pages_reconstruct_same_order_without_duplicates()
    {
        using var factory = new Factory(); using var client = factory.Client();
        var full = await new HttpCatalogDocumentSource(client, new Uri(client.BaseAddress!, "/api/v1/catalog"), TimeProvider.System).LoadAsync(Ct);
        var ids = new List<string>(); int? offset = 0;
        while (offset is not null)
        {
            using var response = await client.GetAsync($"/api/v1/catalog/pages?offset={offset}&limit=2&revision={full.Revision}", Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            ids.AddRange(json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()!));
            var next = json.RootElement.GetProperty("nextOffset"); offset = next.ValueKind == JsonValueKind.Null ? null : next.GetInt32();
        }
        Assert.Equal(full.Items.Select(i => i.Id), ids); Assert.Equal(ids.Count, ids.Distinct().Count());
    }
    [Theory]
    [InlineData("limit=0")] [InlineData("limit=101")] [InlineData("limit=oops")]
    [InlineData("offset=-1")] [InlineData("offset=2147483648")]
    [InlineData("offset=1")] [InlineData("offset=6&revision=fixture-2026-11-15-v1")]
    [InlineData("limit=1&limit=2")] [InlineData("unknown=1")]
    [InlineData("revision=")] [InlineData("revision=bad%20revision")]
    public async Task Invalid_query_returns_validation_problem(string query)
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/catalog/pages?" + query, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.True(json.RootElement.TryGetProperty("errors", out _));
    }
    [Fact]
    public async Task Stale_page_revision_returns_conflict()
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/catalog/pages?offset=2&revision=older", Ct);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
    [Fact]
    public async Task End_offset_is_empty_terminal_page()
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/catalog/pages?offset=5&revision=fixture-2026-11-15-v1", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("nextOffset").ValueKind);
    }
    [Fact]
    public async Task Development_openapi_describes_read_routes_and_problem_responses()
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var response = await client.GetAsync("/openapi/v1.json", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var paths = json.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/catalog", out _));
        var page = paths.GetProperty("/api/v1/catalog/pages").GetProperty("get");
        Assert.True(page.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(page.GetProperty("responses").TryGetProperty("409", out _));
        Assert.False(paths.TryGetProperty("/api/v1/orders", out _));
    }
    [Fact]
    public async Task Production_openapi_is_not_mapped()
    {
        using var factory = new Factory("Production"); using var client = factory.Client();
        using var response = await client.GetAsync("/openapi/v1.json", Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Theory]
    [InlineData("/api/v1/orders")] [InlineData("/api/v1/quotes")] [InlineData("/api/v1/requests")]
    public async Task Customer_write_routes_do_not_exist(string path)
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var response = await client.PostAsync(path, new StringContent("{}"), Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact]
    public async Task Catalog_post_is_method_not_allowed()
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var response = await client.PostAsync("/api/v1/catalog", new StringContent("{}"), Ct);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
    [Theory]
    [InlineData("Development")] [InlineData("Production")]
    public async Task Unexpected_exception_returns_safe_problem_in_all_environments(string environment)
    {
        using var factory = new Factory(environment, true); using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/catalog", Ct);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("PRIVATE_TEST_MARKER", await response.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Health_contains_no_configuration_or_customer_fields()
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var response = await client.GetAsync("/health", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Single(json.RootElement.EnumerateObject());
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
    }
}
