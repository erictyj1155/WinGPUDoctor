# Shared checks for the opt-in M3 laptop protocol and deterministic synthetic tests.
# Loading this file performs no collection, export, or system change.
function Get-M3ApplicationFingerprint([string]$Cli) {
    $directory = Split-Path $Cli -Parent
    $depsPath = [IO.Path]::ChangeExtension($Cli, '.deps.json')
    $deps = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json -AsHashtable
    $target = $deps.targets[$deps.runtimeTarget.name]
    $assemblies = @(
        foreach ($library in $deps.libraries.Keys) {
            if ($deps.libraries[$library].type -ne 'project') { continue }
            foreach ($asset in $target[$library].runtime.Keys) {
                if ([IO.Path]::GetExtension($asset) -ne '.dll' -or [IO.Path]::GetFileName($asset) -ne $asset) {
                    throw 'Unexpected application assembly layout in build manifest.'
                }
                $path = Join-Path $directory $asset
                $file = Get-Item -LiteralPath $path -ErrorAction Stop
                [pscustomobject]@{
                    File = $asset
                    Library = $library
                    Length = $file.Length
                    Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256 -ErrorAction Stop).Hash
                }
            }
        }
    )
    # These are the three project libraries verified in the current build and project references.
    foreach ($name in @('wingpudoctor', 'WinGPUDoctor.Core', 'WinGPUDoctor.Windows')) {
        if (@($assemblies | Where-Object { ($_.Library -split '/')[0] -ceq $name }).Count -ne 1) {
            throw 'Required application assembly is missing or ambiguous in build manifest.'
        }
    }
    $assemblies | Sort-Object File
}

function Test-M3ApplicationFingerprint([object[]]$Expected, [object[]]$Actual) {
    if ($Expected.Count -eq 0 -or $Actual.Count -ne $Expected.Count) { return $false }
    $left = @($Expected | Sort-Object File | Select-Object File, Library, Length, Sha256) | ConvertTo-Json -Compress
    $right = @($Actual | Sort-Object File | Select-Object File, Library, Length, Sha256) | ConvertTo-Json -Compress
    return $left -ceq $right
}

function Get-M3PrivacyFailures($Value, [string]$Path = 'report') {
    if ($null -eq $Value) { return }
    if ($Value -is [string]) {
        # Only the dedicated GDI field may contain the exact documented display alias.
        if ($Path -match '^report\.facts\.displays\.value\[\d+\]\.sourceGdiName\.value$' -and
            $Value -cmatch '^\\\\\.\\DISPLAY[1-9][0-9]{0,5}$') { return }
        # Decoded backslashes cover PCI/USB instances, device namespaces and local/UNC paths.
        # Named identifiers/credentials and UUIDs are already prohibited by PRIVACY.md.
        if ($Value -match '(?i)\\|\b(?:serial|s/n|hostname|username|password|passwd|secret|token|bearer|api[ _-]?key)\b|\b(?:luid|instanceId|edid)\s*[:=]|\b[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\b') {
            'forbidden identifier content in decoded string'
        }
    } elseif ($Value -is [Collections.IDictionary]) {
        foreach ($key in $Value.Keys) {
            if ($key -match '(?i)^(?:luid|instanceId|adapterDevicePath|monitorDevicePath|edid|serial|serialNumber|hostname|username|computerName|domain|exceptionMessage|stackTrace)$') {
                'forbidden privacy property present'
            }
            Get-M3PrivacyFailures $Value[$key] ($Path + '.' + $key)
        }
    } elseif ($Value -is [Collections.IEnumerable]) {
        $index = 0
        foreach ($item in $Value) {
            Get-M3PrivacyFailures $item ($Path + '[' + $index + ']')
            $index++
        }
    }
}

function Test-Rate([object]$rate, [uint64]$numerator, [uint64]$denominator) {
    return $null -ne $rate -and [uint64]$rate.numerator -eq $numerator -and [uint64]$rate.denominator -eq $denominator
}

