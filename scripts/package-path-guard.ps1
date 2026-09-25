# Package-time guard: packaged bytes must not carry a local build path. Dot-source only; it reads and reports.
# Release builds map source paths to /_/ (Directory.Build.props); this checks the bytes that are actually packaged.
Add-Type -AssemblyName System.Reflection.Metadata, System.IO.Compression.ZipFile

function Test-FirstPartyDllName([string]$Name) {
    # WinGPUDoctor.*.dll, wingpudoctor.dll and wingpudoctor-worker.dll. Tested on the file name, so worker/ copies match.
    [IO.Path]::GetFileName($Name) -match '^wingpudoctor([.-][^/\\]+)?\.dll$'
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

function Get-PathLeakFindings([string]$Name, [byte[]]$Bytes, [string[]]$Needles) {
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
            if (Test-FirstPartyDllName $fileName) {
                $codeView = @($debug | Where-Object Type -eq 'CodeView')
                if ($codeView.Count -ne 1 -or
                    $reader.ReadCodeViewDebugDirectoryData($codeView[0]).Path -cnotmatch '^/_/[^:\\]+\.pdb$') {
                    "${Name}: first-party PDB path is not rooted at /_/"
                }
            }
        }
        finally { $reader.Dispose() }
    }
}

function Get-DirectoryPathLeakFindings([string]$Root, [string[]]$ForbiddenRoots) {
    $needles = New-PathLeakNeedles $ForbiddenRoots
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File -Force) {
        $relative = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        Get-PathLeakFindings $relative ([IO.File]::ReadAllBytes($file.FullName)) $needles
    }
}

function Get-ZipPathLeakFindings([string]$ZipPath, [string[]]$ForbiddenRoots) {
    $needles = New-PathLeakNeedles $ForbiddenRoots
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($entry in @($archive.Entries | Where-Object { $_.Name })) {
            $buffer = [IO.MemoryStream]::new()
            $stream = $entry.Open()
            try { $stream.CopyTo($buffer) } finally { $stream.Dispose() }
            Get-PathLeakFindings $entry.FullName $buffer.ToArray() $needles
        }
    }
    finally { $archive.Dispose() }
}
