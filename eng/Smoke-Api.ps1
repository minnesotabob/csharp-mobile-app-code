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
    Write-Output 'Local Kestrel HTTP smoke passed: health, catalog and pagination.'
} catch {
    if (Test-Path "$logBase.err") { Get-Content "$logBase.err" }
    throw
} finally {
    if ($serverProcess -and !$serverProcess.HasExited) { Stop-Process -Id $serverProcess.Id -Force }
    Remove-Item "$logBase.out", "$logBase.err" -ErrorAction SilentlyContinue
}
