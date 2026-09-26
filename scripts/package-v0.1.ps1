# Build and package the reviewed framework-dependent Windows x64 Release layout.
# This does not run a live collection or publish anything.
# -DevGui builds a local test package instead (M7 Step 5): the CLI, wingpudoctor-gui.exe and worker/ in one
# folder, named WinGPUDoctor-<version>-dev-win-x64 under ignored artifacts/. It is not a release asset.
param([switch]$DevGui)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$settings = @{
    DOTNET_CLI_TELEMETRY_OPTOUT='1'; DOTNET_GENERATE_ASPNET_CERTIFICATE='false'; DOTNET_CLI_USE_MSBUILD_SERVER='0'
    DOTNET_CLI_HOME=(Join-Path $projectRoot '.tools\cli-home'); NUGET_PACKAGES=(Join-Path $projectRoot '.tools\packages')
    WINGPUDOCTOR_M4_SCHEMA_DIR=(Join-Path $projectRoot 'artifacts\m4-schema')
}
$previous = @{}
$previousPresent = @{}
$callerEnvironment = [Environment]::GetEnvironmentVariables('Process')
foreach ($setting in $settings.Keys) {
    $previousPresent[$setting] = $callerEnvironment.Contains($setting)
    if ($previousPresent[$setting]) { $previous[$setting] = $callerEnvironment[$setting] }
}
try {
foreach ($setting in $settings.Keys) { [Environment]::SetEnvironmentVariable($setting, $settings[$setting], 'Process') }
$localSdk = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$cliProject = Join-Path $projectRoot 'src/WinGPUDoctor.Cli/WinGPUDoctor.Cli.csproj'
$versionLines = @(& $sdk msbuild $cliProject -getProperty:Version -nologo)
if ($LASTEXITCODE -ne 0) { throw 'Unable to read the project version.' }
$versions = @($versionLines | Where-Object { $_ -match '^\d+\.\d+\.\d+$' })
if ($versions.Count -ne 1) { throw 'The project version must be one numeric release version.' }
$version = $versions[0]
. (Join-Path $PSScriptRoot 'package-layout.ps1')
$name = Get-PackageName $version -Dev:$DevGui
$artifactRoot = Join-Path $projectRoot 'artifacts'
$zipPath = Join-Path $artifactRoot "$name.zip"
$checksumPath = "$zipPath.sha256"
if ((Test-Path -LiteralPath $zipPath) -or (Test-Path -LiteralPath $checksumPath)) {
    throw 'This package artifact already exists; refusing to overwrite it.'
}

& (Join-Path $PSScriptRoot 'dev.ps1') -Action build
$source = Join-Path $projectRoot 'src/WinGPUDoctor.Cli/bin/Release/net10.0-windows'
$guiSource = Join-Path $projectRoot 'src/WinGPUDoctor.Desktop/bin/Release/net10.0-windows'
$workerSource = Join-Path $projectRoot 'src/WinGPUDoctor.Worker/bin/Release/net10.0-windows'
. (Join-Path $PSScriptRoot 'worker-runtime-closure.ps1')
. (Join-Path $PSScriptRoot 'package-path-guard.ps1')
$workerFiles = @(Get-WorkerRuntimeFiles $workerSource)
if ($workerFiles.Count -eq 0) { throw 'Worker runtime closure is empty.' }

$parentFiles = @(Get-PackageParentFiles -IncludeGui:$DevGui)
$guiFiles = @(Get-PackageGuiFiles)
function Get-ParentSource([string]$Relative) {
    Join-Path $(if ($guiFiles -contains $Relative) { $guiSource } else { $source }) $Relative
}
$documentFiles = @('README.md', 'PRIVACY.md', 'SECURITY.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md')
foreach ($relative in $parentFiles) {
    $file = Get-Item -LiteralPath (Get-ParentSource $relative) -ErrorAction Stop
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Invalid parent asset: $relative" }
}
# Every application-local runtime file that an executable declares must be packaged beside it.
$declared = @(Get-DepsRuntimeFiles (Join-Path $source 'wingpudoctor.deps.json'))
if ($DevGui) { $declared += @(Get-DepsRuntimeFiles (Join-Path $guiSource 'wingpudoctor-gui.deps.json')) }
$undeclared = @($declared | Where-Object { $parentFiles -notcontains $_ })
if ($undeclared.Count) { throw "A declared runtime file is not in the package list: $($undeclared -join ', ')" }
foreach ($relative in $documentFiles) {
    $file = Get-Item -LiteralPath (Join-Path $projectRoot $relative) -ErrorAction Stop
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Invalid package document: $relative" }
}

$exe = [IO.File]::ReadAllBytes((Join-Path $source 'wingpudoctor.exe'))
if ($exe.Length -lt 256 -or [BitConverter]::ToUInt16($exe, 0) -ne 0x5A4D) { throw 'CLI executable is not a PE image.' }
$peOffset = [BitConverter]::ToInt32($exe, 0x3C)
if ($peOffset -lt 0 -or $peOffset -gt $exe.Length - 24 -or
    [BitConverter]::ToUInt32($exe, $peOffset) -ne 0x00004550 -or
    [BitConverter]::ToUInt16($exe, $peOffset + 4) -ne 0x8664) { throw 'CLI executable is not Windows x64.' }
$productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $source 'wingpudoctor.exe')).ProductVersion
if ($productVersion -cne $version) { throw "CLI product version does not match $version." }
$runtimeConfig = Get-Content -LiteralPath (Join-Path $source 'wingpudoctor.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($runtimeConfig.runtimeOptions.framework.name -cne 'Microsoft.NETCore.App' -or
    $runtimeConfig.runtimeOptions.framework.version -notmatch '^10\.') { throw 'Expected .NET 10 shared runtime configuration is missing.' }
