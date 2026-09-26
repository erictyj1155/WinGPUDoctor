# Package-time guard: packaged bytes must not carry a local build path. Dot-source only; it reads and reports.
# Release builds map source paths to /_/ (Directory.Build.props); this checks the bytes that are actually packaged.
Add-Type -AssemblyName System.Reflection.Metadata, System.IO.Compression.ZipFile

function Test-FirstPartyDllName([string]$Name) {
    # WinGPUDoctor.*.dll, wingpudoctor.dll, wingpudoctor-gui.dll and wingpudoctor-worker.dll. Tested on the file
    # name, so worker/ copies match.
    [IO.Path]::GetFileName($Name) -match '^wingpudoctor([.-][^/\\]+)?\.dll$'
}

# The packaged .exe files are SDK apphosts: the Microsoft-built native launcher, stamped with the app name and
# resources. Only such a file may keep the launcher's own (non-/_/) PDB path, and it is recognized by content:
# its CodeView record and its code (.text section) must equal those of an SDK apphost template.
function Get-AppHostSignature([byte[]]$Bytes) {
    $reader = [Reflection.PortableExecutable.PEReader]::new([IO.MemoryStream]::new($Bytes, $false))
    try {
        $codeView = @($reader.ReadDebugDirectory() | Where-Object Type -eq 'CodeView')
        $text = @($reader.PEHeaders.SectionHeaders | Where-Object Name -eq '.text')
        if ($codeView.Count -ne 1 -or $text.Count -ne 1) { return $null }
        $data = $reader.ReadCodeViewDebugDirectoryData($codeView[0])
        $length = [Math]::Min($text[0].VirtualSize, $text[0].SizeOfRawData)
        if ($text[0].PointerToRawData + $length -gt $Bytes.Length) { return $null }
        $code = [Security.Cryptography.SHA256]::HashData([IO.MemoryStream]::new($Bytes, $text[0].PointerToRawData, $length, $false))
        '{0}|{1}|{2}|{3}' -f $data.Guid, $data.Age, $data.Path, [Convert]::ToHexString($code)
    }
    catch [BadImageFormatException] { $null }
    finally { $reader.Dispose() }
}

function Get-AppHostTemplateSignatures([string]$DotnetRoot) {
    # The SDK's own template and the Windows x64 host pack, plus host packs restored into the NuGet cache.
    $patterns = @(
        (Join-Path $DotnetRoot 'sdk/*/AppHostTemplate/apphost.exe'),
        (Join-Path $DotnetRoot 'packs/Microsoft.NETCore.App.Host.win-x64/*/runtimes/win-x64/native/apphost.exe')
    )
    if ($env:NUGET_PACKAGES) { $patterns += Join-Path $env:NUGET_PACKAGES 'microsoft.netcore.app.host.win-x64/*/runtimes/win-x64/native/apphost.exe' }
    $signatures = foreach ($pattern in $patterns) {
        foreach ($file in @(Get-ChildItem -Path $pattern -File -ErrorAction SilentlyContinue)) { Get-AppHostSignature ([IO.File]::ReadAllBytes($file.FullName)) }
    }
    , @($signatures | Where-Object { $_ } | Sort-Object -Unique)
}

