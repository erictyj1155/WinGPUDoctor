# Read-only execution inputs for the current CLI deployment. No collection or report export.
function Get-ExecutionFingerprint([string]$Cli, [string]$DotnetHost) {
    $Cli = [IO.Path]::GetFullPath($Cli)
    $directory = Split-Path $Cli -Parent
    if (!$DotnetHost) {
        $localHost = Join-Path (Split-Path $PSScriptRoot -Parent) '.tools/dotnet/dotnet.exe'
        $DotnetHost = if (Test-Path -LiteralPath $localHost) { $localHost } else { (Get-Command dotnet -ErrorAction Stop).Source }
    }
    $DotnetHost = (Get-Item -LiteralPath $DotnetHost -ErrorAction Stop).FullName
    . (Join-Path $PSScriptRoot 'worker-runtime-closure.ps1')
    $entries = [Collections.Generic.List[object]]::new()
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    function Add-ExecutionInput([string]$scope, [string]$relative, [string]$path, [string]$library, [string]$identity = '') {
        if (!$seen.Add("$scope/$relative")) { throw 'Duplicate execution fingerprint input.' }
        $file = Get-Item -LiteralPath $path -ErrorAction Stop
        if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Invalid execution input.' }
        $entries.Add([pscustomobject]@{
            Scope = $scope; File = $relative; Library = $library; Length = $file.Length
            Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; Identity = $identity
        })
    }
    $depsPath = [IO.Path]::ChangeExtension($Cli, '.deps.json')
    $configPath = [IO.Path]::ChangeExtension($Cli, '.runtimeconfig.json')
    Add-ExecutionInput 'parent' ([IO.Path]::GetFileName($depsPath)) $depsPath 'dependency-manifest'
    Add-ExecutionInput 'parent' ([IO.Path]::GetFileName($configPath)) $configPath 'runtime-configuration'
    $deps = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json -AsHashtable
    $target = $deps.targets[$deps.runtimeTarget.name]
    if (!$target) { throw 'Parent runtime target is missing.' }
    foreach ($library in $target.Keys) {
        $assets = $target[$library]
        foreach ($group in @('runtime', 'native')) {
            if ($assets.ContainsKey($group)) {
                foreach ($asset in $assets[$group].Keys) {
                    $relative = [IO.Path]::GetFileName((ConvertTo-WorkerRelativePath $asset))
                    Add-ExecutionInput 'parent' $relative (Join-Path $directory $relative) $library
                }
            }
        }
        if ($assets.ContainsKey('runtimeTargets')) {
            foreach ($asset in $assets.runtimeTargets.Keys) {
                $relative = ConvertTo-WorkerRelativePath $asset
                Add-ExecutionInput 'parent' $relative (Join-Path $directory $relative) $library
            }
        }
        if ($assets.ContainsKey('resources')) {
            foreach ($asset in $assets.resources.Keys) {
                $relative = ConvertTo-WorkerRelativePath ($assets.resources[$asset].locale + '/' + [IO.Path]::GetFileName($asset))
                Add-ExecutionInput 'parent' $relative (Join-Path $directory $relative) $library
            }
        }
    }
    foreach ($name in @('wingpudoctor.dll','WinGPUDoctor.Supervisor.dll','WinGPUDoctor.Protocol.dll','WinGPUDoctor.Core.dll','WinGPUDoctor.Windows.dll')) {
        if (!$seen.Contains("parent/$name")) { throw 'Required parent execution input is missing.' }
    }
    $workerDirectory = Join-Path $directory 'worker'
    foreach ($file in (Get-WorkerRuntimeFiles $workerDirectory)) {
        Add-ExecutionInput 'worker' ('worker/' + $file.RelativePath) $file.FullName 'worker-runtime'
    }
    foreach ($name in @('WinGPUDoctor.Core.dll','WinGPUDoctor.Protocol.dll','WinGPUDoctor.Windows.dll')) {
        $parent = $entries | Where-Object { $_.Scope -eq 'parent' -and $_.File -eq $name }
        $worker = $entries | Where-Object File -eq "worker/$name"
        if ($parent.Sha256 -cne $worker.Sha256) { throw 'Mixed parent/worker shared assembly.' }
    }

    # Use the actual selected host and CLI runtime/dependency metadata. The worker's internal
    # metadata-only mode does not construct a collector, pipe, supervisor, or native provider.
    $start = [Diagnostics.ProcessStartInfo]::new($DotnetHost)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in @('exec', '--runtimeconfig', $configPath, '--depsfile', $depsPath,
        (Join-Path $workerDirectory 'wingpudoctor-worker.dll'), '--execution-identity')) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(10000)) { $process.Kill($true); throw 'Runtime identity probe exceeded its watchdog.' }
        if ($process.ExitCode -ne 0 -or !$stdout.Wait(1000) -or !$stderr.Wait(1000)) { throw 'Runtime identity probe failed.' }
        $runtime = $stdout.Result | ConvertFrom-Json
    } finally { $process.Dispose() }
    if (![string]::Equals([IO.Path]::GetFullPath($runtime.HostPath), $DotnetHost, [StringComparison]::OrdinalIgnoreCase) -or
        !$runtime.RuntimeVersion -or !(Test-Path -LiteralPath $runtime.CoreLibrary -PathType Leaf)) { throw 'Runtime identity was not verified.' }
    $hostIdentity = [ordered]@{ Path = $DotnetHost; Version = (Get-Item -LiteralPath $DotnetHost).VersionInfo.FileVersion } | ConvertTo-Json -Compress
    $runtimeIdentity = [ordered]@{ Version = $runtime.RuntimeVersion; Directory = $runtime.RuntimeDirectory; CoreLibrary = $runtime.CoreLibrary } | ConvertTo-Json -Compress
    Add-ExecutionInput 'host' 'host/dotnet.exe' $DotnetHost 'dotnet-host' $hostIdentity
    Add-ExecutionInput 'runtime' 'runtime/System.Private.CoreLib.dll' $runtime.CoreLibrary 'Microsoft.NETCore.App' $runtimeIdentity
    $entries | Sort-Object Scope, File
}
