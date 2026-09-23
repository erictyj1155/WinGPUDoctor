# Opt-in M3 single-laptop repeatability check. It performs read-only collections,
# writes sanitized reports under ignored artifacts/, and never changes system settings.
param(
    [ValidateRange(1, 12)][int]$Runs = 6,
    [ValidateRange(1, 6)][int]$BatchSize = 3,
    [ValidateRange(0, 60)][int]$PauseSeconds = 30,
    [ValidateRange(1, [uint64]::MaxValue)][uint64]$ExpectedPathRateNumerator = 74321400,
    [ValidateRange(1, [uint64]::MaxValue)][uint64]$ExpectedPathRateDenominator = 450432,
    [ValidateRange(1, [uint64]::MaxValue)][uint64]$ExpectedSignalRateNumerator = 74321400,
    [ValidateRange(1, [uint64]::MaxValue)][uint64]$ExpectedSignalRateDenominator = 450432
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'm3-validation-checks.ps1')
if ($Runs % $BatchSize -ne 0) { throw 'Runs must be evenly divisible by BatchSize.' }

$projectRoot = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$cli = Join-Path $projectRoot 'src\WinGPUDoctor.Cli\bin\Release\net10.0-windows\wingpudoctor.dll'
$schema = Join-Path $projectRoot 'schemas\report-0.2.0.schema.json'
if (!(Test-Path -LiteralPath $cli)) { throw 'Build Release first using scripts/dev.ps1 -Action build.' }

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    $administrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
} finally { $identity.Dispose() }
if ($administrator) { throw 'This validation protocol requires a non-administrator process.' }

$outputRoot = Join-Path $projectRoot ('artifacts\m3-validation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$buildFingerprint = @(Get-M3ApplicationFingerprint -Cli $cli -DotnetHost $sdk)
$protocolStartedOnUtc = [DateTime]::UtcNow.ToString('o')

function Invoke-Collection([int]$runNumber) {
    $destination = Join-Path $outputRoot ('run-{0:D2}.json' -f $runNumber)
    $start = [Diagnostics.ProcessStartInfo]::new($sdk)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardInput = $true
    $start.WorkingDirectory = $projectRoot
    $start.ArgumentList.Add($cli)
    foreach ($argument in @('--format', 'json', '--output', $destination, '--yes')) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try {
        $null = $process.Start()
        $process.StandardInput.Close()
        $outTask = $process.StandardOutput.ReadToEndAsync()
        $errTask = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(60000)) {
            $process.Kill($true)
            return [pscustomobject]@{ Run = $runNumber; Destination = $destination; ExitCode = 124; DurationMs = $timer.ElapsedMilliseconds }
        }
        $null = $outTask.GetAwaiter().GetResult()
        $null = $errTask.GetAwaiter().GetResult()
        return [pscustomobject]@{ Run = $runNumber; Destination = $destination; ExitCode = $process.ExitCode; DurationMs = $timer.ElapsedMilliseconds }
    } finally {
        $timer.Stop()
        $process.Dispose()
    }
}

