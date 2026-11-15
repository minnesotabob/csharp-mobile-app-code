using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.Core.Networking;

public sealed record RequestPreview(string Id, string Summary);
public sealed class RequestAccessException(HttpStatusCode status)
    : Exception("The request preview is unavailable for this session.")
{
    public HttpStatusCode Status { get; } = status;
}
/// <summary>Owned read only. The supplied HTTP handler must disable redirects.</summary>
public sealed class ProtectedRequestClient(HttpClient http, ApiSession session)
{
    public async Task<RequestPreview> GetAsync(string id, CancellationToken ct = default)
    {
        if (!RequestIdPolicy.IsValid(id))
            throw new ArgumentException("Use one bounded request ID.", nameof(id));
        var uri = new Uri(session.Settings.ApiOrigin, "api/v1/requests/" + id);
        var lease = await session.AcquireAsync(ct);
        if (!session.IsCurrent(lease)) throw new SessionEndedException();
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", lease.Token);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!session.IsCurrent(lease)) throw new SessionEndedException();
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await session.RejectLeaseAsync(lease, ct);
            throw new InteractiveSignInRequiredException();
        }
        if (!response.IsSuccessStatusCode) throw new RequestAccessException(response.StatusCode);
        if (response.Content.Headers.ContentType?.MediaType != "application/json") throw new InvalidDataException("Expected a JSON preview.");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var body = new MemoryStream(); var buffer = new byte[4096]; int count;
        while ((count = await input.ReadAsync(buffer, ct)) != 0)
        {
            if (body.Length + count > 16384) throw new InvalidDataException("Preview exceeds its body limit.");
            body.Write(buffer, 0, count);
        }
        var preview = JsonSerializer.Deserialize<RequestPreview>(body.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (preview is null || preview.Id != id || string.IsNullOrWhiteSpace(preview.Summary) ||
            preview.Summary.Length > 1000 || preview.Summary.Any(char.IsControl)) throw new InvalidDataException("Invalid request preview.");
        if (!session.IsCurrent(lease)) throw new SessionEndedException();
        return preview;
    }
}
