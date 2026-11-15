using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.Core.Notifications;

public sealed class HttpPushRegistrationSink(ApiSession session, HttpMessageHandler? handler = null) : IPushRegistrationSink, IDisposable
{
    private readonly HttpClient http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    public Task UpsertAsync(PushBinding binding, CancellationToken ct)
    {
        if (!binding.IsValid()) throw new InvalidDataException("Invalid push binding.");
        return SendAsync(HttpMethod.Put, binding.InstallationId, binding, ct);
    }
    public Task RemoveAsync(string installationId, CancellationToken ct) => SendAsync(HttpMethod.Delete, installationId, null, ct);
    private async Task SendAsync(HttpMethod method, string id, PushBinding? body, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "N", out var parsed) || parsed.ToString("N") != id) throw new ArgumentException("Use a canonical installation ID.");
        var lease = await session.AcquireAsync(ct);
        if (!session.IsCurrent(lease)) throw new SessionEndedException();
        using var request = new HttpRequestMessage(method, new Uri(session.Settings.ApiOrigin, "api/v1/notification-installations/" + id));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", lease.Token);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        ct.ThrowIfCancellationRequested(); if (!session.IsCurrent(lease)) throw new SessionEndedException();
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        { await session.RejectLeaseAsync(lease, ct); throw new InteractiveSignInRequiredException(); }
        if (response.StatusCode != HttpStatusCode.NoContent) throw new HttpRequestException("Push registration was not confirmed.", null, response.StatusCode);
    }
    public void Dispose() => http.Dispose();
}
