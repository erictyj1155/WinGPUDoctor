# Package-time guard: packaged bytes must not carry a local build path. Dot-source only; it reads and reports.
# Release builds map source paths to /_/ (Directory.Build.props); this checks the bytes that are actually packaged.
Add-Type -AssemblyName System.Reflection.Metadata, System.IO.Compression.ZipFile

function Test-FirstPartyDllName([string]$Name) {
    # WinGPUDoctor.*.dll, wingpudoctor.dll, wingpudoctor-gui.dll and wingpudoctor-worker.dll. Tested on the file
    # name, so worker/ copies match.
    [IO.Path]::GetFileName($Name) -match '^wingpudoctor([.-][^/\\]+)?\.dll$'
}

# The packaged .exe files are SDK apphosts: the Microsoft-built native launcher that the SDK copies from a template
# and edits for the app. Every packaged .exe must be such a file (it keeps the launcher's own PDB path). There is one trusted
# template: the one MSBuild selects for the build (AppHostSourcePath), which must be in the host pack of the SDK
# that builds the package, reached without a junction or link (Resolve-PlainPath, package-layout.ps1). A copy in
# a writable package cache is never used.
function Get-SelectedAppHostTemplate([string]$Dotnet, [string[]]$Projects) {
    $selected = @(foreach ($project in $Projects) {
        $lines = @(& $Dotnet msbuild $project -nologo -p:Configuration=Release -t:ResolveFrameworkReferences -getProperty:AppHostSourcePath)
        if ($LASTEXITCODE -ne 0) { throw 'Unable to read the apphost template that the build uses.' }
        ($lines -join '').Trim()
    }) | Sort-Object -Unique
    if (@($selected).Count -ne 1 -or !$selected) { throw 'The packaged executables must be built from one apphost template.' }
    Assert-TrustedAppHostTemplate $selected (Split-Path $Dotnet -Parent)
}

function Assert-TrustedAppHostTemplate([string]$Path, [string]$DotnetRoot) {
    $packs = Join-Path $DotnetRoot 'packs/Microsoft.NETCore.App.Host.win-x64'
    $full = try { Resolve-PlainPath $packs $Path } catch { '' }
    if (!$full -or [IO.Path]::GetRelativePath($packs, $full) -notmatch '^\d+\.\d+\.\d+\\runtimes\\win-x64\\native\\apphost\.exe$') {
        throw 'The apphost template must be the Windows x64 host pack of the SDK that builds the package.'
    }
    if (!(Test-Path -LiteralPath $full -PathType Leaf)) { throw 'The selected apphost template is missing.' }
    $full
}

# Offsets of the COFF header, optional header and section table of a PE32+ image whose headers leave room for one
# more section header, or $null.
function Get-PeHeaderOffsets([byte[]]$Bytes) {
    if ($Bytes.Length -lt 0x40 -or [BitConverter]::ToUInt16($Bytes, 0) -ne 0x5A4D) { return $null }
    $coff = [BitConverter]::ToInt32($Bytes, 0x3C) + 4
    if ($coff -lt 4 -or $coff + 260 -gt $Bytes.Length -or [BitConverter]::ToUInt32($Bytes, $coff - 4) -ne 0x4550 -or
        [BitConverter]::ToUInt16($Bytes, $coff + 16) -ne 240 -or [BitConverter]::ToUInt16($Bytes, $coff + 20) -ne 0x20B) { return $null }
    $sections = $coff + 260
    $end = $sections + 40 * ([BitConverter]::ToUInt16($Bytes, $coff + 2) + 1)
    if ($end -gt [BitConverter]::ToUInt32($Bytes, $coff + 80) -or $end -gt $Bytes.Length) { return $null }
    [pscustomobject]@{ Coff = $coff; Optional = $coff + 20; Sections = $sections }
}