if ($DevGui) {
    # The GUI is framework-dependent on the .NET 10 Desktop Runtime, x64, with the same product version.
    $gui = [IO.File]::ReadAllBytes((Join-Path $guiSource 'wingpudoctor-gui.exe'))
    if ($gui.Length -lt 256 -or [BitConverter]::ToUInt16($gui, 0) -ne 0x5A4D) { throw 'GUI executable is not a PE image.' }
    $guiPe = [BitConverter]::ToInt32($gui, 0x3C)
    if ($guiPe -lt 0 -or $guiPe -gt $gui.Length - 24 -or
        [BitConverter]::ToUInt32($gui, $guiPe) -ne 0x00004550 -or
        [BitConverter]::ToUInt16($gui, $guiPe + 4) -ne 0x8664) { throw 'GUI executable is not Windows x64.' }
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $guiSource 'wingpudoctor-gui.exe')).ProductVersion -cne $version) {
        throw "GUI product version does not match $version."
    }
    $guiFrameworks = @((Get-Content -LiteralPath (Join-Path $guiSource 'wingpudoctor-gui.runtimeconfig.json') -Raw | ConvertFrom-Json).runtimeOptions.frameworks)
    foreach ($framework in 'Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App') {
        if (@($guiFrameworks | Where-Object { $_.name -ceq $framework -and $_.version -match '^10\.' }).Count -ne 1) {
            throw "Expected .NET 10 $framework configuration for the GUI is missing."
        }
    }
    foreach ($relative in Get-PackageSharedFiles) {
        if ((Get-FileHash -LiteralPath (Join-Path $source $relative) -Algorithm SHA256).Hash -cne
            (Get-FileHash -LiteralPath (Join-Path $guiSource $relative) -Algorithm SHA256).Hash) {
            throw "The CLI and GUI builds differ for shared file $relative."
        }
    }
}

