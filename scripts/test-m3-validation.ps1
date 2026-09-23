# Deterministic tests only: synthetic reports and existing build fingerprints; no live collection.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'm3-validation-checks.ps1')
$projectRoot = Split-Path $PSScriptRoot -Parent
$schema = Join-Path $projectRoot 'schemas/report-0.2.0.schema.json'
$fixture = Get-Content -LiteralPath (Join-Path $projectRoot 'examples/report.example.json') -Raw | ConvertFrom-Json -AsHashtable
$display = $fixture.facts.displays.value[0]
$display.outputTechnology.value = 'internal'
$display.name.state = 'unknown'; $display.name.value = $null; $display.name.reason = 'missingValue'
$display.pathRefreshRate.value = @{ numerator = 74321400; denominator = 450432 }
$display.signalRefreshRate.value = @{ numerator = 74321400; denominator = 450432 }
($fixture.collection | Where-Object source -eq 'displayConfig').issues = @(
    @{ operation = 'targetName'; reason = 'missingValue'; nativeErrorCode = $null }
)
$fixtureJson = $fixture | ConvertTo-Json -Depth 40
$passed = [Collections.Generic.List[string]]::new()
function Test-ReportCase([string]$Name, [scriptblock]$Mutate, [string]$ExpectedFailure = '', [bool]$SchemaValid = $true) {
    $report = $fixtureJson | ConvertFrom-Json -AsHashtable
    & $Mutate $report
    $json = $report | ConvertTo-Json -Depth 40
    $valid = Test-Json -Json $json -SchemaFile $schema -ErrorAction SilentlyContinue
    if ($valid -ne $SchemaValid) { throw "Fixture schema precondition failed: $Name" }
    $failures = @(Get-M3ReportFailures -Json $json -SchemaFile $schema)
    if ($ExpectedFailure) {
        if ($ExpectedFailure -notin $failures) { throw "Expected rejection absent: $Name ($ExpectedFailure)" }
    } elseif ($failures.Count -ne 0) { throw "Positive fixture rejected: $Name ($($failures -join ', '))" }
    $passed.Add($Name)
}

Test-ReportCase 'unknown name with diagnostic and permitted GDI alias' { param($r) }
Test-ReportCase 'available friendly name' {
    param($r)
    $r.facts.displays.value[0].name = @{ state = 'available'; value = 'Example Panel'; source = 'displayConfig'; reason = 'none' }
    ($r.collection | Where-Object source -eq 'displayConfig').issues = @()
}
Test-ReportCase 'report-local relabeling preserves references' {
    param($r)
    $r.facts.gpus.value[1].id = 'gpu-9'
    $r.facts.displays.value[0].sourceAdapter.value.gpuId = 'gpu-9'
    $r.facts.displays.value[0].targetAdapter.value.gpuId = 'gpu-9'
}
Test-ReportCase 'missing friendly-name diagnostic' {
    param($r) ($r.collection | Where-Object source -eq 'displayConfig').issues = @()
} 'expected one missing-name diagnostic'
Test-ReportCase 'missing issue structure' {
    param($r) ($r.collection | Where-Object source -eq 'displayConfig').Remove('issues')
} 'schema or JSON validation failed' $false
Test-ReportCase 'missing native-error diagnostic field' {
    param($r) ($r.collection | Where-Object source -eq 'displayConfig').issues[0].Remove('nativeErrorCode')
} 'schema or JSON validation failed' $false
Test-ReportCase 'substantive extra diagnostic' {
    param($r) ($r.collection | Where-Object source -eq 'displayConfig').issues += @(
        @{ operation = 'decodeMode'; reason = 'invalidModeIndex'; nativeErrorCode = $null }
    )
} 'unexpected display issue decodeMode/invalidModeIndex'
Test-ReportCase 'name diagnostic with native failure' {
    param($r) ($r.collection | Where-Object source -eq 'displayConfig').issues[0].nativeErrorCode = 31
} 'unexpected display issue targetName/missingValue'
Test-ReportCase 'duplicate GPU IDs' {
    param($r) $r.facts.gpus.value[0].id = $r.facts.gpus.value[1].id
} 'adapter reference must resolve to exactly one GPU'
foreach ($side in @('sourceAdapter', 'targetAdapter')) {
    Test-ReportCase "missing $side reference" {
        param($r) $r.facts.displays.value[0].$side.value.gpuId = 'gpu-99'
    } 'adapter reference must resolve to exactly one GPU'
}
Test-ReportCase 'unavailable adapter relationship' {
    param($r)
    $r.facts.displays.value[0].sourceAdapter = @{ state = 'unknown'; value = $null; source = 'setupApiInstanceJoin'; reason = 'unmatchedAdapter' }
} 'adapter correlation unavailable'
Test-ReportCase 'unexpected GPU redaction even without warning or counter' {
    param($r) $r.facts.gpus.value[1].name = @{ state = 'redacted'; value = $null; source = 'wmiVideoController'; reason = 'sensitiveValue' }
} 'unexpected GPU redaction'
Test-ReportCase 'unexpected driver redaction' {
    param($r) $r.facts.gpus.value[0].driver.provider = @{ state = 'redacted'; value = $null; source = 'wmiSignedDriver'; reason = 'sensitiveValue' }
} 'unexpected GPU redaction'
Test-ReportCase 'unexpected redaction warning' {
    param($r) $r.warnings += @('valuesRedacted')
} 'unexpected privacy redaction'