# An SDK apphost is its template with only the SDK's own edits (HostWriter): the app DLL name in the placeholder,
# the console or GUI subsystem, the time stamp, and one appended read-only .rsrc section holding the app's
# resources, with the header fields that describe it. Every other template byte (headers, entry point, code, data,
# relocations and the template's own PDB record) must be unchanged, and nothing else may follow.
function Test-SdkAppHost([byte[]]$Bytes, [byte[]]$Template, [string]$AppDll) {
    $t = Get-PeHeaderOffsets $Template
    $b = Get-PeHeaderOffsets $Bytes
    if (!$t -or !$b -or $b.Coff -ne $t.Coff -or $Bytes.Length -le $Template.Length) { return $false }
    $u16 = { param([byte[]]$Data, [int]$At) [BitConverter]::ToUInt16($Data, $At) }
    $u32 = { param([byte[]]$Data, [int]$At) [BitConverter]::ToUInt32($Data, $At) }
    $o = $t.Optional
    $count = & $u16 $Template ($t.Coff + 2)
    $header = $t.Sections + 40 * $count
    # The template is a console launcher without resources and with one app-path placeholder.
    $latin1 = [Text.Encoding]::Latin1
    $templateText = $latin1.GetString($Template)
    $marker = 'c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2'
    $at = $templateText.IndexOf($marker, [StringComparison]::Ordinal)
    if ((& $u16 $Template ($o + 68)) -ne 3 -or [BitConverter]::ToUInt64($Template, $o + 128) -ne 0 -or
        $at -lt 0 -or $templateText.IndexOf($marker, $at + 1, [StringComparison]::Ordinal) -ge 0) { return $false }
    $name = [Text.Encoding]::UTF8.GetBytes($AppDll)
    if ($name.Length -eq 0 -or $name.Length -gt $marker.Length) { return $false }
    # All template bytes outside the edited fields are unchanged.
    $edited = @(@(($t.Coff + 2), 2), @(($t.Coff + 4), 4), @(($o + 8), 4), @(($o + 56), 4), @(($o + 68), 2), @(($o + 128), 8), @($header, 40), @($at, $marker.Length))
    $unedited = [byte[]]::new($Template.Length)
    [Array]::Copy($Bytes, $unedited, $Template.Length)
    foreach ($field in $edited) { [Array]::Copy($Template, $field[0], $unedited, $field[0], $field[1]) }
    if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($unedited)) -cne
        [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Template))) { return $false }
    # The edits are exactly the SDK's: the app DLL name, a subsystem, one more section, and that section is a
    # read-only .rsrc that holds the resource directory, covers the rest of the file and is counted in the sizes.
    $expectedName = [byte[]]::new($marker.Length)
    [Array]::Copy($name, $expectedName, $name.Length)
    $virtualSize = & $u32 $Bytes ($header + 8)
    $rawSize = & $u32 $Bytes ($header + 16)
    $sectionAlignment = & $u32 $Template ($o + 32)
    $resourceSize = & $u32 $Bytes ($o + 132)
    [Convert]::ToHexString($Bytes, $at, $marker.Length) -ceq [Convert]::ToHexString($expectedName) -and
    (& $u16 $Bytes ($o + 68)) -in @(2, 3) -and
    (& $u16 $Bytes ($t.Coff + 2)) -eq $count + 1 -and
    $latin1.GetString($Bytes, $header, 8) -ceq ".rsrc`0`0`0" -and
    $virtualSize -gt 0 -and $virtualSize -le $rawSize -and
    (& $u32 $Bytes ($header + 12)) -eq (& $u32 $Template ($o + 56)) -and
    (& $u32 $Bytes ($header + 20)) -eq $Template.Length -and
    $rawSize -eq $Bytes.Length - $Template.Length -and $rawSize % (& $u32 $Template ($o + 36)) -eq 0 -and
    [Convert]::ToHexString($Bytes, $header + 24, 12) -ceq ('0' * 24) -and
    (& $u32 $Bytes ($header + 36)) -eq 0x40000040 -and
    (& $u32 $Bytes ($o + 128)) -eq (& $u32 $Bytes ($header + 12)) -and $resourceSize -gt 0 -and $resourceSize -le $virtualSize -and
    (& $u32 $Bytes ($o + 56)) -eq (& $u32 $Template ($o + 56)) + [Math]::Ceiling($virtualSize / $sectionAlignment) * $sectionAlignment -and
    (& $u32 $Bytes ($o + 8)) -eq (& $u32 $Template ($o + 8)) + $rawSize
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

function Get-PathLeakFindings([string]$Name, [byte[]]$Bytes, [string[]]$Needles, [byte[]]$AppHostTemplate = $null) {
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
            # Every packaged .exe must be the SDK apphost: the selected template with only the SDK's edits, whatever
            # its PDB path says (a /_/ path does not make an executable acceptable). First-party DLLs need a /_/ path.
            if ($fileName -match '\.exe$') {
                if ($null -eq $AppHostTemplate -or !(Test-SdkAppHost $Bytes $AppHostTemplate ([IO.Path]::ChangeExtension($fileName, '.dll')))) {
                    "${Name}: executable is not the SDK apphost template with only the SDK's edits"
                }
            }
            elseif (Test-FirstPartyDllName $fileName) {
                $codeView = @($debug | Where-Object Type -eq 'CodeView')
                $mapped = $codeView.Count -eq 1 -and $reader.ReadCodeViewDebugDirectoryData($codeView[0]).Path -cmatch '^/_/[^:\\]+\.pdb$'
                if (!$mapped) { "${Name}: first-party PDB path is not rooted at /_/" }
            }
        }
        finally { $reader.Dispose() }
    }
}

function Get-DirectoryPathLeakFindings([string]$Root, [string[]]$ForbiddenRoots, [byte[]]$AppHostTemplate = $null) {
    $needles = New-PathLeakNeedles $ForbiddenRoots
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File -Force) {
        $relative = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        Get-PathLeakFindings $relative ([IO.File]::ReadAllBytes($file.FullName)) $needles $AppHostTemplate
    }
}

function Get-ZipPathLeakFindings([string]$ZipPath, [string[]]$ForbiddenRoots, [byte[]]$AppHostTemplate = $null) {
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try { Get-ZipArchivePathLeakFindings $archive $ForbiddenRoots $AppHostTemplate }
    finally { $archive.Dispose() }
}

