# Synthetic copies of build inputs only; no diagnostic collection. Keep evidence under artifacts/.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'm3-validation-checks.ps1')
$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $root 'src/WinGPUDoctor.Cli/bin/Release/net10.0-windows'
$fixture = Join-Path $root ('artifacts/m4-fingerprint-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $fixture -Recurse
$cli = Join-Path $fixture 'wingpudoctor.dll'
$baseline = @(Get-M3ApplicationFingerprint $cli)
if (!(Test-M3ApplicationFingerprint $baseline @(Get-M3ApplicationFingerprint $cli))) { throw 'Stable execution inputs changed.' }
$passed = 1
foreach ($relative in @('wingpudoctor.runtimeconfig.json', 'wingpudoctor.deps.json', 'worker/runtimes/win/lib/net10.0/System.Management.dll')) {
    $path = Join-Path $fixture $relative
    $original = [IO.File]::ReadAllBytes($path)
    try {
        [IO.File]::WriteAllBytes($path, [byte[]]($original + [byte]10))
        if (Test-M3ApplicationFingerprint $baseline @(Get-M3ApplicationFingerprint $cli)) { throw "Changed input missed: $relative" }
        $passed++
    } finally { [IO.File]::WriteAllBytes($path, $original) }
}
foreach ($scope in @('host', 'runtime')) {
    $changed = @($baseline | ConvertTo-Json -Depth 10 | ConvertFrom-Json)
    ($changed | Where-Object Scope -eq $scope).Identity += '-changed'
    if (Test-M3ApplicationFingerprint $baseline $changed) { throw "Changed $scope identity metadata missed." }
    $passed++
}
$shared = Join-Path $fixture 'WinGPUDoctor.Core.dll'
try {
    Copy-Item -LiteralPath (Join-Path $fixture 'WinGPUDoctor.Protocol.dll') -Destination $shared
    $rejected = $false
    try { Get-M3ApplicationFingerprint $cli | Out-Null } catch { $rejected = $true }
    if (!$rejected) { throw 'Mixed parent/worker accepted.' }
    $passed++
} finally { Copy-Item -LiteralPath (Join-Path $source 'WinGPUDoctor.Core.dll') -Destination $shared }
$required = [IO.Path]::GetFullPath((Join-Path $fixture 'wingpudoctor.runtimeconfig.json'))
if (!$required.StartsWith([IO.Path]::GetFullPath($fixture) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Fixture path escaped its test directory.'
}
try {
    Remove-Item -LiteralPath $required
    $rejected = $false
    try { Get-M3ApplicationFingerprint $cli | Out-Null } catch { $rejected = $true }
    if (!$rejected) { throw 'Missing runtime configuration accepted.' }
    $passed++
} finally { Copy-Item -LiteralPath (Join-Path $source 'wingpudoctor.runtimeconfig.json') -Destination $required }
"Execution fingerprint checks: $passed passed, 0 failed."
