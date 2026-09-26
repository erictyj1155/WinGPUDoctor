param([ValidateSet('build','test','preview','audit')][string]$Action = 'test')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
# This script runs in the caller's process; restore its environment on exit.
$settings = @{
    DOTNET_CLI_TELEMETRY_OPTOUT='1'; DOTNET_GENERATE_ASPNET_CERTIFICATE='false'; DOTNET_CLI_USE_MSBUILD_SERVER='0'
    DOTNET_CLI_HOME=(Join-Path $projectRoot '.tools\cli-home'); NUGET_PACKAGES=(Join-Path $projectRoot '.tools\packages')
    WINGPUDOCTOR_M4_SCHEMA_DIR=(Join-Path $projectRoot 'artifacts\m4-schema')
}
$previous = @{}
foreach ($name in $settings.Keys) { $previous[$name] = [Environment]::GetEnvironmentVariable($name); [Environment]::SetEnvironmentVariable($name, $settings[$name]) }
Push-Location $projectRoot
try {
    # Locked deterministic restore skips vulnerability auditing; this is not an offline switch.
    # Explicit audit action and CI restore consult advisory sources and can require network access.
    $audit = if ($Action -eq 'audit') { 'true' } else { 'false' }
    & $sdk restore WinGPUDoctor.slnx --locked-mode -m:1 -nodeReuse:false "-p:NuGetAudit=$audit"
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    if ($Action -eq 'audit') { return }
    & $sdk build WinGPUDoctor.slnx --no-restore -c Release -m:1 -nodeReuse:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if ($Action -eq 'test') {
        & $sdk test WinGPUDoctor.slnx --no-build --no-restore -c Release -m:1 -nodeReuse:false --logger 'trx;LogFileName=unit-tests.trx' --results-directory artifacts/test-results
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
        & (Join-Path $PSScriptRoot 'verify-schema.ps1')
        & (Join-Path $PSScriptRoot 'test-m3-validation.ps1')
        & (Join-Path $PSScriptRoot 'verify-m4-schema.ps1')
        & (Join-Path $PSScriptRoot 'test-m4-admission.ps1')
        & (Join-Path $PSScriptRoot 'test-m4-deployment.ps1')
        & (Join-Path $PSScriptRoot 'test-execution-fingerprint.ps1')
        & (Join-Path $PSScriptRoot 'test-m4-process-evidence.ps1')
        & (Join-Path $PSScriptRoot 'test-package-layout.ps1')
    }
    if ($Action -eq 'preview') {
        & $sdk 'src/WinGPUDoctor.Cli/bin/Release/net10.0-windows/wingpudoctor.dll'
        if ($LASTEXITCODE -notin @(0,3)) { throw 'Preview failed.' }
    }
}
finally {
    Pop-Location
    foreach ($name in $settings.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
