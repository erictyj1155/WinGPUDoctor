# Deterministic checks of the package layout and path guard over the existing Release build output.
# Runs no collection and writes only a temporary folder under the system temp directory, removed afterwards;
# dev.ps1 -Action test runs it after the build.
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

# Output locations: only plain folders inside the base. A junction is refused even though its path text stays
# inside the base, and so is a junction whose target is gone.
$temp = Join-Path ([IO.Path]::GetTempPath()) ('wingpudoctor-layout-' + [guid]::NewGuid().ToString('N'))
$base = Join-Path $temp 'artifacts'
$outside = Join-Path $temp 'outside'
$gone = Join-Path $temp 'gone'
foreach ($folder in (Join-Path $base 'plain'), $outside, $gone) { [void][IO.Directory]::CreateDirectory($folder) }
$link = Join-Path $base 'link'
$dangling = Join-Path $base 'dangling'
try {
    [void](New-Item -ItemType Junction -Path $link -Target $outside)
    [void](New-Item -ItemType Junction -Path $dangling -Target $gone)
    [IO.Directory]::Delete($gone)
    function Get-Refusal([string]$Base, [string]$Path) { try { [void](Resolve-PlainPath $Base $Path); '' } catch { $_.Exception.Message } }
    Assert-True ((Resolve-PlainPath $base 'plain/new/deeper') -ceq (Join-Path $base 'plain\new\deeper') -and
        (Resolve-PlainPath $base $base) -ceq $base) 'a plain or new folder inside the base is accepted'
    Assert-True ((Get-Refusal $base 'link/new') -match 'junction' -and (Get-Refusal $base $link) -match 'junction') 'a junction inside the base is refused'
    Assert-True ((Test-PathEntry $dangling) -and (Get-Refusal $base 'dangling/new') -match 'junction') 'a junction to a missing target is refused'
    Assert-True ((Get-Refusal $link 'new') -match 'junction') 'a junction as the base is refused'
    Assert-True ((Get-Refusal $base '../outside') -match 'inside' -and (Get-Refusal $base $outside) -match 'inside') 'a folder outside the base is refused'
}
finally {
    foreach ($junction in $link, $dangling) { if (Test-PathEntry $junction) { [IO.Directory]::Delete($junction) } }
    Remove-Item -LiteralPath $temp -Recurse -Force
}

