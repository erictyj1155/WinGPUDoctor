# Deterministic checks of the package layout and path guard over the existing Release build output.
# Writes nothing and runs no collection; dev.ps1 -Action test runs it after the build.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/package-layout.ps1"
. "$PSScriptRoot/package-path-guard.ps1"
$cli = Join-Path $root 'src/WinGPUDoctor.Cli/bin/Release/net10.0-windows'
$gui = Join-Path $root 'src/WinGPUDoctor.Desktop/bin/Release/net10.0-windows'
$count = 0
function Assert-True([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw "Package layout check failed: $Message" }
    $script:count++
}
function Same([object[]]$Left, [object[]]$Right) { (@($Left | Sort-Object) -join '|') -ceq (@($Right | Sort-Object) -join '|') }

# Layouts: the release layout is the CLI's, now with its Host dependency; the test package adds exactly the GUI.
$release = @(Get-PackageParentFiles)
$dev = @(Get-PackageParentFiles -IncludeGui)
Assert-True ($release -contains 'WinGPUDoctor.Host.dll') 'the release layout includes WinGPUDoctor.Host.dll'
Assert-True (@($release | Where-Object { $_ -like 'wingpudoctor-gui*' }).Count -eq 0) 'the release layout has no GUI file'
Assert-True (Same $dev (@($release) + @(Get-PackageGuiFiles))) 'the test package is the release layout plus the GUI files'
Assert-True (@($dev | Where-Object { $_ -match '\.pdb$' }).Count -eq 0 -and @($dev | Select-Object -Unique).Count -eq $dev.Count) 'no PDB or duplicate entry'
Assert-True (@($release | Where-Object { Test-FirstPartyDllName $_ }).Count -eq 6) '6 first-party CLI DLLs'
Assert-True (@($dev | Where-Object { Test-FirstPartyDllName $_ }).Count -eq 7) '7 first-party DLLs with the GUI'
Assert-True ((Test-FirstPartyDllName 'wingpudoctor-gui.dll') -and (Test-FirstPartyDllName 'worker/wingpudoctor-worker.dll') -and
    !(Test-FirstPartyDllName 'wingpudoctor-gui.exe') -and !(Test-FirstPartyDllName 'System.Management.dll')) 'first-party DLL names'
Assert-True ((Get-PackageName '0.1.0') -ceq 'WinGPUDoctor-0.1.0-win-x64' -and
    (Get-PackageName '0.1.0' -Dev) -ceq 'WinGPUDoctor-0.1.0-dev-win-x64') 'package names; only the test package is marked -dev'

# Every application-local runtime file that each executable declares is in its layout.
$cliDeclared = @(Get-DepsRuntimeFiles (Join-Path $cli 'wingpudoctor.deps.json'))
$guiDeclared = @(Get-DepsRuntimeFiles (Join-Path $gui 'wingpudoctor-gui.deps.json'))
Assert-True ($cliDeclared.Count -gt 0 -and @($cliDeclared | Where-Object { $release -notcontains $_ }).Count -eq 0) 'the CLI deps.json files are packaged'
Assert-True ($guiDeclared.Count -gt 0 -and @($guiDeclared | Where-Object { $dev -notcontains $_ }).Count -eq 0) 'the GUI deps.json files are packaged'
foreach ($relative in Get-PackageSharedFiles) {
    Assert-True ((Get-FileHash (Join-Path $cli $relative)).Hash -ceq (Get-FileHash (Join-Path $gui $relative)).Hash) "shared $relative is identical in both builds"
}

# Path guard over the GUI files as built: no local checkout or profile path, PDB path rooted at /_/.
$needles = New-PathLeakNeedles @($root, $env:USERPROFILE)
foreach ($relative in Get-PackageGuiFiles) {
    $findings = @(Get-PathLeakFindings $relative ([IO.File]::ReadAllBytes((Join-Path $gui $relative))) $needles)
    Assert-True ($findings.Count -eq 0) "the built $relative carries no local path"
}

# Path guard catches leaks in GUI files: synthetic text, and PDB paths rewritten in place to the same length.
$latin1 = [Text.Encoding]::Latin1
$synthetic = [Text.Encoding]::UTF8.GetBytes('{"path":"C:\\Users\\builder\\src\\wingpudoctor-gui.dll"}')
Assert-True (@(@(Get-PathLeakFindings 'wingpudoctor-gui.deps.json' $synthetic $needles) -match 'user-profile path').Count -eq 1) 'a profile path in GUI JSON is reported'
$wide = [Text.Encoding]::Unicode.GetBytes('D:\Users\builder\report')
Assert-True (@(@(Get-PathLeakFindings 'wingpudoctor-gui.runtimeconfig.json' $wide $needles) -match 'user-profile path').Count -eq 1) 'a UTF-16 profile path is reported'
function Get-Rewritten([string]$File, [string]$From, [string]$To) {
    if ($From.Length -ne $To.Length) { throw 'Rewrites must keep the length.' }
    $text = $latin1.GetString([IO.File]::ReadAllBytes($File))
    if ($text.IndexOf($From, [StringComparison]::Ordinal) -lt 0) { throw "Expected PDB path text is missing from $File." }
    , $latin1.GetBytes($text.Replace($From, $To))
}
$dll = Get-Rewritten (Join-Path $gui 'wingpudoctor-gui.dll') '/_/src/WinGP' 'C:/Users/ab/'
$dllFindings = @(Get-PathLeakFindings 'wingpudoctor-gui.dll' $dll $needles)
Assert-True (@($dllFindings -match 'not rooted at /_/').Count -eq 1) 'an unmapped GUI PDB path is reported'
Assert-True (@($dllFindings -match 'PDB path is under a Windows user profile').Count -eq 1) 'a GUI DLL PDB path under a profile is reported'
$exe = Get-Rewritten (Join-Path $gui 'wingpudoctor-gui.exe') 'D:\a\_work\' 'C:\Users\a\'
Assert-True (@(@(Get-PathLeakFindings 'wingpudoctor-gui.exe' $exe $needles) -match 'PDB path is under a Windows user profile').Count -eq 1) 'a GUI executable PDB path under a profile is reported'

"Package layout checks: $count passed, 0 failed."
