using System.Globalization;
using GnazzosWorld.Api.Identity;
using GnazzosWorld.Core.Networking;
using System.Text.Json.Serialization;
using GnazzosWorld.Api;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
ApiIdentity.AddIdentity(builder.Services, builder.Configuration);
builder.Services.AddSingleton(new OwnedRequestStore());
builder.Services.AddSingleton<IPublicCatalog>(_ => new PublicCatalog(CatalogSnapshot.LoadBundled(),
    builder.Configuration["Catalog:Revision"] ?? "fixture-2026-11-15-v1"));
var app = builder.Build();
_ = app.Services.GetRequiredService<IPublicCatalog>(); // Fail invalid fixture/config at startup.
app.UseExceptionHandler(error => error.Run(context => Results.Problem(
    statusCode: 500, title: "The catalog service could not complete the request.").ExecuteAsync(context)));
app.UseStatusCodePages(context => Results.Problem(statusCode: context.HttpContext.Response.StatusCode,
    title: "The request could not be completed.").ExecuteAsync(context.HttpContext));
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/v1/requests/{id}", (string id, HttpContext context, OwnedRequestStore requests) =>
{
    var row = requests.ForOwner(id, context.User);
    return row is null ? Results.NotFound() : Results.Ok(new RequestPreview(row.Id, row.Summary));
}).RequireAuthorization(ApiIdentity.ReadPolicy);
app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" })).WithName("Health");
app.MapGet("/api/v1/catalog", (IPublicCatalog catalog) => TypedResults.Ok(catalog.Document))
    .WithName("GetCatalog").ProducesProblem(500);
// Retains Chapter 17's small unpaged array contract; no customer data or request writes.
app.MapGet("/api/v1/catalog/items", (IPublicCatalog catalog) => TypedResults.Ok(catalog.Document.Items))
    .WithName("GetCatalogItems").ProducesProblem(500);
app.MapGet("/api/v1/catalog/pages", (HttpRequest request, IPublicCatalog catalog,
    string? offset, string? limit, string? revision) =>
{
    var errors = new Dictionary<string, string[]>();
    var allowed = new[] { "offset", "limit", "revision" };
    if (request.Query.Keys.Any(k => !allowed.Contains(k, StringComparer.Ordinal)))
        errors["query"] = ["Only offset, limit and revision are supported."];
    int Number(string key, string? raw, int fallback)
    {
        if (!request.Query.TryGetValue(key, out var values)) return fallback;
        if (values.Count != 1 || !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        { errors[key] = ["Use one nonnegative integer."]; return fallback; }
        return value;
    }
    var offsetValue = Number("offset", offset, 0); var limitValue = Number("limit", limit, 20);
    if (limitValue is < 1 or > 100) errors["limit"] = ["Limit must be between 1 and 100."];
    if (request.Query.TryGetValue("revision", out var revisions))
    {
        if (revisions.Count != 1 || !CatalogDocument.IsValidRevision(revisions[0]))
            errors["revision"] = ["Use one valid catalog revision."];
        else revision = revisions[0];
    }
    if (offsetValue > 0 && revision is null) errors["revision"] = ["Supply the first page's revision when continuing."];
    if (errors.Count != 0) return Results.ValidationProblem(errors);
    var document = catalog.Document;
    if (revision is not null && revision != document.Revision)
        return Results.Problem(statusCode: 409, title: "Catalog revision changed. Restart paging.");
    if (offsetValue > document.Items.Count)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["offset"] = ["Offset exceeds this catalog."] });
    var items = document.Items.Skip(offsetValue).Take(limitValue).ToArray();
    int? next = offsetValue + items.Length < document.Items.Count ? offsetValue + items.Length : null;
    return Results.Ok(new CatalogPageDto(1, document.Revision, offsetValue, limitValue, document.Items.Count, next, items));
}).WithName("GetCatalogPage").Produces<CatalogPageDto>().ProducesValidationProblem().ProducesProblem(409).ProducesProblem(500);
app.Run();

public partial class Program { }
