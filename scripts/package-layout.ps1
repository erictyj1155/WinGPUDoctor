# Reviewed package file lists, names and output locations, shared by packaging and its deterministic checks.
# Dot-source only; loading it changes nothing (only the output helpers below write, when packaging calls them).

# Files beside the executables: the CLI and, since 0.2.0, the GUI in the same package root with one worker/
# (ADR 0008 decision 6). WinGPUDoctor.Host.dll is a CLI dependency since M7 Step 1.
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

function Get-PackageParentFiles { @(Get-PackageCliFiles) + @(Get-PackageGuiFiles) }

# Documents beside the executables. The README's screenshots are packaged at the paths its links use, so the
# extracted guide shows them without fetching anything.
function Get-PackageDocumentFiles {
    @('README.md', 'PRIVACY.md', 'SECURITY.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md',
        'docs/images/gui-welcome.png', 'docs/images/gui-results.png', 'docs/images/gui-save.png')
}

# A local test package has the release contents but is marked -dev, so it can never be mistaken for a release asset.
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

# True if anything is at the path, including a junction or symbolic link whose target is missing.
function Test-PathEntry([string]$Path) {
    try { [void][IO.File]::GetAttributes($Path); $true }
    catch [IO.FileNotFoundException], [IO.DirectoryNotFoundException] { $false }
}

# Returns the full path of $Path, which must be $Base or inside it. $Base and every existing entry below it on the
# way to $Path must be plain: a junction, symbolic link or mount point could send reads or writes outside $Base,
# whatever the path text says. Missing trailing entries are allowed; the caller creates them as plain folders.
function Resolve-PlainPath([string]$Base, [string]$Path) {
    $baseFull = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Base))
    $full = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path, $baseFull))
    $separator = [IO.Path]::DirectorySeparatorChar
    if ($full -ne $baseFull -and !$full.StartsWith($baseFull + $separator, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The path must be inside $([IO.Path]::GetFileName($baseFull))."
    }
    $current = $baseFull
    $parts = @(if ($full.Length -gt $baseFull.Length) { $full.Substring($baseFull.Length + 1).Split($separator) })
    foreach ($part in @('') + $parts) {
        if ($part) { $current = Join-Path $current $part }
        if (!(Test-PathEntry $current)) { break }
        if ([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) {
            throw 'The path passes through a junction, symbolic link or mount point.'
        }
    }
    $full
}

# Writing without following links, even if another process changes artifacts/ meanwhile. A pinned folder is
# opened as itself (a junction or link there is refused, not followed) and without delete sharing, so while the
# handle is open nobody can rename, delete or replace it; packaging pins every folder it writes into, from
# artifacts/ down. A new file is created only if nothing, not even a dangling link, has its name, and a link at
# that name is never followed (FILE_FLAG_OPEN_REPARSE_POINT with CREATE_NEW), so no existing file is truncated.
if (!('WinGPUDoctor.Packaging.PinnedOutput' -as [type])) {
    Add-Type -TypeDefinition @'
using System; using System.ComponentModel; using System.IO; using System.Runtime.InteropServices; using Microsoft.Win32.SafeHandles;
namespace WinGPUDoctor.Packaging {
public static class PinnedOutput {
    const uint ListDirectory = 0x1, ReadAttributes = 0x80, GenericRead = 0x80000000, GenericWrite = 0x40000000;
    const uint ShareRead = 0x1, ShareWrite = 0x2, CreateNewDisposition = 1, OpenExisting = 3;
    const uint BackupSemantics = 0x02000000, OpenReparsePoint = 0x00200000, DirectoryAttribute = 0x10, ReparseAttribute = 0x400;
    const int AlreadyExists = 183;
    [StructLayout(LayoutKind.Sequential)]
    struct FileInformation {
        public uint Attributes, CreatedLow, CreatedHigh, AccessedLow, AccessedHigh, WrittenLow, WrittenHigh;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateDirectoryW(string name, IntPtr security);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

    static IOException Failure(string message, int error) => new IOException(message + " " + new Win32Exception(error).Message);

    // Pins an existing plain folder; throws if the path is a junction, link, mount point or file.
    public static SafeFileHandle Pin(string path) {
        var handle = CreateFileW(path, ListDirectory | ReadAttributes, ShareRead | ShareWrite, IntPtr.Zero, OpenExisting,
            BackupSemantics | OpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid) throw Failure("An output folder could not be pinned.", Marshal.GetLastWin32Error());
        if (!GetFileInformationByHandle(handle, out var information) || (information.Attributes & DirectoryAttribute) == 0 ||
            (information.Attributes & ReparseAttribute) != 0) {
            handle.Dispose();
            throw new IOException("The output path passes through a junction, symbolic link or mount point, or is not a folder.");
        }
        return handle;
    }

    // Creates a folder inside a pinned parent, or keeps an existing plain one unless it must be new, and pins it.
    public static SafeFileHandle CreateAndPin(string path, bool mustBeNew) {
        if (!CreateDirectoryW(path, IntPtr.Zero)) {
            var error = Marshal.GetLastWin32Error();
            if (error != AlreadyExists || mustBeNew) throw Failure("An output folder could not be created as a new folder.", error);
        }
        return Pin(path);
    }

    // Creates a new file for exclusive writing; fails if anything already has that name.
    public static FileStream CreateNewFile(string path) {
        var handle = CreateFileW(path, GenericRead | GenericWrite, 0, IntPtr.Zero, CreateNewDisposition, OpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid) throw Failure("A package file could not be created as a new file; refusing to overwrite.", Marshal.GetLastWin32Error());
        return new FileStream(handle, FileAccess.ReadWrite);
    }
}
}
'@
}

# Pins $Path and every folder between the pinned $Base and it, creating missing ones (all of them if $New).
# $Pins maps each pinned full path to its handle; the caller disposes them when it has finished writing.
function Add-PinnedFolders([hashtable]$Pins, [string]$Base, [string]$Path, [switch]$New) {
    $baseFull = Resolve-PlainPath $Base $Base
    $full = Resolve-PlainPath $Base $Path
    if (!$Pins.ContainsKey($baseFull)) { $Pins[$baseFull] = [WinGPUDoctor.Packaging.PinnedOutput]::CreateAndPin($baseFull, $false) }
    $current = $baseFull
    foreach ($part in @(if ($full.Length -gt $baseFull.Length) { $full.Substring($baseFull.Length + 1).Split([IO.Path]::DirectorySeparatorChar) })) {
        $current = Join-Path $current $part
        if (!$Pins.ContainsKey($current)) { $Pins[$current] = [WinGPUDoctor.Packaging.PinnedOutput]::CreateAndPin($current, [bool]$New) }
    }
    $full
}

# Writes a new file inside a pinned folder (Add-PinnedFolders); fails rather than replace or follow anything there.
function Write-NewFile([string]$Path, [byte[]]$Bytes, [Nullable[DateTime]]$LastWriteTimeUtc = $null) {
    $stream = [WinGPUDoctor.Packaging.PinnedOutput]::CreateNewFile($Path)
    try {
        $stream.Write($Bytes, 0, $Bytes.Length)
        $stream.Flush()
        if ($null -ne $LastWriteTimeUtc) { [IO.File]::SetLastWriteTimeUtc($stream.SafeFileHandle, [DateTime]$LastWriteTimeUtc) }
    }
    finally { $stream.Dispose() }
}
