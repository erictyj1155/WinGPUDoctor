# Opt-in live check. Run after building; writes a sanitized local JSON report under ignored artifacts/.
# Requires PowerShell 7. Does not run from CI or deterministic tests.
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$cli = Join-Path $projectRoot 'src\WinGPUDoctor.Cli\bin\Release\net10.0-windows\wingpudoctor.dll'
if (!(Test-Path -LiteralPath $cli)) { throw 'Build Release first using scripts/dev.ps1 -Action build.' }
$outputDir = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$destination = Join-Path $outputDir ('hardware-smoke-' + [guid]::NewGuid().ToString('N') + '.json')
& $sdk $cli --format json --output $destination --yes
$collectorExit = $LASTEXITCODE
if ($collectorExit -notin @(0,3)) { throw 'Live collector/export failed.' }
if (!(Test-Json -LiteralPath $destination -SchemaFile (Join-Path $projectRoot 'schemas\report-0.2.0.schema.json'))) { throw 'Report schema check failed.' }
$report = Get-Content -LiteralPath $destination -Raw | ConvertFrom-Json
if ($report.facts.gpus.state -eq 'available') {
    # Same-provider comparison verifies plumbing, not independent hardware truth.
    $referenceCount = @(Get-CimInstance -Query 'SELECT Name FROM Win32_VideoController').Count
    if ($report.facts.gpus.value.Count -ne $referenceCount) { throw 'Video-controller count differs from the subsequent CIM query; check for a topology change.' }
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    $admin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
} finally { $identity.Dispose() }
"Hardware check complete. Collector exit: $collectorExit; administrator token: $admin."
'Report is local under artifacts/. Inspect it before sharing. This is not independent hardware validation.'
