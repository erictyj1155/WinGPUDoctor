# Opt-in integration checks: invokes live read-only collection for preview/refusal/overwrite cases.
# Requires PowerShell 7 and an existing Release build. No real reports are committed.
param()
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$localSdk=Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$sdk=if(Test-Path -LiteralPath $localSdk){$localSdk}else{(Get-Command dotnet -ErrorAction Stop).Source}
$cli=Join-Path $projectRoot 'src\WinGPUDoctor.Cli\bin\Release\net10.0-windows\wingpudoctor.dll'
$checkDir=Join-Path $projectRoot ('artifacts\cli-check-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $checkDir -Force | Out-Null
function Invoke-CheckedCli([string[]]$Arguments, [int[]]$Expected) {
    $start=[Diagnostics.ProcessStartInfo]::new($sdk)
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true
    $start.RedirectStandardError=$true
    $start.RedirectStandardInput=$true
    $start.WorkingDirectory=$checkDir
    $start.ArgumentList.Add($cli)
    foreach($arg in $Arguments){$start.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::new()
    $process.StartInfo=$start
    try {
        $null=$process.Start()
        $process.StandardInput.Close()
        $outTask=$process.StandardOutput.ReadToEndAsync()
        $errTask=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit(60000)){ $process.Kill($true); throw 'CLI integration timeout.' }
        $stdout=$outTask.GetAwaiter().GetResult()
        $stderr=$errTask.GetAwaiter().GetResult()
        if($process.ExitCode -notin $Expected){throw "Unexpected CLI exit code $($process.ExitCode)."}
        [pscustomobject]@{Out=$stdout;Err=$stderr;Code=$process.ExitCode}
    } finally {$process.Dispose()}
}
$checks=[Collections.Generic.List[string]]::new()
$help=Invoke-CheckedCli @('--help') @(0)
if($help.Out -notmatch 'Usage:'){throw 'Help missing.'};$checks.Add('help: PASS')
$null=Invoke-CheckedCli @('--format','xml') @(2);$checks.Add('invalid format rejected: PASS')
$null=Invoke-CheckedCli @('--yes') @(2);$checks.Add('unscoped --yes rejected: PASS')
$null=Invoke-CheckedCli @('--output','\\example.invalid\share\report.json','--yes') @(2);$checks.Add('UNC path rejected before collection: PASS')
$preview=Invoke-CheckedCli @('--format','json') @(0,3)
if(!(Test-Json -Json $preview.Out -SchemaFile (Join-Path $projectRoot 'schemas\report-0.2.0.schema.json'))){throw 'JSON stdout is not a valid report.'}
if(@(Get-ChildItem -LiteralPath $checkDir -Force).Count -ne 0){throw 'Default preview unexpectedly created files.'}
$checks.Add('JSON preview schema and no file creation: PASS')
$declined=Join-Path $checkDir 'declined.json'
$refusal=Invoke-CheckedCli @('--format','json','--output',$declined) @(4)
if(Test-Path -LiteralPath $declined){throw 'Unconfirmed export created a file.'}
if($refusal.Err -notmatch 'WinGPUDoctor diagnostic report'){throw 'Export preview missing.'}
$checks.Add('redirected input requires explicit export acceptance: PASS')
$existing=Join-Path $checkDir 'existing.json'
Set-Content -LiteralPath $existing -Value 'SYNTHETIC DO NOT OVERWRITE' -NoNewline
$hash=(Get-FileHash -LiteralPath $existing).Hash
$null=Invoke-CheckedCli @('--format','json','--output',$existing,'--yes') @(5)
if((Get-FileHash -LiteralPath $existing).Hash -ne $hash){throw 'Existing output was modified.'}
$checks.Add('existing-file hash unchanged after refused overwrite: PASS')
$checks | Set-Content -LiteralPath (Join-Path $checkDir 'results.txt')
$checks
