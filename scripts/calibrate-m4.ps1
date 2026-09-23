# Opt-in engineering calibration: synthetic infrastructure plus repeated healthy read-only runs.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$outputRoot = Join-Path $projectRoot ('artifacts\m4-calibration-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$envSettings = @{
    WINGPUDOCTOR_M4_CALIBRATION = '1'
    WINGPUDOCTOR_M4_CALIBRATION_DIR = $outputRoot
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    DOTNET_CLI_USE_MSBUILD_SERVER = '0'
}
$previous = @{}
foreach ($name in $envSettings.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name)
    [Environment]::SetEnvironmentVariable($name, $envSettings[$name])
}
try {
    & $sdk test (Join-Path $projectRoot 'tests\WinGPUDoctor.Tests\WinGPUDoctor.Tests.csproj') `
        --no-build --no-restore -c Release -m:1 -nodeReuse:false `
        --filter 'FullyQualifiedName~CalibrationTests.M4CalibrationWhenRequested'
    if ($LASTEXITCODE -ne 0) { throw 'Calibration test failed.' }
}
finally {
    foreach ($name in $envSettings.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}

$file = Join-Path $outputRoot 'calibration.json'
if (!(Test-Path -LiteralPath $file)) { throw 'Calibration output is missing.' }
$data = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
function Summary([object[]]$Values) {
    if ($Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    $mid = [int]($sorted.Count / 2)
    $median = if ($sorted.Count % 2 -eq 1) { $sorted[$mid] } else { ($sorted[$mid - 1] + $sorted[$mid]) / 2 }
    [pscustomobject]@{ Min = $sorted[0]; Median = $median; Max = $sorted[-1] }
}
$healthy = @($data.Healthy)
$summary = [pscustomobject]@{
    Directory = $outputRoot
    SyntheticSamples = @($data.Synthetic).Count
    HealthySamples = $healthy.Count
    HealthyTotalMs = Summary @($healthy | ForEach-Object TotalMs)
    OperationMs = @(
        foreach ($operation in @('wmi.operatingSystem','wmi.computerSystem','wmi.videoControllers','wmi.displayDrivers','display.activeTopology')) {
            $values = @($healthy.Timing | Where-Object { $_.Stage -eq 'Operation' -and $_.Operation -eq $operation } | ForEach-Object Milliseconds)
            [pscustomobject]@{ Operation = $operation; Summary = Summary $values }
        }
    )
    CleanupMs = Summary @($healthy.Timing | Where-Object { $_.Stage -eq 'CleanupTotal' } | ForEach-Object Milliseconds)
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'summary.json')
$summary | ConvertTo-Json -Depth 8
