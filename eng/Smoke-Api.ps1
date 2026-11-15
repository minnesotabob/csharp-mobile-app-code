$ErrorActionPreference = 'Stop'
$apiProject = Join-Path $PSScriptRoot '../src/GnazzosWorld.Api/GnazzosWorld.Api.csproj'
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$baseUrl = "http://127.0.0.1:$port"
$logBase = Join-Path ([System.IO.Path]::GetTempPath()) ("gnazzo-api-" + [guid]::NewGuid())
$serverProcess = $null
try {
    $serverProcess = Start-Process dotnet -PassThru -NoNewWindow -ArgumentList @(
        'run', '--project', ('"' + $apiProject + '"'), '-c', 'Release', '--no-build', '--no-launch-profile',
        '--', '--urls', $baseUrl, '--environment', 'Development'
    ) -RedirectStandardOutput "$logBase.out" -RedirectStandardError "$logBase.err"
    $ready = $false
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        if ($serverProcess.HasExited) { throw 'API process stopped before readiness.' }
        try {
            $health = Invoke-RestMethod "$baseUrl/health" -TimeoutSec 1
            if ($health.status -eq 'ok') { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 200 }
    }
    if (!$ready) { throw 'API readiness deadline exceeded.' }
    $catalog = Invoke-RestMethod "$baseUrl/api/v1/catalog" -TimeoutSec 5
    if ($catalog.schemaVersion -ne 1 -or $catalog.items.Count -ne 5) { throw 'Unexpected catalog response.' }
    $page = Invoke-RestMethod "$baseUrl/api/v1/catalog/pages?limit=2" -TimeoutSec 5
    if ($page.nextOffset -ne 2 -or $page.revision -ne $catalog.revision) { throw 'Unexpected page response.' }
    $mediaClient = [System.Net.Http.HttpClient]::new()
    try {
        $fullMedia = $mediaClient.GetAsync("$baseUrl/api/v1/media/timing").GetAwaiter().GetResult()
        $mediaBytes = $fullMedia.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        if ($mediaBytes.Length -ne 160044) { throw 'Public media fixture length mismatch.' }
        $mediaHash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($mediaBytes)).ToLowerInvariant()
        if ($mediaHash -ne 'd72025ca39105344dd24b0353eeb3f019c8c6fabfed0390a85988df48a7e284f') { throw 'Public media fixture hash mismatch.' }
        $rangeRequest = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, "$baseUrl/api/v1/media/timing")
        $rangeRequest.Headers.Range = [System.Net.Http.Headers.RangeHeaderValue]::new(100, 199)
        $rangeRequest.Headers.IfRange = [System.Net.Http.Headers.RangeConditionHeaderValue]::new($fullMedia.Headers.ETag)
        $partialMedia = $mediaClient.SendAsync($rangeRequest).GetAwaiter().GetResult()
        $rangeBytes = $partialMedia.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        if ([int]$partialMedia.StatusCode -ne 206 -or $rangeBytes.Length -ne 100 -or $partialMedia.Content.Headers.ContentRange.ToString() -ne 'bytes 100-199/160044') { throw 'Public media range contract mismatch.' }
        for ($i = 0; $i -lt 100; $i++) { if ($rangeBytes[$i] -ne $mediaBytes[$i + 100]) { throw 'Public media slice mismatch.' } }
        $partialMedia.Dispose(); $rangeRequest.Dispose(); $fullMedia.Dispose()
    } finally { $mediaClient.Dispose() }
    Write-Output 'Local Kestrel HTTP smoke passed: health, catalog, pagination and public media range/hash.'
} catch {
    if (Test-Path "$logBase.err") { Get-Content "$logBase.err" }
    throw
} finally {
    if ($serverProcess -and !$serverProcess.HasExited) { Stop-Process -Id $serverProcess.Id -Force }
    Remove-Item "$logBase.out", "$logBase.err" -ErrorAction SilentlyContinue
}
