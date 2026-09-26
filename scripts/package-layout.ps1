# Reviewed package file lists and names, shared by packaging and its deterministic checks.
# Dot-source only; it reads build output and changes nothing.

# Files beside the executables. The CLI list is the release layout; the local test package adds the GUI
# (ADR 0008 D6: same package root, same worker/). WinGPUDoctor.Host.dll is a CLI dependency since M7 Step 1.
function Get-PackageCliFiles {
    @('wingpudoctor.exe', 'wingpudoctor.dll', 'wingpudoctor.deps.json', 'wingpudoctor.runtimeconfig.json',
        'WinGPUDoctor.Core.dll', 'WinGPUDoctor.Host.dll', 'WinGPUDoctor.Protocol.dll', 'WinGPUDoctor.Supervisor.dll',
        'WinGPUDoctor.Windows.dll', 'System.CodeDom.dll', 'System.Management.dll', 'runtimes/win/lib/net10.0/System.Management.dll')
}

function Get-PackageGuiFiles {
    @('wingpudoctor-gui.exe', 'wingpudoctor-gui.dll', 'wingpudoctor-gui.deps.json', 'wingpudoctor-gui.runtimeconfig.json')
}

# Files that both builds produce. They are packaged once, from the CLI build, so the GUI build must hold
# the same bytes; worker deployment also checks the loaded Core, Protocol and Windows identities.
function Get-PackageSharedFiles {
    @('WinGPUDoctor.Core.dll', 'WinGPUDoctor.Host.dll', 'WinGPUDoctor.Protocol.dll', 'WinGPUDoctor.Supervisor.dll',
        'WinGPUDoctor.Windows.dll', 'System.Management.dll', 'runtimes/win/lib/net10.0/System.Management.dll')
}

function Get-PackageParentFiles([switch]$IncludeGui) {
    if ($IncludeGui) { @(Get-PackageCliFiles) + @(Get-PackageGuiFiles) } else { Get-PackageCliFiles }
}

# The local test package is marked -dev so that it can never be mistaken for a release asset.
function Get-PackageName([string]$Version, [switch]$Dev) {
    if ($Dev) { "WinGPUDoctor-$Version-dev-win-x64" } else { "WinGPUDoctor-$Version-win-x64" }
}

# Application-local runtime files that an app's deps.json declares (framework files are not listed there).
# The asset groups match the worker closure (worker-runtime-closure.ps1): runtime and native by file name,
# runtimeTargets by relative path, resources by locale.
function Get-DepsRuntimeFiles([string]$DepsPath) {
    ConvertFrom-DepsRuntimeFiles (Get-Content -LiteralPath $DepsPath -Raw)
}

function ConvertFrom-DepsRuntimeFiles([string]$Json) {
    $deps = $Json | ConvertFrom-Json -AsHashtable
    $target = $deps.targets[$deps.runtimeTarget.name]
    if (!$target) { throw 'Runtime target is missing from deps.json.' }
    $files = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($library in $target.Values) {
        foreach ($group in @('runtime', 'native')) {
            if ($library.ContainsKey($group)) { foreach ($asset in $library[$group].Keys) { [void]$files.Add([IO.Path]::GetFileName($asset)) } }
        }
        if ($library.ContainsKey('runtimeTargets')) { foreach ($asset in $library.runtimeTargets.Keys) { [void]$files.Add($asset.Replace('\', '/')) } }
        if ($library.ContainsKey('resources')) {
            foreach ($asset in $library.resources.Keys) { [void]$files.Add($library.resources[$asset].locale + '/' + [IO.Path]::GetFileName($asset)) }
        }
    }
    $files | Sort-Object
}

# Declared runtime files that a package layout would leave out; packaging fails if any remain.
function Get-UnpackagedRuntimeFiles([string[]]$Declared, [string[]]$PackageFiles) {
    @($Declared | Where-Object { $PackageFiles -notcontains $_ } | Sort-Object -Unique)
}