$stageParent = Join-Path $artifactRoot ('package-stage-' + [guid]::NewGuid().ToString('N'))
$stage = Join-Path $stageParent $name
[void][IO.Directory]::CreateDirectory($stage)
function Copy-ReviewedFile([string]$From, [string]$Relative) {
    $destination = Join-Path $stage $Relative
    [void][IO.Directory]::CreateDirectory((Split-Path $destination -Parent))
    Copy-Item -LiteralPath $From -Destination $destination
    if ((Get-FileHash -LiteralPath $From -Algorithm SHA256).Hash -cne
        (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) { throw "Copied asset changed: $Relative" }
}
foreach ($relative in $parentFiles) { Copy-ReviewedFile (Get-ParentSource $relative) $relative }
foreach ($relative in $workerFiles.RelativePath) {
    Copy-ReviewedFile (Join-Path $workerSource $relative) (Join-Path 'worker' $relative)
}
foreach ($relative in $documentFiles) { Copy-ReviewedFile (Join-Path $projectRoot $relative) $relative }
$copiedWorker = @(Get-WorkerRuntimeFiles (Join-Path $stage 'worker'))
if ($copiedWorker.Count -ne $workerFiles.Count) { throw 'Packaged worker closure differs from the build.' }

$expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($relative in $parentFiles + $documentFiles) { [void]$expected.Add($relative.Replace('\', '/')) }
foreach ($relative in $workerFiles.RelativePath) { [void]$expected.Add(('worker/' + $relative.Replace('\', '/'))) }
$actual = @(Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object {
    [IO.Path]::GetRelativePath($stage, $_.FullName).Replace('\', '/')
})
if ($actual.Count -ne $expected.Count -or @($actual | Where-Object { !$expected.Contains($_) }).Count) {
    throw 'Package staging contains a missing, duplicate, or unexpected file.'
}
$firstParty = if ($DevGui) { 11 } else { 10 }
if (@($actual | Where-Object { Test-FirstPartyDllName $_ }).Count -ne $firstParty) {
    throw "Expected $firstParty first-party DLL entries (6 CLI$(if ($DevGui) { ' + 1 GUI' }) + 4 Worker) for the CodeView check."
}
# No packaged byte may carry the checkout path, the user-profile path, any X:\Users\ path or an unmapped first-party PDB path.
$forbiddenRoots = @($projectRoot, $env:USERPROFILE)
$stageLeaks = @(Get-DirectoryPathLeakFindings $stage $forbiddenRoots)
if ($stageLeaks.Count) {
    $stageLeaks | ForEach-Object { Write-Warning $_ }
    throw 'Package staging contains a local build path; no ZIP was written. Build Release from a Git checkout.'
}

[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $true)
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entries = @($archive.Entries | Where-Object { $_.Name })
    $names = @($entries | ForEach-Object { $_.FullName })
    if ($names.Count -ne $expected.Count -or @($names | Where-Object {
        !$_.StartsWith("$name/", [StringComparison]::Ordinal) -or !$expected.Contains($_.Substring($name.Length + 1))
    }).Count) { throw 'ZIP entries differ from the reviewed package file list.' }
} finally { $archive.Dispose() }
$zipLeaks = @(Get-ZipPathLeakFindings $zipPath $forbiddenRoots)
if ($zipLeaks.Count) {
    $zipLeaks | ForEach-Object { Write-Warning $_ }
    throw 'The ZIP contains a local build path; no checksum was written.'
}

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($checksumPath, "$hash  $name.zip`n", [Text.UTF8Encoding]::new($false))
[pscustomobject]@{
    Zip = $zipPath
    Bytes = (Get-Item -LiteralPath $zipPath).Length
    Sha256 = $hash
    Entries = $names.Count
    WorkerFiles = $workerFiles.Count
    Checksum = $checksumPath
    Kind = if ($DevGui) { 'local test package (not a release asset)' } else { 'release layout' }
}
} finally {
    foreach ($setting in $settings.Keys) {
        if ($previousPresent[$setting]) { [Environment]::SetEnvironmentVariable($setting, $previous[$setting], 'Process') }
        else { Remove-Item -LiteralPath "Env:$setting" -ErrorAction SilentlyContinue }
    }
}
