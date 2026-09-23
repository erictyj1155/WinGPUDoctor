# Opt-in real CTRL_C_EVENT checks on an isolated console; no forced-second-interrupt test.
param(
    [ValidateSet('Cancel', 'Observe')][string]$Mode = 'Cancel',
    [ValidateRange(1,6)][int]$Runs = 1,
    [string]$EvidenceRoot,
    [switch]$ProbeWorker,
    [string]$EvidenceDir
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'm4-process-evidence.ps1')
$projectRoot = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$cli = Join-Path $projectRoot 'src\WinGPUDoctor.Cli\bin\Release\net10.0-windows\wingpudoctor.dll'
if ($ProbeWorker) {
    Add-Type -Namespace WgdCancelProbe -Name Api -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
public static extern bool FreeConsole();
[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
public static extern bool AttachConsole(uint dwProcessId);
[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
public static extern bool SetConsoleCtrlHandler(System.IntPtr handler, bool add);
[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
public static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
'@
    $report = Join-Path $EvidenceDir 'report.json'
    $errorPath = Join-Path $EvidenceDir 'cli-stderr.txt'
    $outPath = Join-Path $EvidenceDir 'cli-stdout.txt'
    $result = [ordered]@{ Mode = $Mode; StartedUtc = [DateTime]::UtcNow.ToString('o') }
    $failures = [Collections.Generic.List[string]]::new()
    $rootProcess = $null; $cliProcess = $null; $activeWorker = $null; $attached = $false
    $previousProbe = $env:WINGPUDOCTOR_M4_PROCESS_PROBE
    try {
        $env:WINGPUDOCTOR_M4_PROCESS_PROBE = '1'
        $inner = '"{0}" "{1}" --format json --output "{2}" --yes 1> "{3}" 2> "{4}"' -f $sdk, $cli, $report, $outPath, $errorPath
        $rootProcess = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\cmd.exe') -ArgumentList '/c', ('"' + $inner + '"') -PassThru -WindowStyle Hidden -WorkingDirectory $projectRoot
        $result.ConsoleRootPid = $rootProcess.Id
        $preparation = [Diagnostics.Stopwatch]::StartNew()
        $readiness = $null
        while ($preparation.Elapsed.TotalSeconds -lt 30) {
            if ($rootProcess.HasExited) { throw 'CLI exited before the intended readiness marker.' }
            $raw = if (Test-Path -LiteralPath $errorPath) { Get-Content -LiteralPath $errorPath -Raw } else { '' }
            $events = @(Get-M4ProbeEvents -Text "$raw" -AllowPartial)
            if (!$cliProcess -and $events.Count) {
                $cliProcess = [Diagnostics.Process]::GetProcessById([int]$events[0].CliPid)
                $null = $cliProcess.Handle # Retain the exact process handle through exit.
                if ($cliProcess.StartTime.ToUniversalTime().Ticks -ne $events[0].CliStartTimeUtcTicks) { throw 'CLI birth identity mismatch.' }
                $parent = Get-CimInstance Win32_Process -Filter "ProcessId = $($cliProcess.Id)"
                if ($parent.ParentProcessId -ne $rootProcess.Id) { throw 'Probe CLI is not owned by the scoped console root.' }
                $result.CliPid = $cliProcess.Id
                $result.CliStartTimeUtcTicks = $events[0].CliStartTimeUtcTicks
            }
            # Parent has validated Ready, Start and the real signed-driver attempt marker.
            # This is a real in-flight operation, not an arbitrary delay or a console-host PID.
            $readiness = $events | Where-Object { $_.Kind -eq 'AttemptValidated' -and $_.Operation -eq 'wmi.displayDrivers' -and $_.Attempt -eq 1 } | Select-Object -Last 1
            if ($readiness) { break }
            Start-Sleep -Milliseconds 20
        }
        if (!$readiness -or !$cliProcess) { throw 'Validated signed-driver attempt readiness was not observed.' }
        if ($Mode -eq 'Cancel') {
            # The validated marker also proves console startup has completed.
            [void][WgdCancelProbe.Api]::FreeConsole()
            $attached = [WgdCancelProbe.Api]::AttachConsole([uint32]$rootProcess.Id)
            if (!$attached -or ![WgdCancelProbe.Api]::SetConsoleCtrlHandler([IntPtr]::Zero, $true)) { throw 'Scoped console attachment/ignore setup failed.' }
        }
        $result.Readiness = $readiness
        $result.CapturedWorkersBeforeSignal = @($events | Where-Object Kind -eq 'WorkerStarted')
        $activeWorker = [Diagnostics.Process]::GetProcessById([int]$readiness.WorkerPid)
        $null = $activeWorker.Handle
        if ($activeWorker.StartTime.ToUniversalTime().Ticks -ne $readiness.WorkerStartTimeUtcTicks -or $activeWorker.HasExited) {
            throw 'Readiness worker is no longer the captured live process.'
        }
        $result.ReadinessSatisfied = $true
        $result.PreparationMs = $preparation.Elapsed.TotalMilliseconds
        if ($Mode -eq 'Cancel') {
            if ($readiness.MonotonicFrequency -ne [Diagnostics.Stopwatch]::Frequency) { throw 'Monotonic clock frequency mismatch.' }
            $result.SignalCallUtc = [DateTime]::UtcNow.ToString('o')
            $result.SignalCallStartedTimestamp = [Diagnostics.Stopwatch]::GetTimestamp()
            $result.SignalDelivered = [WgdCancelProbe.Api]::GenerateConsoleCtrlEvent(0, 0)
            $result.SignalCallReturnedTimestamp = [Diagnostics.Stopwatch]::GetTimestamp()
            $result.MonotonicFrequency = [Diagnostics.Stopwatch]::Frequency
            if (!$result.SignalDelivered) { throw 'Console signal delivery failed.' }
        }
        $result.CliExited = $cliProcess.WaitForExit(70000)
        $result.CliExitObservedTimestamp = [Diagnostics.Stopwatch]::GetTimestamp()
        if (!$result.CliExited) { throw 'Owned CLI exceeded the validation watchdog.' }
        $result.ExitCode = $cliProcess.ExitCode
        if ($Mode -eq 'Cancel') {
            $result.SignalCallToCliExitObservedMs = 1000.0 * ($result.CliExitObservedTimestamp - $result.SignalCallStartedTimestamp) / $result.MonotonicFrequency
        }
        if (!$rootProcess.WaitForExit(5000)) { throw 'Console root did not exit after CLI.' }
    }
    catch { $failures.Add($_.Exception.Message) }
    finally {
        if ($attached) { [void][WgdCancelProbe.Api]::FreeConsole() }
        $env:WINGPUDOCTOR_M4_PROCESS_PROBE = $previousProbe
        if ($rootProcess -and !$rootProcess.HasExited) { $rootProcess.Kill($true); $null = $rootProcess.WaitForExit(5000) }
        $stderr = if (Test-Path -LiteralPath $errorPath) { Get-Content -LiteralPath $errorPath -Raw } else { '' }
        $stdout = if (Test-Path -LiteralPath $outPath) { Get-Content -LiteralPath $outPath -Raw } else { '' }
        try {
            $events = @(Get-M4ProbeEvents -Text "$stderr")
            $result.Events = $events
            $evidence = Get-M4WorkerEvidence -Events $events
            $result.WorkerChecks = $evidence.Workers
            foreach ($failure in $evidence.Failures) { $failures.Add($failure) }
            if ($Mode -eq 'Cancel') {
                $cancelled = @($events | Where-Object Kind -eq 'CancellationObserved')
                $result.CancellationObserved = $cancelled.Count -eq 1
                $result.NoLaterWorkerLaunchObserved = $result.SignalDelivered -and
                    @($events | Where-Object { $_.Kind -eq 'WorkerStarted' -and $_.MonotonicTimestamp -ge $result.SignalCallStartedTimestamp }).Count -eq 0
                if (!$result.CancellationObserved -or !$result.NoLaterWorkerLaunchObserved) { $failures.Add('cancellation/later-worker trace check failed') }
            }
        } catch { $failures.Add('process evidence could not be verified') }
        $result.ReportCreated = Test-Path -LiteralPath $report
        $result.CancellationNotice = "$stderr" -match 'Collection cancelled\. No report was exported\.'
        $result.PreviewPresent = "$stderr" -match 'WinGPUDoctor diagnostic report'
        $result.StdoutEmpty = [string]::IsNullOrWhiteSpace("$stdout")
        if (!$result.ReadinessSatisfied) { $failures.Add('readiness not satisfied') }
        if ($Mode -eq 'Cancel') {
            if ($result.ExitCode -ne 3 -or !$result.CancellationNotice -or $result.ReportCreated -or $result.PreviewPresent -or !$result.StdoutEmpty) {
                $failures.Add('controlled cancellation output/exit contract failed')
            }
        } elseif ($result.ExitCode -ne 0 -or !$result.ReportCreated -or !$result.PreviewPresent) { $failures.Add('normal control failed') }
        $result.Failures = @($failures)
        $result.FinishedUtc = [DateTime]::UtcNow.ToString('o')
        [pscustomobject]$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $EvidenceDir 'probe-result.json')
        if ($activeWorker) { $activeWorker.Dispose() }
        if ($cliProcess) { $cliProcess.Dispose() }
        if ($rootProcess) { $rootProcess.Dispose() }
    }
    exit $(if ($failures.Count) { 1 } else { 0 })
}
if (!(Test-Path -LiteralPath $cli)) { throw 'Build Release first.' }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try { $administrator = ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
finally { $identity.Dispose() }
if ($administrator) { throw 'Live cancellation validation requires a non-administrator process.' }
$pwsh = (Get-Command pwsh -ErrorAction Stop).Source
$totalFailures = 0
for ($run = 1; $run -le $Runs; $run++) {
    $dir = New-M4EvidenceDirectory -Root $EvidenceRoot -Prefix ('m4-' + $Mode.ToLowerInvariant() + '-')
    $child = Start-Process -FilePath $pwsh -ArgumentList @('-NoProfile','-File',('"' + $PSCommandPath + '"'),
        '-ProbeWorker','-Mode',$Mode,'-EvidenceDir',('"' + $dir + '"')) -PassThru -Wait -NoNewWindow
    try {
        $resultPath = Join-Path $dir 'probe-result.json'
        if ($child.ExitCode -ne 0 -or !(Test-Path -LiteralPath $resultPath)) { $totalFailures++ }
        $probe = if (Test-Path -LiteralPath $resultPath) { Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json } else { $null }
        Write-Host ("M4 {0} run {1}/{2}: exit {3}, failures {4}; evidence {5}." -f $Mode,$run,$Runs,$probe.ExitCode,@($probe.Failures).Count,$dir)
    } finally { $child.Dispose() }
}
if ($totalFailures) { throw "M4 $Mode validation failed in $totalFailures run(s)." }
Write-Host "M4 controlled cancellation harness: PASS ($Runs run(s), mode $Mode)."
