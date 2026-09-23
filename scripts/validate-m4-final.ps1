# Bounded fresh-process healthy collections. No same-host admission-reuse claim.
param([ValidateRange(1,6)][int]$Runs = 3, [string]$EvidenceRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'm3-validation-checks.ps1')
. (Join-Path $PSScriptRoot 'execution-fingerprint.ps1')
. (Join-Path $PSScriptRoot 'm4-process-evidence.ps1')
$projectRoot = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$cli = Join-Path $projectRoot 'src/WinGPUDoctor.Cli/bin/Release/net10.0-windows/wingpudoctor.dll'
$schema = Join-Path $projectRoot 'schemas/report-0.2.0.schema.json'
if (!(Test-Path -LiteralPath $cli)) { throw 'Build Release first using scripts/dev.ps1 -Action build.' }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    $administrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
} finally { $identity.Dispose() }
if ($administrator) { throw 'Final validation requires a non-administrator process.' }
$outputRoot = New-M4EvidenceDirectory -Root $EvidenceRoot -Prefix 'm4-healthy-'
$baseline = @(Get-ExecutionFingerprint -Cli $cli -DotnetHost $sdk)
$results = [Collections.Generic.List[object]]::new()
$failures = [Collections.Generic.List[string]]::new()
for ($run = 1; $run -le $Runs; $run++) {
    if ($run -eq 4) { Start-Sleep -Seconds 5 }
    $destination = Join-Path $outputRoot ('run-{0:D2}.json' -f $run)
    $start = [Diagnostics.ProcessStartInfo]::new($sdk)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.WorkingDirectory = $projectRoot
    $start.Environment['WINGPUDOCTOR_M4_PROCESS_PROBE'] = '1'
    foreach ($argument in @($cli, '--format', 'json', '--output', $destination, '--yes')) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
    # Capture both lists for this exact run; refuse to collect on a changed build.
    $before = @(Get-ExecutionFingerprint -Cli $cli -DotnetHost $sdk)
    if (!(Test-M3ApplicationFingerprint $baseline $before)) { throw 'Execution inputs changed before run; collection skipped.' }
    $beforeUtc = [DateTime]::UtcNow.ToString('o')
    $timer = [Diagnostics.Stopwatch]::StartNew()
    [int]$exitCode = -1
    try {
        $null = $process.Start()
        $cliPid = $process.Id
        $cliBirth = $process.StartTime.ToUniversalTime().Ticks
        $out = $process.StandardOutput.ReadToEndAsync(); $err = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(60000)) { $process.Kill($true); $failures.Add("run $run watchdog") }
        $stdout = $out.GetAwaiter().GetResult(); $preview = $err.GetAwaiter().GetResult()
        $exitCode = $process.ExitCode
    } finally { $timer.Stop(); $process.Dispose() }
    $after = @(Get-ExecutionFingerprint -Cli $cli -DotnetHost $sdk)
    $afterUtc = [DateTime]::UtcNow.ToString('o')
    $stdout | Set-Content -LiteralPath (Join-Path $outputRoot ('run-{0:D2}-stdout.txt' -f $run))
    $preview | Set-Content -LiteralPath (Join-Path $outputRoot ('run-{0:D2}-stderr.txt' -f $run))
    $runFailures = [Collections.Generic.List[string]]::new()
    $events = @(Get-M4ProbeEvents -Text $preview)
    if ($events.Count -eq 0 -or $events[0].CliPid -ne $cliPid -or $events[0].CliStartTimeUtcTicks -ne $cliBirth) {
        $runFailures.Add('CLI process identity not verified')
    }
    $workerEvidence = Get-M4WorkerEvidence -Events $events
    foreach ($failure in $workerEvidence.Failures) { $runFailures.Add($failure) }
    if ($workerEvidence.Workers.Count -ne 5) { $runFailures.Add('expected five application worker records for this laptop') }
    if ($exitCode -ne 0) { $runFailures.Add("exit $exitCode") }
    if (!(Test-Path -LiteralPath $destination)) { $runFailures.Add('report missing') }
    if ([string]::IsNullOrWhiteSpace($preview) -or $preview -notmatch 'WinGPUDoctor diagnostic report') { $runFailures.Add('preview missing') }
    if (Test-Path -LiteralPath $destination) {
        $raw = Get-Content -LiteralPath $destination -Raw
        foreach ($failure in @(Get-M3ReportFailures -Json $raw -SchemaFile $schema)) { $runFailures.Add($failure) }
        $report = $raw | ConvertFrom-Json
        if ($report.facts.system.windowsVersion.state -ne 'available' -or
            $report.facts.system.windowsBuild.state -ne 'available' -or
            $report.facts.system.manufacturer.state -ne 'available' -or
            $report.facts.system.model.state -ne 'available') { $runFailures.Add('system facts unavailable') }
        if ($report.facts.gpus.state -ne 'available' -or @($report.facts.gpus.value).Count -lt 1) { $runFailures.Add('GPU inventory unavailable') }
        if (@($report.collection | Where-Object source -eq 'wmiSignedDriver').Count -ne 1) { $runFailures.Add('driver operation missing') }
        foreach ($gpu in @($report.facts.gpus.value)) {
            if ($gpu.driver.provider.state -ne 'available' -or $gpu.driver.version.state -ne 'available' -or $gpu.driver.date.state -ne 'available') {
                $runFailures.Add('driver association incomplete')
            }
        }
    }
    $matched = (Test-M3ApplicationFingerprint $before $after) -and (Test-M3ApplicationFingerprint $baseline $after)
    if (!$matched) { $runFailures.Add('execution fingerprint changed') }
    foreach ($failure in $runFailures) { $failures.Add("run $run`: $failure") }
    $results.Add([pscustomobject]@{
        Run = $run; DurationMs = $timer.ElapsedMilliseconds; ExitCode = $exitCode
        FingerprintMatched = $matched; Failures = @($runFailures); Report = $destination
        BeforeFingerprintUtc = $beforeUtc; AfterFingerprintUtc = $afterUtc
        BeforeFingerprint = $before; AfterFingerprint = $after
        CliPid = $cliPid; CliStartTimeUtcTicks = $cliBirth
        Events = $events; WorkerChecks = $workerEvidence.Workers
    })
    Write-Host "M4 healthy run $run/$Runs complete: exit $exitCode, failures $($runFailures.Count)."
}
$summary = [pscustomobject]@{
    GeneratedOnUtc = [DateTime]::UtcNow.ToString('o')
    AdministratorToken = $administrator
    FingerprintAlgorithm = 'SHA256'
    FingerprintFormat = 2
    Scope = 'Separate CLI processes; private lifecycle probe enabled; no in-process admission reuse measured.'
    BaselineFingerprint = $baseline
    Runs = $results
    Failures = @($failures)
}
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $outputRoot 'summary.json')
if ($failures.Count -gt 0) {
    Write-Host "M4 final healthy validation: FAIL ($($failures.Count) issue(s)); evidence retained under $outputRoot."
    exit 1
}
Write-Host "M4 final healthy validation: PASS ($Runs/$Runs); evidence retained under $outputRoot."