# The apphost template is the one MSBuild selects for both executables, from the building SDK's own host pack.
# A copy anywhere else, such as the writable package cache, is refused, and so is a missing one.
$localSdk = Join-Path $root '.tools/dotnet/dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$dotnetRoot = Split-Path $dotnet -Parent
$templatePath = Get-SelectedAppHostTemplate $dotnet @((Join-Path $root 'src/WinGPUDoctor.Cli/WinGPUDoctor.Cli.csproj'), (Join-Path $root 'src/WinGPUDoctor.Desktop/WinGPUDoctor.Desktop.csproj'))
$packs = [IO.Path]::GetFullPath((Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Host.win-x64'))
Assert-True ($templatePath -is [string] -and $templatePath.StartsWith($packs + '\', [StringComparison]::OrdinalIgnoreCase)) 'the selected apphost template is in the SDK host pack'
function Get-TemplateRefusal([string]$Path) { try { [void](Assert-TrustedAppHostTemplate $Path $dotnetRoot); '' } catch { $_.Exception.Message } }
$inPack = 'runtimes/win-x64/native/apphost.exe'
Assert-True ((Get-TemplateRefusal (Join-Path $root ".tools/packages/microsoft.netcore.app.host.win-x64/10.0.12/$inPack")) -match 'host pack of the SDK' -and
    (Get-TemplateRefusal (Join-Path $dotnetRoot 'sdk/10.0.401/AppHostTemplate/apphost.exe')) -match 'host pack of the SDK' -and
    (Get-TemplateRefusal (Join-Path $packs "10.0.12/../../../../packages/x/$inPack")) -match 'host pack of the SDK') 'an apphost template outside the SDK host pack is refused'
Assert-True ((Get-TemplateRefusal (Join-Path $packs "0.0.0/$inPack")) -match 'missing') 'a missing apphost template is refused'
$template = [IO.File]::ReadAllBytes($templatePath)

# Path guard over the GUI files as built: no local checkout or profile path, PDB paths rooted at /_/, and the
# executables recognized as SDK apphosts of that template, as packaging does.
$needles = New-PathLeakNeedles @($root, $env:USERPROFILE)
foreach ($relative in Get-PackageGuiFiles) {
    $findings = @(Get-PathLeakFindings $relative ([IO.File]::ReadAllBytes((Join-Path $gui $relative))) $needles $template)
    Assert-True ($findings.Count -eq 0) "the built $relative carries no local path"
}
$cliExe = [IO.File]::ReadAllBytes((Join-Path $cli 'wingpudoctor.exe'))
Assert-True (@(Get-PathLeakFindings 'wingpudoctor.exe' $cliExe $needles $template).Count -eq 0) 'the built CLI executable is a recognized apphost'
$guiExe = [IO.File]::ReadAllBytes((Join-Path $gui 'wingpudoctor-gui.exe'))
Assert-True (@(@(Get-PathLeakFindings 'wingpudoctor-gui.exe' $guiExe $needles) -match 'not rooted at /_/').Count -eq 1) 'without the apphost template an executable is rejected'
Assert-True (@(@(Get-PathLeakFindings 'wingpudoctor-other.exe' $guiExe $needles $template) -match 'not rooted at /_/').Count -eq 1) 'an apphost that launches a different DLL is rejected'

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
Assert-True (@(@(Get-PathLeakFindings 'wingpudoctor-gui.exe' $exe $needles $template) -match 'PDB path is under a Windows user profile').Count -eq 1) 'a GUI executable PDB path under a profile is reported'
# Any other absolute PDB path in an executable, outside the checkout and user profiles, is rejected too.
$elsewhere = Get-Rewritten (Join-Path $gui 'wingpudoctor-gui.exe') 'D:\a\_work\1\s\' 'D:\Dev\GPU\app\'
$elsewhereFindings = @(Get-PathLeakFindings 'wingpudoctor-gui.exe' $elsewhere $needles $template)
Assert-True (@($elsewhereFindings -match 'not rooted at /_/').Count -eq 1 -and @($elsewhereFindings -match 'user profile|user-profile|checkout').Count -eq 0) 'an executable PDB path under D:\Dev is reported as unmapped'
# The apphost exception covers the whole template: a one-byte change to its code, entry point or other header
# fields, an executable or longer appended section, or extra trailing bytes is reported.
$reader = [Reflection.PortableExecutable.PEReader]::new([IO.MemoryStream]::new($guiExe, $false))
try {
    $headers = $reader.PEHeaders
    $textOffset = ($headers.SectionHeaders | Where-Object Name -eq '.text').PointerToRawData
    $optional = $headers.PEHeaderStartOffset
    $resourceHeader = $optional + 240 + 40 * ($headers.SectionHeaders.Length - 1)
}
finally { $reader.Dispose() }
function Get-Patched([int]$At, [byte]$Mask) { $copy = [byte[]]$guiExe.Clone(); $copy[$At] = $copy[$At] -bxor $Mask; , $copy }
$patches = [ordered]@{
    'code' = Get-Patched ($textOffset + 64) 0xFF
    'entry point' = Get-Patched ($optional + 16) 0x01
    'DLL characteristics' = Get-Patched ($optional + 70) 0x01
    'section alignment' = Get-Patched ($optional + 32) 0x10
    'executable resources' = Get-Patched ($resourceHeader + 39) 0x20
    'resource section size' = Get-Patched ($resourceHeader + 16) 0x01
}
foreach ($patch in $patches.GetEnumerator()) {
    Assert-True (@(@(Get-PathLeakFindings 'wingpudoctor-gui.exe' $patch.Value $needles $template) -match 'not rooted at /_/').Count -eq 1) "an executable with a changed $($patch.Key) is reported"
}
Assert-True (@(@(Get-PathLeakFindings 'wingpudoctor-gui.exe' ([byte[]]($guiExe + [byte]0)) $needles $template) -match 'not rooted at /_/').Count -eq 1) 'an executable with extra trailing bytes is reported'

"Package layout checks: $count passed, 0 failed."
