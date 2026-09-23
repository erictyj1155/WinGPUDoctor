# Private development probe records, never report data. Loading this helper has no side effects.
function Get-M4ProbeEvents([string]$Text, [switch]$AllowPartial) {
    foreach ($line in ($Text -split '\r?\n')) {
        if (!$line.StartsWith('WGD-M4-PROBE ')) { continue }
        try { $line.Substring(13) | ConvertFrom-Json -ErrorAction Stop }
        catch { if (!$AllowPartial) { throw 'Malformed M4 process probe record.' } }
    }
}

function Get-M4ProcessIdentityState([int]$ProcessId, [long]$StartTimeUtcTicks) {
    $process = $null
    try {
        try { $process = [Diagnostics.Process]::GetProcessById($ProcessId) }
        catch [ArgumentException] { return [pscustomobject]@{ State = 'Exited'; OriginalAlive = $false } }
        # PID alone is insufficient: Windows may reuse it after the owned process exits.
        $birth = $process.StartTime.ToUniversalTime().Ticks
        if ($birth -ne $StartTimeUtcTicks) { return [pscustomobject]@{ State = 'PidReused'; OriginalAlive = $false } }
        $process.Refresh()
        return [pscustomobject]@{ State = $(if ($process.HasExited) { 'Exited' } else { 'Alive' }); OriginalAlive = !$process.HasExited }
    }
    catch { return [pscustomobject]@{ State = 'Unverified'; OriginalAlive = $null } }
    finally { if ($process) { $process.Dispose() } }
}

function Get-M4WorkerEvidence([object[]]$Events) {
    $failures = [Collections.Generic.List[string]]::new()
    $started = @($Events | Where-Object Kind -eq 'WorkerStarted')
    if ($started.Count -eq 0) { $failures.Add('no application worker identities recorded') }
    $sequence = 0
    foreach ($event in $Events) {
        if ($event.Sequence -ne ++$sequence) { $failures.Add('non-contiguous probe sequence') }
        if ($event.Kind -notin @('WorkerStarted','AttemptValidated','CancellationObserved','CleanupConfirmed') -or
            $event.Operation -notin @('wmi.operatingSystem','wmi.computerSystem','wmi.videoControllers','wmi.displayDrivers','display.activeTopology')) {
            $failures.Add('unexpected probe kind or operation')
        }
        if ($event.CliPid -ne $Events[0].CliPid -or $event.CliStartTimeUtcTicks -ne $Events[0].CliStartTimeUtcTicks) {
            $failures.Add('mixed CLI process identity')
        }
    }
    $checks = @(
        foreach ($worker in $started) {
            if ($worker.WorkerPid -le 0 -or $worker.WorkerStartTimeUtcTicks -le 0) { $failures.Add('missing worker birth identity'); continue }
            $state = Get-M4ProcessIdentityState -ProcessId $worker.WorkerPid -StartTimeUtcTicks $worker.WorkerStartTimeUtcTicks
            if ($state.OriginalAlive -ne $false) { $failures.Add('tracked worker alive or liveness unverified') }
            $confirmed = @($Events | Where-Object {
                $_.Kind -eq 'CleanupConfirmed' -and $_.WorkerPid -eq $worker.WorkerPid -and
                $_.WorkerStartTimeUtcTicks -eq $worker.WorkerStartTimeUtcTicks -and $_.Sequence -gt $worker.Sequence
            }).Count -eq 1
            if (!$confirmed) { $failures.Add('worker cleanup confirmation missing or duplicated') }
            [pscustomobject]@{
                Operation = $worker.Operation; WorkerPid = $worker.WorkerPid
                StartTimeUtcTicks = $worker.WorkerStartTimeUtcTicks
                CleanupConfirmed = $confirmed; PostExitState = $state.State; OriginalAlive = $state.OriginalAlive
            }
        }
    )
    [pscustomobject]@{ Workers = $checks; Failures = @($failures) }
}

function New-M4EvidenceDirectory([string]$Root, [string]$Prefix) {
    $project = Split-Path $PSScriptRoot -Parent
    $allowed = [IO.Path]::GetFullPath((Join-Path $project 'artifacts'))
    if (!$Root) { $Root = $allowed }
    $resolved = [IO.Path]::GetFullPath($Root)
    if ($resolved -ine $allowed -and !$resolved.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Private M4 evidence must remain under ignored artifacts/.'
    }
    $directory = Join-Path $resolved ($Prefix + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    return $directory
}