function New-PathLeakNeedles([string[]]$Roots) {
    # UTF-8 and UTF-16LE forms with \, / and JSON-escaped \\ separators, as Latin-1 text (one character per byte).
    $latin1 = [Text.Encoding]::Latin1
    $needles = [Collections.Generic.List[string]]::new()
    foreach ($root in $Roots) {
        if ([string]::IsNullOrWhiteSpace($root)) { continue }
        $full = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($root))
        if ($full.Length -lt 4 -or $full -eq [IO.Path]::GetPathRoot($full)) { throw 'A forbidden path must not be a drive root.' }
        foreach ($form in @($full, $full.Replace('\', '/'), $full.Replace('\', '\\'))) {
            foreach ($encoding in @([Text.Encoding]::UTF8, [Text.Encoding]::Unicode)) {
                $needles.Add($latin1.GetString($encoding.GetBytes($form)))
            }
        }
    }
    if ($needles.Count -eq 0) { throw 'No forbidden local path was supplied.' }
    , $needles.ToArray()
}

function Get-PathLeakFindings([string]$Name, [byte[]]$Bytes, [string[]]$Needles, [string[]]$AppHostSignatures = @()) {
    # Findings name the entry and the failed check, never the path itself.
    $text = [Text.Encoding]::Latin1.GetString($Bytes)
    if (@($Needles | Where-Object { $text.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count) {
        "${Name}: contains the local checkout or user-profile path"
    }
    $profilePatterns = @(
        '[A-Z]:[\\/]{1,2}Users[\\/]{1,2}',
        '[A-Z]\x00:\x00[\\/]\x00(?:[\\/]\x00)?U\x00s\x00e\x00r\x00s\x00[\\/]\x00'
    )
    if (@($profilePatterns | Where-Object { [regex]::IsMatch($text, $_, 'IgnoreCase, CultureInvariant') }).Count) {
        "${Name}: contains a Windows user-profile path"
    }
    $fileName = [IO.Path]::GetFileName($Name)
    if ($fileName -match '\.(dll|exe)$') {
        $reader = [Reflection.PortableExecutable.PEReader]::new([IO.MemoryStream]::new($Bytes, $false))
        try {
            $debug = @($reader.ReadDebugDirectory())
            # An embedded PDB is compressed, so the byte checks above cannot see inside it.
            if (@($debug | Where-Object Type -eq 'EmbeddedPortablePdb').Count) { "${Name}: embeds a PDB" }
            # No PDB path of any executable or library (first-party or not, CLI or GUI) may name a user profile.
            foreach ($entry in @($debug | Where-Object Type -eq 'CodeView')) {
                if ($reader.ReadCodeViewDebugDirectoryData($entry).Path -match '\A[A-Z]:[\\/]+Users[\\/]') {
                    "${Name}: PDB path is under a Windows user profile"
                }
            }
            # Every packaged .exe is first-party (an apphost of ours), like the first-party DLLs.
            if ((Test-FirstPartyDllName $fileName) -or $fileName -match '\.exe$') {
                $codeView = @($debug | Where-Object Type -eq 'CodeView')
                $mapped = $codeView.Count -eq 1 -and $reader.ReadCodeViewDebugDirectoryData($codeView[0]).Path -cmatch '^/_/[^:\\]+\.pdb$'
                $appHost = $fileName -match '\.exe$' -and $AppHostSignatures.Count -and $AppHostSignatures -contains (Get-AppHostSignature $Bytes)
                if (!$mapped -and !$appHost) { "${Name}: first-party PDB path is not rooted at /_/" }
            }
        }
        finally { $reader.Dispose() }
    }
}

function Get-DirectoryPathLeakFindings([string]$Root, [string[]]$ForbiddenRoots, [string[]]$AppHostSignatures = @()) {
    $needles = New-PathLeakNeedles $ForbiddenRoots
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File -Force) {
        $relative = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        Get-PathLeakFindings $relative ([IO.File]::ReadAllBytes($file.FullName)) $needles $AppHostSignatures
    }
}

function Get-ZipPathLeakFindings([string]$ZipPath, [string[]]$ForbiddenRoots, [string[]]$AppHostSignatures = @()) {
    $needles = New-PathLeakNeedles $ForbiddenRoots
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($entry in @($archive.Entries | Where-Object { $_.Name })) {
            $buffer = [IO.MemoryStream]::new()
            $stream = $entry.Open()
            try { $stream.CopyTo($buffer) } finally { $stream.Dispose() }
            Get-PathLeakFindings $entry.FullName $buffer.ToArray() $needles $AppHostSignatures
        }
    }
    finally { $archive.Dispose() }
}
