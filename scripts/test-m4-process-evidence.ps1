# Deterministic parser checks and current-process identity checks; no hardware collection.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'm4-process-evidence.ps1')
$parsed = @(Get-M4ProbeEvents -Text ('ordinary notice' + [Environment]::NewLine + 'WGD-M4-PROBE {"Kind":"WorkerStarted"}'))
if ($parsed.Count -ne 1 -or $parsed[0].Kind -ne 'WorkerStarted') { throw 'Probe parsing failed.' }
$rejected = $false
try { Get-M4ProbeEvents -Text 'WGD-M4-PROBE {' | Out-Null } catch { $rejected = $true }
if (!$rejected) { throw 'Truncated final probe accepted.' }
if (@(Get-M4ProbeEvents -Text 'WGD-M4-PROBE {' -AllowPartial).Count -ne 0) { throw 'Partial readiness read accepted.' }
$current = [Diagnostics.Process]::GetCurrentProcess()
try {
    $alive = Get-M4ProcessIdentityState -ProcessId $current.Id -StartTimeUtcTicks $current.StartTime.ToUniversalTime().Ticks
    if ($alive.OriginalAlive -ne $true) { throw 'Live original process missed.' }
    $wrongBirth = Get-M4ProcessIdentityState -ProcessId $current.Id -StartTimeUtcTicks 1
    if ($wrongBirth.OriginalAlive -ne $false -or $wrongBirth.State -ne 'PidReused') { throw 'PID-only identity accepted.' }
} finally { $current.Dispose() }
if (@((Get-M4WorkerEvidence -Events @()).Failures).Count -eq 0) { throw 'Missing worker evidence accepted.' }
'M4 process evidence checks: 6 passed, 0 failed.'
