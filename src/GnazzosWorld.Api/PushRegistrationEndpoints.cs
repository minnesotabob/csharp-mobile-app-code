using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;
using GnazzosWorld.Core.Notifications;
using GnazzosWorld.Api.Identity;

namespace GnazzosWorld.Api;

/// <summary>Bounded volatile registration teaching store. No provider credentials or outbound push sender.</summary>
public sealed class PushInstallationRegistry(TimeProvider clock)
{
    private sealed record Row(string Owner, PushBinding Binding, DateTimeOffset Expires)
    { public override string ToString() => "Push installation [token redacted]"; }
    private readonly Dictionary<string, Row> rows = new(StringComparer.Ordinal);
    private readonly object sync = new();
    private void Prune() { foreach (var key in rows.Where(x => x.Value.Expires <= clock.GetUtcNow()).Select(x => x.Key).ToArray()) rows.Remove(key); }
    public bool Upsert(string owner, PushBinding binding)
    {
        if (!binding.IsValid()) return false;
        lock (sync)
        {
            Prune();
            if (rows.TryGetValue(binding.InstallationId, out var old) &&
                (old.Owner != owner || binding.Generation <= old.Binding.Generation)) return false;
            if (rows.Values.Any(x => x.Binding.InstallationId != binding.InstallationId && x.Binding.Token == binding.Token)) return false;
            if (!rows.ContainsKey(binding.InstallationId) && (rows.Count >= 100 || rows.Values.Count(x => x.Owner == owner) >= 4)) return false;
            rows[binding.InstallationId] = new(owner, binding, clock.GetUtcNow().AddHours(24)); return true;
        }
    }
    public void Remove(string owner, string id)
    { lock (sync) { Prune(); if (rows.TryGetValue(id, out var row) && row.Owner == owner) rows.Remove(id); } }
    public int CountFor(string owner) { lock (sync) { Prune(); return rows.Values.Count(x => x.Owner == owner); } }
}
public static class PushRegistrationEndpoints
{
    public const string Policy = "notifications-manage";
    public static void Register(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(TimeProvider.System); builder.Services.AddSingleton<PushInstallationRegistry>();
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("push-registration-budget", limiter =>
        { limiter.PermitLimit = 8; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; limiter.AutoReplenishment = true; }));
        builder.Services.AddAuthorizationBuilder().AddPolicy(Policy, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
        {
            var subjects = context.User.FindAll("sub").ToArray();
            return subjects.Length == 1 && !string.IsNullOrWhiteSpace(subjects[0].Value) && subjects[0].Value.Length <= 256 &&
                !subjects[0].Value.Any(char.IsControl) && context.User.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Contains("notifications:manage", StringComparer.Ordinal);
        }));
    }
    private static string Owner(ClaimsPrincipal user) => user.FindFirst("iss")!.Value + "\n" + user.FindFirst("sub")!.Value;
    public static void Map(WebApplication app)
    {
        app.MapPut("/api/v1/notification-installations/{id}", async (string id, HttpContext context, PushInstallationRegistry registry) =>
        {
            if (context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true) return Results.StatusCode(415);
            var bytes = new byte[4097]; var length = 0;
            while (length < bytes.Length)
            {
                var read = await context.Request.Body.ReadAsync(bytes.AsMemory(length), context.RequestAborted);
                if (read == 0) break; length += read;
            }
            if (length > 4096) return Results.StatusCode(413);
            PushBinding? binding;
            try
            {
                binding = JsonSerializer.Deserialize<PushBinding>(bytes.AsSpan(0, length), new JsonSerializerOptions(JsonSerializerDefaults.Web)
                { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow });
            }
            catch (JsonException) { return Results.BadRequest(); }
            if (binding is null || !binding.IsValid() || binding.InstallationId != id) return Results.BadRequest();
            return registry.Upsert(Owner(context.User), binding) ? Results.NoContent() : Results.Conflict();
        }).RequireAuthorization(Policy).RequireRateLimiting("push-registration-budget");
        app.MapDelete("/api/v1/notification-installations/{id}", (string id, HttpContext context, PushInstallationRegistry registry) =>
        {
            if (!Guid.TryParseExact(id, "N", out var parsed) || parsed.ToString("N") != id) return Results.BadRequest();
            registry.Remove(Owner(context.User), id); return Results.NoContent();
        }).RequireAuthorization(Policy).RequireRateLimiting("push-registration-budget");
    }
}