# For an archive that is already open, such as the ZIP that packaging still holds exclusively.
function Get-ZipArchivePathLeakFindings([IO.Compression.ZipArchive]$Archive, [string[]]$ForbiddenRoots, [byte[]]$AppHostTemplate = $null) {
    $needles = New-PathLeakNeedles $ForbiddenRoots
    foreach ($entry in @($Archive.Entries | Where-Object { $_.Name })) {
        $buffer = [IO.MemoryStream]::new()
        $stream = $entry.Open()
        try { $stream.CopyTo($buffer) } finally { $stream.Dispose() }
        Get-PathLeakFindings $entry.FullName $buffer.ToArray() $needles $AppHostTemplate
    }
}

# No network capability in packaged code: the app has no network client (SECURITY.md). A packaged managed assembly
# must not reference a .NET networking assembly (System.Net.*) or type, and no packaged PE may import or P/Invoke a
# Windows networking library. This is a static check of what is packaged; the shared framework is not scanned.
$script:NetworkLibraries = @('ws2_32.dll', 'wsock32.dll', 'mswsock.dll', 'winhttp.dll', 'wininet.dll', 'dnsapi.dll', 'iphlpapi.dll',
    'httpapi.dll', 'urlmon.dll', 'websocket.dll', 'netapi32.dll', 'mpr.dll', 'rasapi32.dll', 'fwpuclnt.dll', 'winnsi.dll')

# Names of the DLLs in a PE's import and delay-import tables.
function Get-PeImportedLibraries([byte[]]$Bytes) {
    $reader = [Reflection.PortableExecutable.PEReader]::new([IO.MemoryStream]::new($Bytes, $false))
    try {
        $headers = $reader.PEHeaders
        if (!$headers.PEHeader) { return }
        function Get-Offset([int]$Rva) {
            foreach ($section in $headers.SectionHeaders) {
                $size = [Math]::Max($section.VirtualSize, $section.SizeOfRawData)
                if ($Rva -ge $section.VirtualAddress -and $Rva -lt $section.VirtualAddress + $size) { return $Rva - $section.VirtualAddress + $section.PointerToRawData }
            }
            -1
        }
        function Get-Name([int]$Rva) {
            $at = Get-Offset $Rva
            if ($at -lt 0) { return '' }
            $end = [Array]::IndexOf($Bytes, [byte]0, $at)
            [Text.Encoding]::ASCII.GetString($Bytes, $at, $end - $at)
        }
        foreach ($table in @(@($headers.PEHeader.ImportTableDirectory, 20, 12), @($headers.PEHeader.DelayImportTableDirectory, 32, 4))) {
            if ($table[0].Size -eq 0) { continue }
            $at = Get-Offset $table[0].RelativeVirtualAddress
            while ($at -ge 0 -and $at + $table[1] -le $Bytes.Length) {
                $nameRva = [BitConverter]::ToInt32($Bytes, $at + $table[2])
                if ($nameRva -eq 0) { break }
                Get-Name $nameRva
                $at += $table[1]
            }
        }
    }
    catch [BadImageFormatException] { }
    finally { $reader.Dispose() }
}

function Get-NetworkCapabilityFindings([string]$Name, [byte[]]$Bytes) {
    if ([IO.Path]::GetFileName($Name) -notmatch '\.(dll|exe)$') { return }
    foreach ($library in @(Get-PeImportedLibraries $Bytes)) {
        if ($script:NetworkLibraries -contains $library.ToLowerInvariant()) { "${Name}: imports network library $library" }
    }
    $reader = [Reflection.PortableExecutable.PEReader]::new([IO.MemoryStream]::new($Bytes, $false))
    try {
        if (!$reader.HasMetadata) { return }
        $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($reader)
        foreach ($handle in $metadata.AssemblyReferences) {
            $assembly = $metadata.GetString($metadata.GetAssemblyReference($handle).Name)
            if ($assembly -like 'System.Net*') { "${Name}: references networking assembly $assembly" }
        }
        foreach ($handle in $metadata.TypeReferences) {
            $type = $metadata.GetTypeReference($handle)
            $namespace = $metadata.GetString($type.Namespace)
            if ($namespace -eq 'System.Net' -or $namespace -like 'System.Net.*') { "${Name}: references networking type $namespace.$($metadata.GetString($type.Name))" }
        }
        $moduleCount = [Reflection.Metadata.Ecma335.MetadataReaderExtensions]::GetTableRowCount($metadata, [Reflection.Metadata.Ecma335.TableIndex]::ModuleRef)
        for ($row = 1; $row -le $moduleCount; $row++) {
            $module = $metadata.GetString($metadata.GetModuleReference([Reflection.Metadata.Ecma335.MetadataTokens]::ModuleReferenceHandle($row)).Name)
            $file = if ($module -like '*.dll') { $module } else { "$module.dll" }
            if ($script:NetworkLibraries -contains $file.ToLowerInvariant()) { "${Name}: calls into network library $module" }
        }
    }
    catch [BadImageFormatException] { }
    finally { $reader.Dispose() }
}
