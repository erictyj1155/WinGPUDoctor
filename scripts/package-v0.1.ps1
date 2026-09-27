# Build and package the reviewed framework-dependent Windows x64 Release layout: the CLI, wingpudoctor-gui.exe and one
# worker/ in a single WinGPUDoctor-<version>-win-x64 folder (ADR 0008 decision 6), zipped under ignored artifacts/.
# This does not run a live collection or publish anything.
# -Dev (formerly -DevGui) names the same package WinGPUDoctor-<version>-dev-win-x64: a local test package, not a release asset.
# -OutputDirectory writes to another folder inside artifacts/, for example to build again without replacing a package;
# the folder and every folder above it up to artifacts/ must be plain folders, not junctions or links.
param([Alias('DevGui')][switch]$Dev, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$settings = @{
    DOTNET_CLI_TELEMETRY_OPTOUT='1'; DOTNET_GENERATE_ASPNET_CERTIFICATE='false'; DOTNET_CLI_USE_MSBUILD_SERVER='0'
    DOTNET_CLI_HOME=(Join-Path $projectRoot '.tools\cli-home'); NUGET_PACKAGES=(Join-Path $projectRoot '.tools\packages')
    WINGPUDOCTOR_M4_SCHEMA_DIR=(Join-Path $projectRoot 'artifacts\m4-schema')
}
$previous = @{}
$previousPresent = @{}
$pins = @{}
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
$name = Get-PackageName $version -Dev:$Dev
# Output goes only through plain folders inside artifacts/: no junction, symbolic link or mount point on the way.
# Every folder written into, from artifacts/ down to the staging folders, is pinned from before the build until
# the checksum is written, so none can be renamed or replaced meanwhile; every file is created new without
# following a link (package-layout.ps1).
$artifacts = Join-Path $projectRoot 'artifacts'
$artifactRoot = Resolve-PlainPath $artifacts $(if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory, $projectRoot) } else { $artifacts })
[void](Add-PinnedFolders $pins $artifacts $artifactRoot)
$zipPath = Join-Path $artifactRoot "$name.zip"
$checksumPath = "$zipPath.sha256"
if ((Test-PathEntry $zipPath) -or (Test-PathEntry $checksumPath)) {
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

$parentFiles = @(Get-PackageParentFiles)
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
$declared = @(Get-DepsRuntimeFiles (Join-Path $source 'wingpudoctor.deps.json')) + @(Get-DepsRuntimeFiles (Join-Path $guiSource 'wingpudoctor-gui.deps.json'))
$unpackaged = @(Get-UnpackagedRuntimeFiles $declared $parentFiles)
if ($unpackaged.Count) { throw "A declared runtime file is not in the package list: $($unpackaged -join ', ')" }
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

# Staging folders are new and pinned; each file is written new, and the bytes written are what the ZIP must hold.
$stage = Add-PinnedFolders $pins $artifactRoot (Join-Path $artifactRoot ('package-stage-' + [guid]::NewGuid().ToString('N')) $name) -New
$stagedHashes = @{}
function Copy-ReviewedFile([string]$From, [string]$Relative) {
    $destination = Join-Path $stage $Relative
    [void](Add-PinnedFolders $pins $stage (Split-Path $destination -Parent) -New)
    $bytes = [IO.File]::ReadAllBytes($From)
    Write-NewFile $destination $bytes ([IO.File]::GetLastWriteTimeUtc($From))
    $script:stagedHashes[$Relative.Replace('\', '/')] = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
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
$firstParty = 11
if (@($actual | Where-Object { Test-FirstPartyDllName $_ }).Count -ne $firstParty) {
    throw "Expected $firstParty first-party DLL entries (6 CLI + 1 GUI + 4 Worker) for the CodeView check."
}
# No packaged byte may carry the checkout path, the user-profile path, any X:\Users\ path or an unmapped first-party PDB path.
$forbiddenRoots = @($projectRoot, $env:USERPROFILE)
# The packaged executables may keep only their apphost template's own PDB path: the template that MSBuild selects
# for them from this SDK's host pack, never a package-cache copy (package-path-guard.ps1). Throws if there is none.
$appHostProjects = @($cliProject, (Join-Path $projectRoot 'src/WinGPUDoctor.Desktop/WinGPUDoctor.Desktop.csproj'))
$appHostTemplate = [IO.File]::ReadAllBytes((Get-SelectedAppHostTemplate $sdk $appHostProjects))
$stageLeaks = @(Get-DirectoryPathLeakFindings $stage $forbiddenRoots $appHostTemplate)
if ($stageLeaks.Count) {
    $stageLeaks | ForEach-Object { Write-Warning $_ }
    throw 'Package staging contains a local build path; no ZIP was written. Build Release from a Git checkout.'
}

# The ZIP is created new and checked through the same exclusive handle before it is released: entry names, entry
# bytes equal to the staged copies, the path guard, and the SHA-256 for the checksum file.
$zipStream = [WinGPUDoctor.Packaging.PinnedOutput]::CreateNewFile($zipPath)
try {
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $zipStream, [IO.Compression.CompressionLevel]::Optimal, $true)
    $zipStream.Position = 0
    $archive = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Read, $true)
    try {
        $entries = @($archive.Entries | Where-Object { $_.Name })
        $names = @($entries | ForEach-Object { $_.FullName })
        if ($names.Count -ne $expected.Count -or @($names | Where-Object {
            !$_.StartsWith("$name/", [StringComparison]::Ordinal) -or !$expected.Contains($_.Substring($name.Length + 1))
        }).Count) { throw 'ZIP entries differ from the reviewed package file list.' }
        foreach ($entry in $entries) {
            $entryStream = $entry.Open()
            try { $entryHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($entryStream)) } finally { $entryStream.Dispose() }
            if ($entryHash -cne $stagedHashes[$entry.FullName.Substring($name.Length + 1)]) { throw "A ZIP entry differs from the staged file: $($entry.FullName)" }
        }
        $zipLeaks = @(Get-ZipArchivePathLeakFindings $archive $forbiddenRoots $appHostTemplate)
    } finally { $archive.Dispose() }
    if ($zipLeaks.Count) {
        $zipLeaks | ForEach-Object { Write-Warning $_ }
        throw 'The ZIP contains a local build path; no checksum was written.'
    }
    $zipStream.Position = 0
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($zipStream)).ToLowerInvariant()
    $zipBytes = $zipStream.Length
} finally { $zipStream.Dispose() }
Write-NewFile $checksumPath ([Text.UTF8Encoding]::new($false).GetBytes("$hash  $name.zip`n"))
[pscustomobject]@{
    Zip = $zipPath
    Bytes = $zipBytes
    Sha256 = $hash
    Entries = $names.Count
    WorkerFiles = $workerFiles.Count
    Checksum = $checksumPath
    Kind = if ($Dev) { 'local test package (not a release asset)' } else { 'release package' }
}
} finally {
    foreach ($pin in $pins.Values) { $pin.Dispose() }
    foreach ($setting in $settings.Keys) {
        if ($previousPresent[$setting]) { [Environment]::SetEnvironmentVariable($setting, $previous[$setting], 'Process') }
        else { Remove-Item -LiteralPath "Env:$setting" -ErrorAction SilentlyContinue }
    }
}
