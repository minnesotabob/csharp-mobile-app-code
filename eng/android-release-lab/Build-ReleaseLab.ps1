param(
    [string]$Output = 'artifacts/android-release-lab',
    [string]$AndroidSdk = $env:ANDROID_HOME,
    [string]$JavaSdk = $env:JAVA_HOME
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$destination = [IO.Path]::GetFullPath((Join-Path (Get-Location) $Output))
if (Test-Path $destination) { throw 'Choose a new lab output directory.' }
$owned = Join-Path ([IO.Path]::GetTempPath()) ('gnazzo-signing-lab-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $owned | Out-Null
New-Item -ItemType Directory $destination | Out-Null
$keytool = Join-Path $JavaSdk 'bin/keytool.exe'
$project = Join-Path $repo 'src/GnazzosWorld.App/GnazzosWorld.App.csproj'
$secretPath = Join-Path $owned 'signing-password.txt'
$store = Join-Path $owned 'disposable.p12'
$cert = Join-Path $owned 'public.der'
function Check-Native([string]$operation) { if ($LASTEXITCODE -ne 0) { throw "$operation failed." } }
try {
    $env:GNAZZO_LAB_KEY_PASSWORD = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    if ($env:GITHUB_ACTIONS -eq 'true') { Write-Output ('::add-mask::' + $env:GNAZZO_LAB_KEY_PASSWORD) }
    [IO.File]::WriteAllText($secretPath, $env:GNAZZO_LAB_KEY_PASSWORD, [Text.UTF8Encoding]::new($false))
    & $keytool -genkeypair -alias disposable -keystore $store -storetype PKCS12 -storepass:env GNAZZO_LAB_KEY_PASSWORD -keypass:env GNAZZO_LAB_KEY_PASSWORD -keyalg RSA -keysize 2048 -validity 2 -dname 'CN=Disposable Gnazzo Lab,OU=Tutorial,O=Gnazzo Lab,C=US' -noprompt
    Check-Native 'Disposable key creation'
    & $keytool -exportcert -alias disposable -keystore $store -storepass:env GNAZZO_LAB_KEY_PASSWORD -file $cert
    Check-Native 'Public certificate export'
    $fingerprint = (Get-FileHash $cert -Algorithm SHA256).Hash.ToLowerInvariant()
    [xml]$manifest = Get-Content (Join-Path $repo 'src/GnazzosWorld.App/Platforms/Android/AndroidManifest.xml')
    $manifest.manifest.application.SetAttribute('testOnly', 'http://schemas.android.com/apk/res/android', 'true') | Out-Null
    $manifestPath = Join-Path $owned 'AndroidManifest.xml'; $manifest.Save($manifestPath)
    $variants = @(
        @{ Name = 'v1'; Version = 1; Rid = 'android-x64'; Format = 'apk' },
        @{ Name = 'v2'; Version = 2; Rid = 'android-x64'; Format = 'apk' },
        @{ Name = 'bundle'; Version = 2; Rid = 'android-arm64'; Format = 'aab' }
    )
    foreach ($variant in $variants) {
        $published = Join-Path $owned $variant.Name
        & dotnet publish $project -f net11.0-android -c Release -r $variant.Rid -o $published `
            '-p:GnazzoAndroidReleaseLab=true' '-p:ApplicationId=com.johngnazzo.gnazzosworld.releaselab' `
            "-p:ApplicationVersion=$($variant.Version)" "-p:ApplicationDisplayVersion=0.1.0-lab.$($variant.Version)" `
            "-p:AndroidPackageFormats=$($variant.Format)" '-p:AndroidKeyStore=true' `
            "-p:AndroidManifest=$manifestPath" "-p:AndroidSigningKeyStore=$store" '-p:AndroidSigningKeyAlias=disposable' `
            "-p:AndroidSigningKeyPass=file:$secretPath" "-p:AndroidSigningStorePass=file:$secretPath" `
            "-p:AndroidSdkDirectory=$AndroidSdk" "-p:JavaSdkDirectory=$JavaSdk"
        Check-Native "Release $($variant.Name) publish"
        $signed = @(Get-ChildItem $published -Filter "*-Signed.$($variant.Format)")
        if ($signed.Count -ne 1) { throw 'Expected exactly one signed package in the explicit publish directory.' }
        $name = if ($variant.Format -eq 'aab') { 'v2.aab' } else { $variant.Name + '.apk' }
        Copy-Item $signed[0].FullName (Join-Path $destination $name)
    }
    [IO.File]::WriteAllText((Join-Path $destination 'expected-signer.txt'), $fingerprint, [Text.UTF8Encoding]::new($false))
    Write-Output 'Disposable Release packages created; private signer remains local and will be removed.'
}
finally {
    Remove-Item Env:GNAZZO_LAB_KEY_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item $owned -Recurse -Force -ErrorAction SilentlyContinue
}