# Each forbidden value is serialized and decoded by the same function used by the live helper.
# The negative cases remain schema-valid: schema validation is not privacy validation.
$identifiers = [ordered]@{
    'PCI instance' = 'PCI\VEN_1234&DEV_5678\SYNTHETICMARKER'
    'USB instance' = 'USB\VID_1234&PID_5678\SYNTHETICMARKER'
    'device path' = '\\?\DISPLAY#SYNTHETICMARKER'
    'monitor instance' = 'DISPLAY\SYNTHETICMARKER'
    'GDI alias in wrong field' = '\\.\DISPLAY8'
    'serial label' = 'serial=SYNTHETICMARKER'
    'LUID label' = 'luid=99887766'
    'EDID label' = 'edid=SYNTHETICMARKER'
    'UUID' = '12345678-1234-1234-1234-123456789abc'
}
foreach ($label in $identifiers.Keys) {
    $value = $identifiers[$label]
    Test-ReportCase "decoded $label" {
        param($r) $r.facts.system.manufacturer.value = $value
    } 'forbidden identifier content in decoded string'
}
$unicodeReport = $fixtureJson | ConvertFrom-Json -AsHashtable
$unicodeReport.facts.system.manufacturer.value = $identifiers['PCI instance']
$unicodeJson = ($unicodeReport | ConvertTo-Json -Depth 40).Replace('\\', '\u005c')
if (!(Test-Json -Json $unicodeJson -SchemaFile $schema) -or
    'forbidden identifier content in decoded string' -notin @(Get-M3ReportFailures -Json $unicodeJson -SchemaFile $schema)) {
    throw 'Unicode escaped schema-valid PCI identifier was not rejected.'
}
$passed.Add('Unicode escaped PCI identifier')

$cli = Join-Path $projectRoot 'src/WinGPUDoctor.Cli/bin/Release/net10.0-windows/wingpudoctor.dll'
$fingerprint = @(Get-M3ApplicationFingerprint $cli)
$expectedParentFiles = @('wingpudoctor.dll', 'WinGPUDoctor.Core.dll', 'WinGPUDoctor.Windows.dll',
    'WinGPUDoctor.Protocol.dll', 'WinGPUDoctor.Supervisor.dll', 'wingpudoctor.deps.json', 'wingpudoctor.runtimeconfig.json',
    'host/dotnet.exe', 'runtime/System.Private.CoreLib.dll')
$requiredWorkerFiles = @('worker/wingpudoctor-worker.dll', 'worker/WinGPUDoctor.Protocol.dll',
    'worker/WinGPUDoctor.Core.dll', 'worker/WinGPUDoctor.Windows.dll')
$actualFiles = @($fingerprint | Select-Object -ExpandProperty File)
if (@($expectedParentFiles | Where-Object { $_ -notin $actualFiles }).Count -ne 0 -or
    @($requiredWorkerFiles | Where-Object { $_ -notin $actualFiles }).Count -ne 0 -or
    @($fingerprint | Where-Object Scope -notin @('parent', 'worker', 'host', 'runtime')).Count -ne 0 -or
    @($fingerprint | Where-Object Sha256 -notmatch '^[0-9A-F]{64}$').Count -ne 0) {
    throw 'Build manifest did not yield the required parent and worker runtime inputs with SHA256 hashes.'
}
$passed.Add('parent and worker runtime input discovery')
if (!(Test-M3ApplicationFingerprint $fingerprint $fingerprint)) { throw 'Unchanged build rejected.' }
$passed.Add('unchanged application fingerprint')
foreach ($entry in $fingerprint) {
    $changed = @($fingerprint | ConvertTo-Json | ConvertFrom-Json)
    ($changed | Where-Object File -eq $entry.File).Sha256 = '0' * 64
    if (Test-M3ApplicationFingerprint $fingerprint $changed) { throw "Changed runtime input missed: $($entry.File)" }
    $passed.Add("changed $($entry.File) detected")
}
if (Test-M3ApplicationFingerprint $fingerprint @($fingerprint[0])) { throw 'Missing assemblies accepted.' }
$passed.Add('missing application assemblies detected')
$passed | ForEach-Object { "PASS: $_" }
"M3 helper deterministic checks: $($passed.Count) passed, 0 failed."
