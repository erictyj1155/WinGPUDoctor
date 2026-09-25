# Regenerate the synthetic examples/report.example.{json,md} from the deterministic test fixture.
# Requires PowerShell 7 and an existing Release build (./scripts/dev.ps1 -Action build).
# Performs no live collection. Run it in its own process (pwsh -NoProfile -File ...) because the
# loaded assemblies stay locked until that PowerShell process exits.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$core = Join-Path $projectRoot 'src/WinGPUDoctor.Core/bin/Release/net10.0/WinGPUDoctor.Core.dll'
$windows = Join-Path $projectRoot 'src/WinGPUDoctor.Windows/bin/Release/net10.0-windows/WinGPUDoctor.Windows.dll'
$tests = Join-Path $projectRoot 'tests/WinGPUDoctor.Tests/bin/Release/net10.0-windows/WinGPUDoctor.Tests.dll'
foreach ($path in @($core, $windows, $tests)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'Release build output is missing; run ./scripts/dev.ps1 -Action build first.' }
}
Add-Type -Path $core
Add-Type -Path $windows
$testAssembly = [Reflection.Assembly]::LoadFrom($tests)
$fixtureType = $testAssembly.GetType('WinGPUDoctor.Tests.TopologyTests', $true)
$binding = [Reflection.BindingFlags]'Static,NonPublic'
$fake = $fixtureType.GetMethod('Single', $binding).Invoke($null, [object[]]@($true))
$topology = $fixtureType.GetMethod('Collect', $binding).Invoke($null, [object[]]@($fake, $null, [WinGPUDoctor.Core.DisplayQueryMode]::VirtualModeAndRefreshAware))
$snapshot = $fixtureType.GetMethod('WithTopology', $binding).Invoke($null, [object[]]@($topology))
$safe = [WinGPUDoctor.Core.PrivacyPolicy]::Prepare($snapshot, [DateOnly]::new(2026, 9, 10))
# Tracked examples use LF line endings and UTF-8 without BOM, matching .editorconfig.
$encoding = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $projectRoot 'examples/report.example.json'), [WinGPUDoctor.Core.ReportWriter]::Json($safe).Replace("`r`n", "`n"), $encoding)
[IO.File]::WriteAllText((Join-Path $projectRoot 'examples/report.example.md'), [WinGPUDoctor.Core.ReportWriter]::Markdown($safe).Replace("`r`n", "`n"), $encoding)
& (Join-Path $PSScriptRoot 'verify-schema.ps1')
