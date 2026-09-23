# Synthetic process-wide admission check. Does not collect hardware or change privileges.
param([switch]$Probe)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (!$Probe) {
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    foreach ($argument in @('-NoProfile', '-File', $PSCommandPath, '-Probe')) { $start.ArgumentList.Add($argument) }
    $child = [Diagnostics.Process]::Start($start)
    try {
        if (!$child.WaitForExit(15000)) { $child.Kill($true); throw 'Admission probe exceeded its test watchdog.' }
        if ($child.ExitCode -ne 0) { throw 'Admission probe failed.' }
    } finally { $child.Dispose() }
    Write-Output 'M4 host-process admission probe: 1 passed, 0 failed.'
    exit 0
}
$bin = Join-Path $projectRoot 'tests/WinGPUDoctor.Tests/bin/Release/net10.0-windows'
foreach ($name in @('WinGPUDoctor.Core', 'WinGPUDoctor.Protocol', 'WinGPUDoctor.Supervisor')) {
    $assembly = [Reflection.Assembly]::LoadFrom((Join-Path $bin "$name.dll"))
}
$flags = [Reflection.BindingFlags]'Instance,Static,NonPublic,Public'
$admissionType = $assembly.GetType('WinGPUDoctor.Supervisor.HostAdmission', $true)
$admission = $admissionType.GetProperty('Process', $flags).GetValue($null)
$admissionType.GetMethod('Poison', $flags).Invoke($admission, @([object]::new())) | Out-Null
$policyType = $assembly.GetType('WinGPUDoctor.Supervisor.CollectionTimingPolicy', $true)
$policy = $policyType.GetProperty('CalibratedProduction', $flags).GetValue($null)
$supervisorType = $assembly.GetType('WinGPUDoctor.Supervisor.CollectionSupervisor', $true)
$constructor = $supervisorType.GetConstructors($flags)[0]
$operationType = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'WinGPUDoctor.Protocol.dll')).GetType('WinGPUDoctor.Protocol.WorkerOperation', $true)
$operation = [Enum]::ToObject($operationType, 0)
for ($i = 0; $i -lt 2; $i++) {
    $supervisor = $constructor.Invoke(@($policy, $null, $null, $null, $null, $null))
    $supervisorType.GetMethod('Begin', $flags).Invoke($supervisor, @([int]1)) | Out-Null
    $pending = $supervisorType.GetMethod('RunOperationAsync', $flags).Invoke($supervisor,
        @($operation, $null, [Threading.CancellationToken]::None, $null))
    $outcome = $pending.AsTask().GetAwaiter().GetResult()
    if ($outcome.Kind.ToString() -ne 'HostFailure' -or $outcome.Code -ne 'HostPoisoned') { throw 'New supervisor bypassed host poison.' }
}
