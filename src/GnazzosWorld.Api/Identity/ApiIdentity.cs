using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace GnazzosWorld.Api.Identity;

public static class ApiIdentity
{
    public const string ReadPolicy = "requests-read";
    public static void AddIdentity(IServiceCollection services, IConfiguration config)
    {
        var authority = config["Identity:Authority"];
        var audience = config["Identity:Audience"];
        if ((string.IsNullOrWhiteSpace(authority)) != (string.IsNullOrWhiteSpace(audience)))
            throw new InvalidOperationException("Set both Identity:Authority and Identity:Audience, or neither.");
        if (!string.IsNullOrWhiteSpace(authority))
        {
            if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer) || issuer.Scheme != "https" ||
                issuer.UserInfo.Length != 0 || issuer.AbsolutePath != "/" || issuer.Query.Length != 0 || issuer.Fragment.Length != 0 ||
                !Uri.TryCreate(audience, UriKind.Absolute, out var resource) || resource.Scheme != "https")
                throw new InvalidOperationException("Use HTTPS issuer and resource configuration.");
            authority = issuer.AbsoluteUri;
        }
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.Authority = authority; options.Audience = audience;
            options.RequireHttpsMetadata = true; options.MapInboundClaims = false; options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = authority, ValidateAudience = true, ValidAudience = audience,
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidateIssuerSigningKey = true, ClockSkew = TimeSpan.FromSeconds(30), ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
            };
        });
        services.AddAuthorizationBuilder().AddPolicy(ReadPolicy, policy => policy.RequireAuthenticatedUser()
            .RequireAssertion(context =>
            {
                var subjects = context.User.FindAll("sub").ToArray();
                return subjects.Length == 1 && !string.IsNullOrWhiteSpace(subjects[0].Value) && subjects[0].Value.Length <= 256 &&
                    !subjects[0].Value.Any(char.IsControl) && context.User.FindAll("scope")
                        .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Contains("requests:read", StringComparer.Ordinal);
            }));
    }
}
public sealed record OwnedRequest(string Issuer, string Subject, string Id, string Summary);
public sealed class OwnedRequestStore(IEnumerable<OwnedRequest>? fixtures = null)
{
    private readonly OwnedRequest[] rows = fixtures?.ToArray() ?? [];
    public OwnedRequest? ForOwner(string id, ClaimsPrincipal user)
    {
        var issuer = user.FindFirst("iss")?.Value; var subject = user.FindFirst("sub")?.Value;
        return rows.SingleOrDefault(r => r.Id == id && r.Issuer == issuer && r.Subject == subject);
    }
}