function Get-M3ReportFailures(
    [string]$Json, [string]$SchemaFile,
    [uint64]$ExpectedPathRateNumerator = 74321400, [uint64]$ExpectedPathRateDenominator = 450432,
    [uint64]$ExpectedSignalRateNumerator = 74321400, [uint64]$ExpectedSignalRateDenominator = 450432
) {
    # The schema checks required issue/observation structure before semantic inspection.
    try {
        if (!(Test-Json -Json $Json -SchemaFile $SchemaFile -ErrorAction Stop)) {
            'schema validation failed'; return
        }
        $report = $Json | ConvertFrom-Json -AsHashtable -ErrorAction Stop
    } catch { 'schema or JSON validation failed'; return }
    Get-M3PrivacyFailures $report
    $failures = [Collections.Generic.List[string]]::new()
    if ($report.schemaVersion -ne '0.2.0') { $failures.Add('unexpected schema version') }
    if ($report.facts.gpus.state -ne 'available') { $failures.Add('GPU inventory unavailable') }
    if ($report.facts.displays.state -ne 'available') { $failures.Add('display topology unavailable') }
    if (@($report.warnings) -contains 'collectionIncomplete') { $failures.Add('collectionIncomplete warning present') }
    # Redaction is a warning in the product; it is an unexpected observation for this laptop protocol.
    if ($report.privacy.redactedFields -ne 0 -or @($report.warnings) -contains 'valuesRedacted') {
        $failures.Add('unexpected privacy redaction')
    }
    $gpuEntries = @($report.facts.gpus.value)
    if (@($gpuEntries | Group-Object id | Where-Object Count -ne 1).Count -gt 0) {
        $failures.Add('duplicate GPU inventory ID')
    }
    foreach ($gpu in $gpuEntries) {
        foreach ($field in @($gpu.name, $gpu.pciVendorId, $gpu.pciDeviceId, $gpu.classification, $gpu.driver.provider, $gpu.driver.version, $gpu.driver.date)) {
            if ($field.state -eq 'redacted') { $failures.Add('unexpected GPU redaction') }
        }
    }

    $displayRuns = @($report.collection | Where-Object source -eq 'displayConfig')
    $nameIssues = @()
    if ($displayRuns.Count -ne 1) {
        $failures.Add('expected one display collector run')
    } else {
        $displayRun = $displayRuns[0]
        if ($displayRun.status -ne 'succeeded') { $failures.Add("display status $($displayRun.status)") }
        if ($displayRun.reason -ne 'none') { $failures.Add("display reason $($displayRun.reason)") }
        if ($displayRun.attempts -lt 1 -or $displayRun.attempts -gt 3) { $failures.Add('unexpected display attempts') }
        foreach ($issue in @($displayRun.issues)) {
            $allowed = ($issue.operation -eq 'queryPaths' -and $issue.reason -eq 'topologyChanged' -and $issue.nativeErrorCode -eq 122) -or
                ($issue.operation -eq 'targetName' -and $issue.reason -eq 'missingValue' -and $null -eq $issue.nativeErrorCode)
            if (!$allowed) { $failures.Add("unexpected display issue $($issue.operation)/$($issue.reason)") }
        }
        $nameIssues = @($displayRun.issues | Where-Object {
            $_.operation -eq 'targetName' -and $_.reason -eq 'missingValue' -and $null -eq $_.nativeErrorCode
        })
    }

    $displays = @($report.facts.displays.value)
    if ($displays.Count -ne 1) {
        $failures.Add('expected one active path')
    } else {
        $display = $displays[0]
        if ($display.outputTechnology.value -ne 'internal') { $failures.Add('unexpected output technology') }
        if (!$display.pathActive) { $failures.Add('path is not active') }
        if (!$display.targetAvailable) { $failures.Add('target is not available') }
        if ($display.sourceResolution.value.widthPixels -ne 2560 -or $display.sourceResolution.value.heightPixels -ne 1600) {
            $failures.Add('resolution is not 2560 x 1600')
        }
        if ($display.rotation.value -ne 'identity') { $failures.Add('unexpected rotation') }
        if ($display.scanLineOrdering.value -ne 'progressive') { $failures.Add('unexpected scan ordering') }
        if (!(Test-Rate $display.pathRefreshRate.value $ExpectedPathRateNumerator $ExpectedPathRateDenominator)) {
            $failures.Add('unexpected path refresh rational')
        }
        if (!(Test-Rate $display.signalRefreshRate.value $ExpectedSignalRateNumerator $ExpectedSignalRateDenominator)) {
            $failures.Add('unexpected signal refresh rational')
        }
        if ($display.name.state -eq 'unknown') {
            if ($display.name.reason -ne 'missingValue' -or $null -ne $display.name.value) { $failures.Add('unexpected unknown name shape') }
            if ($nameIssues.Count -ne 1) { $failures.Add('expected one missing-name diagnostic') }
        } elseif ($display.name.state -eq 'available') {
            if ([string]::IsNullOrWhiteSpace($display.name.value)) { $failures.Add('available name is blank') }
            if ($nameIssues.Count -ne 0) { $failures.Add('missing-name diagnostic contradicts available name') }
        } else { $failures.Add('unexpected name state') }

        foreach ($field in @('sourceId', 'targetId', 'sourceAdapterId', 'targetAdapterId')) {
            if ($display.$field -notmatch '^(source|target|adapter)-[1-9][0-9]*$') { $failures.Add("invalid report-local reference $field") }
        }
        $sourceMatch = $display.sourceAdapter
        $targetMatch = $display.targetAdapter
        if ($sourceMatch.state -ne 'available' -or $targetMatch.state -ne 'available') {
            $failures.Add('adapter correlation unavailable')
        } else {
            foreach ($match in @($sourceMatch, $targetMatch)) {
                if ($match.value.evidence -ne 'exactSetupApiInstanceId' -or $match.value.confidence -ne 'exact') {
                    $failures.Add('adapter correlation evidence is not exact')
                }
                $matches = @($gpuEntries | Where-Object { $_.id -ceq $match.value.gpuId })
                if ($matches.Count -ne 1) { $failures.Add('adapter reference must resolve to exactly one GPU') }
                elseif ($matches[0].name.state -ne 'available') { $failures.Add('matched GPU description unavailable') }
            }
            if ($sourceMatch.value.gpuId -cne $targetMatch.value.gpuId) { $failures.Add('source and target adapters differ from baseline') }
        }
    }
    $failures.ToArray()
}
