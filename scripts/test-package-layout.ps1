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
Assert-True ($cliDeclared.Count -gt 0 -and @(Get-UnpackagedRuntimeFiles $cliDeclared $release).Count -eq 0) 'the CLI deps.json files are packaged'
Assert-True ($guiDeclared.Count -gt 0 -and @(Get-UnpackagedRuntimeFiles $guiDeclared $dev).Count -eq 0) 'the GUI deps.json files are packaged'
# Every asset group the worker closure reads counts, native included: a synthetic deps.json whose native library
# is missing from the layout is reported, which makes packaging fail.
$syntheticDeps = '{ "runtimeTarget": { "name": ".NETCoreApp,Version=v10.0" }, "targets": { ".NETCoreApp,Version=v10.0": {
  "app/1.0.0": { "runtime": { "app.dll": {} } },
  "Native.Package/1.0.0": { "native": { "runtimes/win-x64/native/example-native.dll": {} } },
  "Rid.Package/1.0.0": { "runtimeTargets": { "runtimes/win/lib/net10.0/Rid.Package.dll": { "rid": "win", "assetType": "runtime" } } },
  "Localized.Package/1.0.0": { "resources": { "lib/net10.0/de/Localized.Package.resources.dll": { "locale": "de" } } } } } }'
$syntheticFiles = @(ConvertFrom-DepsRuntimeFiles $syntheticDeps)
Assert-True (Same $syntheticFiles @('app.dll', 'example-native.dll', 'runtimes/win/lib/net10.0/Rid.Package.dll', 'de/Localized.Package.resources.dll')) 'runtime, native, runtimeTargets and resources assets are all read'
$withoutNative = @('app.dll', 'runtimes/win/lib/net10.0/Rid.Package.dll', 'de/Localized.Package.resources.dll')
Assert-True ((@(Get-UnpackagedRuntimeFiles $syntheticFiles $withoutNative) -join '|') -ceq 'example-native.dll') 'an unpackaged native library is reported, so packaging fails'
Assert-True (@(Get-UnpackagedRuntimeFiles $syntheticFiles (@($withoutNative) + 'example-native.dll')).Count -eq 0) 'nothing is reported once the native library is packaged'
foreach ($relative in Get-PackageSharedFiles) {
    Assert-True ((Get-FileHash (Join-Path $cli $relative)).Hash -ceq (Get-FileHash (Join-Path $gui $relative)).Hash) "shared $relative is identical in both builds"
}

# Path guard over the GUI files as built: no local checkout or profile path, PDB paths rooted at /_/, and the
# executables recognized as SDK apphosts by their CodeView record and code, as packaging does.
$localSdk = Join-Path $root '.tools/dotnet/dotnet.exe'
$dotnetRoot = Split-Path $(if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }) -Parent
$appHosts = @(Get-AppHostTemplateSignatures $dotnetRoot)
Assert-True ($appHosts.Count -ge 1) 'an SDK apphost template is found'
$needles = New-PathLeakNeedles @($root, $env:USERPROFILE)
foreach ($relative in Get-PackageGuiFiles) {
    $findings = @(Get-PathLeakFindings $relative ([IO.File]::ReadAllBytes((Join-Path $gui $relative))) $needles $appHosts)
    Assert-True ($findings.Count -eq 0) "the built $relative carries no local path"
}
$cliExe = [IO.File]::ReadAllBytes((Join-Path $cli 'wingpudoctor.exe'))
Assert-True (@(Get-PathLeakFindings 'wingpudoctor.exe' $cliExe $needles $appHosts).Count -eq 0) 'the built CLI executable is a recognized apphost'
$guiExe = [IO.File]::ReadAllBytes((Join-Path $gui 'wingpudoctor-gui.exe'))
Assert-True (@(@(Get-PathLeakFindings 'wingpudoctor-gui.exe' $guiExe $needles) -match 'not rooted at /_/').Count -eq 1) 'without a known apphost template an executable is rejected'

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
Assert-True (@(@(Get-PathLeakFindings 'wingpudoctor-gui.exe' $exe $needles $appHosts) -match 'PDB path is under a Windows user profile').Count -eq 1) 'a GUI executable PDB path under a profile is reported'
# Any other absolute PDB path in an executable, outside the checkout and user profiles, is rejected too.
$elsewhere = Get-Rewritten (Join-Path $gui 'wingpudoctor-gui.exe') 'D:\a\_work\1\s\' 'D:\Dev\GPU\app\'
$elsewhereFindings = @(Get-PathLeakFindings 'wingpudoctor-gui.exe' $elsewhere $needles $appHosts)
Assert-True (@($elsewhereFindings -match 'not rooted at /_/').Count -eq 1 -and @($elsewhereFindings -match 'user profile|user-profile|checkout').Count -eq 0) 'an executable PDB path under D:\Dev is reported as unmapped'
# The apphost exception needs the template's code as well as its PDB record.
$reader = [Reflection.PortableExecutable.PEReader]::new([IO.MemoryStream]::new($guiExe, $false))
try { $textOffset = ($reader.PEHeaders.SectionHeaders | Where-Object Name -eq '.text').PointerToRawData } finally { $reader.Dispose() }
$patched = [byte[]]$guiExe.Clone(); $patched[$textOffset + 64] = $patched[$textOffset + 64] -bxor 0xFF
Assert-True (@(@(Get-PathLeakFindings 'wingpudoctor-gui.exe' $patched $needles $appHosts) -match 'not rooted at /_/').Count -eq 1) 'an executable whose code differs from the apphost template is reported'

"Package layout checks: $count passed, 0 failed."
