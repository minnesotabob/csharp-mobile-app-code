using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace GnazzosWorld.PerformanceLab;

internal static class ApiMeasurements
{
    internal static async Task<object> Run(string dll)
    {
        dll = Path.GetFullPath(dll); if (!File.Exists(dll)) throw new FileNotFoundException("Publish the API first.");
        var reserve = new TcpListener(IPAddress.Loopback, 0); reserve.Start(); var port = ((IPEndPoint)reserve.LocalEndpoint).Port; reserve.Stop();
        var origin = new Uri($"http://127.0.0.1:{port}/");
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetDirectoryName(dll)!, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(dll); start.Environment.Clear();
        foreach (var name in new[] { "PATH", "SystemRoot", "TEMP", "TMP", "HOME", "USERPROFILE", "DOTNET_ROOT", "DOTNET_MULTILEVEL_LOOKUP" })
            if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        start.Environment["ASPNETCORE_URLS"] = origin.AbsoluteUri;
        start.Environment["DOTNET_ENVIRONMENT"] = start.Environment["ASPNETCORE_ENVIRONMENT"] = "Staging";
        start.Environment["Commerce__Enabled"] = start.Environment["Payments__Enabled"] = start.Environment["Fulfillment__Enabled"] = start.Environment["Operations__Enabled"] = "false";
        using var process = new Process { StartInfo = start }; var logCharacters = 0; var started = false;
        void Discard(object? _, DataReceivedEventArgs e) { if (e.Data is not null) Interlocked.Add(ref logCharacters, e.Data.Length); }
        process.OutputDataReceived += Discard; process.ErrorDataReceived += Discard;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45)); var ct = deadline.Token;
        try
        {
            var began = Stopwatch.GetTimestamp(); if (!process.Start()) throw new InvalidOperationException("API process did not start."); started = true;
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(3) };
            while (true)
            {
                ct.ThrowIfCancellationRequested(); if (process.HasExited) throw new InvalidDataException("API exited before health.");
                try { using var live = await http.GetAsync("health", ct); if (live.StatusCode == HttpStatusCode.OK) break; }
                catch (HttpRequestException) { }
                await Task.Delay(100, ct);
            }
            var startup = Stopwatch.GetElapsedTime(began).TotalMilliseconds;
            async Task<(double Ms, int Bytes)> Get()
            {
                var time = Stopwatch.GetTimestamp();
                using var response = await http.GetAsync("api/v1/catalog", HttpCompletionOption.ResponseHeadersRead, ct);
                if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentType?.MediaType != "application/json") throw new InvalidDataException("Unexpected public catalog response.");
                await using var input = await response.Content.ReadAsStreamAsync(ct); var buffer = new byte[4096]; var bytes = 0; int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0) { bytes += read; if (bytes > 1048576) throw new InvalidDataException("Catalog exceeded bound."); }
                return (Stopwatch.GetElapsedTime(time).TotalMilliseconds, bytes);
            }
            for (var i = 0; i < 3; i++) _ = await Get();
            var samples = new double[21]; var sizes = new int[21];
            for (var i = 0; i < samples.Length; i++) { var value = await Get(); samples[i] = value.Ms; sizes[i] = value.Bytes; }
            if (sizes.Distinct().Count() != 1 || sizes[0] == 0 || Volatile.Read(ref logCharacters) > 262144) throw new InvalidDataException("Unstable or excessive API fixture output.");
            var ordered = samples.Order().ToArray();
            return new { ProcessToFirstHealthMs = startup, PollIntervalMs = 100, Samples = samples, MedianMs = ordered[10], P95Ms = ordered[19], ResponseBodyBytes = sizes[0],
                Scope = "Published local API; sequential loopback HTTP and five-record public catalog. Includes client/body read. Not mobile startup, TLS, WAN load or wire/energy cost." };
        }
        finally
        {
            if (started && !process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            process.OutputDataReceived -= Discard; process.ErrorDataReceived -= Discard;
        }
    }
}
