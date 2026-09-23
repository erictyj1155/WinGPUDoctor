# Synthetic deployment fixtures only. Retained under ignored artifacts for inspection.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/worker-runtime-closure.ps1"
$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $root 'src/WinGPUDoctor.Worker/bin/Release/net10.0-windows'
$fixture = Join-Path $root ('artifacts/m4-deployment-fixture-' + [guid]::NewGuid().ToString('N'))
$files = @(Get-WorkerRuntimeFiles $source)
& "$PSScriptRoot/copy-worker-deployment.ps1" -WorkerOutput $source -Destination $fixture
$copy = @(Get-WorkerRuntimeFiles $fixture)
if (($files.RelativePath -join '|') -cne ($copy.RelativePath -join '|')) { throw 'Copy and manifest closure disagree.' }
$count = 1
function Assert-Rejected([scriptblock]$Check) {
    $rejected = $false
    try { & $Check | Out-Null } catch { $rejected = $true }
    if (!$rejected) { throw 'Expected deployment rejection missing.' }
}
$nested = $copy | Where-Object RelativePath -eq 'runtimes/win/lib/net10.0/System.Management.dll'
if (!$nested) { throw 'Nested runtime file missing from closure.' }
# Only remove a single known synthetic fixture file, then restore it explicitly.
Remove-Item -LiteralPath $nested.FullName
Assert-Rejected { Get-WorkerRuntimeFiles $fixture }; $count++
Copy-Item -LiteralPath (Join-Path $source $nested.RelativePath) -Destination $nested.FullName
$extra = Join-Path $fixture 'runtimes/stale.dll'
Set-Content -LiteralPath $extra -Value 'synthetic'
Assert-Rejected { Get-WorkerRuntimeFiles $fixture }; $count++
Assert-Rejected { & "$PSScriptRoot/copy-worker-deployment.ps1" -WorkerOutput $source -Destination $fixture }; $count++
Remove-Item -LiteralPath $extra
$depsPath = Join-Path $fixture 'wingpudoctor-worker.deps.json'
$original = Get-Content -LiteralPath $depsPath -Raw
$deps = $original | ConvertFrom-Json -AsHashtable
$library = @($deps.targets[$deps.runtimeTarget.name].Values | Where-Object { $_.ContainsKey('runtimeTargets') })[0]
$asset = @($library.runtimeTargets.Keys)[0]
$library.runtimeTargets[$asset.Replace('/', '\').ToUpperInvariant()] = $library.runtimeTargets[$asset]
$deps | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $depsPath
Assert-Rejected { Get-WorkerRuntimeFiles $fixture }; $count++
Set-Content -LiteralPath $depsPath -Value $original -NoNewline
foreach ($path in @('../escape', 'a/../escape', 'C:/escape', 'a//b', 'a./b')) {
    Assert-Rejected { ConvertTo-WorkerRelativePath $path }; $count++
}
Write-Output "M4 deployment helper checks: $count passed, 0 failed."
