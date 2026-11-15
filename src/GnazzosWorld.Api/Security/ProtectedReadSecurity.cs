using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace GnazzosWorld.Api.Security;

public enum ReadDecision { InvalidIdentifier, Granted, Unavailable, RateLimited }

/// <summary>Minimal allowlisted event projection, not a complete tamper-resistant audit system.</summary>
public sealed class ReadSecurityAudit(ILogger<ReadSecurityAudit> logger)
{
    public void Record(ReadDecision decision)
    {
        // No caller-supplied text, request URL, principal, token, summary or record ID.
        var value = decision switch
        {
            ReadDecision.InvalidIdentifier => "invalid_identifier",
            ReadDecision.Granted => "granted",
            ReadDecision.Unavailable => "unavailable",
            ReadDecision.RateLimited => "rate_limited",
            _ => throw new ArgumentOutOfRangeException(nameof(decision))
        };
        logger.LogInformation(new EventId(2601, "OwnedReadDecision"), "Owned request read: {Decision}", value);
    }
}

public static class ProtectedReadSecurity
{
    public const string Policy = "owned-read-budget";
    public const int PermitLimit = 8;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static void AddProtectedReadSecurity(this IServiceCollection services)
    {
        services.AddSingleton<ReadSecurityAudit>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter(Policy, limiter =>
            {
                // One shared per-process budget: bounded cardinality, not per-user fairness.
                limiter.PermitLimit = PermitLimit; limiter.Window = Window;
                limiter.QueueLimit = 0; limiter.AutoReplenishment = true;
            });
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.RequestServices.GetRequiredService<ReadSecurityAudit>().Record(ReadDecision.RateLimited);
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Protected read budget exhausted. Try again later.").ExecuteAsync(context.HttpContext);
            };
        });
    }
}
