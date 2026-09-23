param([Parameter(Mandatory)][string]$WorkerOutput, [Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'worker-runtime-closure.ps1')
$files = @(Get-WorkerRuntimeFiles $WorkerOutput)
foreach ($file in $files) {
    $target = Join-Path $Destination $file.RelativePath
    [void][IO.Directory]::CreateDirectory((Split-Path $target -Parent))
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
}
# Never silently adopt or delete obsolete output. Unexpected assets stop the build.
$null = @(Get-WorkerRuntimeFiles $Destination)
