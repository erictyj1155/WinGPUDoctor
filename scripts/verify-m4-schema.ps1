# Deterministic schema validation for integrated Gate 2 timeout/incomplete snapshots.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$schema = Join-Path $projectRoot 'schemas\report-0.2.0.schema.json'
$directory = Join-Path $projectRoot 'artifacts\m4-schema'
foreach ($name in @('timeout.json', 'incomplete.json', 'wmi-timeout.json', 'topology-timeout.json',
    'omitted-driver.json', 'zero-attempt.json', 'partial-unmatched.json', 'optional-name.json')) {
    $path = Join-Path $directory $name
    if (!(Test-Path -LiteralPath $path)) { throw "Integrated schema fixture is missing: $name" }
    $json = Get-Content -LiteralPath $path -Raw
    if (!(Test-Json -Json $json -SchemaFile $schema)) { throw "Integrated schema fixture failed: $name" }
    $report = $json | ConvertFrom-Json
    if ($name -ne 'optional-name.json' -and $report.collection.status -notcontains 'failed' -and $report.collection.status -notcontains 'partial') {
        throw "Integrated schema fixture did not contain an incomplete operation: $name"
    }
    if ($name -ne 'optional-name.json' -and $report.warnings -notcontains 'collectionIncomplete') {
        throw "Integrated schema fixture lacked collectionIncomplete: $name"
    }
    $os = $report.collection | Where-Object source -eq 'wmiOperatingSystem'
    $topology = $report.collection | Where-Object source -eq 'displayConfig'
    switch ($name) {
        'wmi-timeout.json' { if ($os.reason -ne 'timeout' -or $os.attempts -ne 1) { throw 'Incorrect WMI timeout fixture.' } }
        'topology-timeout.json' {
            if ($topology.reason -ne 'timeout' -or $topology.attempts -ne 1 -or @($topology.issues).Count -ne 0) { throw 'Incorrect topology timeout fixture.' }
        }
        'omitted-driver.json' { if ($report.collection.source -contains 'wmiSignedDriver') { throw 'Omitted driver appeared in report.' } }
        'zero-attempt.json' { if ($os.reason -ne 'timeout' -or $os.attempts -ne 0) { throw 'Incorrect zero-attempt fixture.' } }
        'partial-unmatched.json' { if ($topology.status -ne 'partial' -or $topology.reason -ne 'unmatchedAdapter') { throw 'Incorrect unmatched fixture.' } }
        'optional-name.json' {
            if ($topology.status -ne 'succeeded' -or $report.warnings -contains 'collectionIncomplete' -or
                $report.facts.displays.value[0].name.state -ne 'unknown' -or $report.facts.displays.value[0].name.reason -ne 'missingValue') {
                throw 'Optional name changed completion semantics.'
            }
        }
    }
}
'M4 integrated schema checks: 8 passed, 0 failed.'