$results = [Collections.Generic.List[object]]::new()
$allFailures = [Collections.Generic.List[string]]::new()
for ($run = 1; $run -le $Runs; $run++) {
    if ($run -gt 1 -and (($run - 1) % $BatchSize) -eq 0) {
        Write-Host "Pausing $PauseSeconds seconds between batches; use the desktop normally during this interval."
        Start-Sleep -Seconds $PauseSeconds
    }

    $runStartedOnUtc = [DateTime]::UtcNow.ToString('o')
    $failures = [Collections.Generic.List[string]]::new()
    $buildBefore = @()
    $buildAfter = @()
    $matchedBefore = $false
    $matchedAfter = $false
    try {
        $buildBefore = @(Get-M3ApplicationFingerprint -Cli $cli -DotnetHost $sdk)
        $matchedBefore = Test-M3ApplicationFingerprint $buildFingerprint $buildBefore
    } catch { $failures.Add('application fingerprint could not be read before run') }
    if (!$matchedBefore) { $failures.Add('application build missing or changed before run; collection skipped') }
    $invocation = if ($matchedBefore) { Invoke-Collection $run } else {
        [pscustomobject]@{ Destination = (Join-Path $outputRoot ('run-{0:D2}.json' -f $run)); ExitCode = $null; DurationMs = 0 }
    }
    try {
        $buildAfter = @(Get-M3ApplicationFingerprint -Cli $cli -DotnetHost $sdk)
        $matchedAfter = Test-M3ApplicationFingerprint $buildFingerprint $buildAfter
    } catch { $failures.Add('application fingerprint could not be read after run') }
    if (!$matchedAfter) { $failures.Add('application build missing or changed after run') }

    $report = $null
    $displayRuns = @()
    $displays = @()
    if ($invocation.ExitCode -ne 0) { $failures.Add("exit code $($invocation.ExitCode)") }
    if (!(Test-Path -LiteralPath $invocation.Destination)) {
        $failures.Add('report file missing')
    } else {
        $raw = Get-Content -LiteralPath $invocation.Destination -Raw
        foreach ($failure in @(Get-M3ReportFailures -Json $raw -SchemaFile $schema `
            -ExpectedPathRateNumerator $ExpectedPathRateNumerator -ExpectedPathRateDenominator $ExpectedPathRateDenominator `
            -ExpectedSignalRateNumerator $ExpectedSignalRateNumerator -ExpectedSignalRateDenominator $ExpectedSignalRateDenominator)) {
            $failures.Add($failure)
        }
        try {
            $report = $raw | ConvertFrom-Json -AsHashtable -ErrorAction Stop
            $displayRuns = @($report.collection | Where-Object source -eq 'displayConfig')
            $displays = @($report.facts.displays.value)
        } catch { $failures.Add('report parse error') }
    }
    foreach ($failure in $failures) { $allFailures.Add("run $run`: $failure") }
    $results.Add([pscustomobject]@{
        Run = $run
        StartedOnUtc = $runStartedOnUtc
        FinishedOnUtc = [DateTime]::UtcNow.ToString('o')
        BuildMatchedBefore = $matchedBefore
        BuildMatchedAfter = $matchedAfter
        AssembliesBefore = $buildBefore
        AssembliesAfter = $buildAfter
        ExitCode = $invocation.ExitCode
        DurationMs = $invocation.DurationMs
        Attempts = if ($displayRuns) { $displayRuns[0].attempts } else { $null }
        NameState = if ($displays) { $displays[0].name.state } else { $null }
        PathRate = if ($displays) { "$($displays[0].pathRefreshRate.value.numerator)/$($displays[0].pathRefreshRate.value.denominator)" } else { $null }
        SignalRate = if ($displays) { "$($displays[0].signalRefreshRate.value.numerator)/$($displays[0].signalRefreshRate.value.denominator)" } else { $null }
        Failures = @($failures)
        Report = $invocation.Destination
    })
    Write-Host "Run $run/$Runs complete: exit $($invocation.ExitCode), failures $($failures.Count)."
}

$summary = [pscustomobject]@{
    GeneratedOnUtc = [DateTime]::UtcNow.ToString('o')
    StartedOnUtc = $protocolStartedOnUtc
    AdministratorToken = $administrator
    AdministratorGuardPassed = !$administrator
    FingerprintAlgorithm = 'SHA256'
    FingerprintFormat = 2
    ExecutionInputs = $buildFingerprint
    Parameters = [pscustomobject]@{
        Runs = $Runs; BatchSize = $BatchSize; PauseSeconds = $PauseSeconds
        ExpectedPathRateNumerator = $ExpectedPathRateNumerator; ExpectedPathRateDenominator = $ExpectedPathRateDenominator
        ExpectedSignalRateNumerator = $ExpectedSignalRateNumerator; ExpectedSignalRateDenominator = $ExpectedSignalRateDenominator
    }
    Runs = $results
    Failures = @($allFailures)
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'summary.json')
$results | Format-Table Run, ExitCode, Attempts, NameState, PathRate, SignalRate, Failures -AutoSize
if ($allFailures.Count -gt 0) {
    Write-Host "M3 live validation: FAIL ($($allFailures.Count) issue(s)); reports and summary retained under $outputRoot."
    exit 1
}
Write-Host "M3 live validation: PASS ($Runs/$Runs); reports and summary retained under $outputRoot."
