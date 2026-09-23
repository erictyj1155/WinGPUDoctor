# One dependency-derived runtime closure for generation, copying and fingerprints.
function ConvertTo-WorkerRelativePath([string]$Path) {
    $normalized = $Path.Replace('\', '/')
    if ([string]::IsNullOrWhiteSpace($normalized) -or [IO.Path]::IsPathRooted($normalized) -or
        $normalized -match '[:<>"|?*\x00-\x1f]' -or
        @($normalized.Split('/') | Where-Object { $_ -in @('', '.', '..') -or $_ -match '[. ]$' }).Count) {
        throw 'Unsafe worker relative path.'
    }
    $normalized
}
function Get-WorkerRuntimeFiles([string]$Root) {
    $Root = [IO.Path]::GetFullPath($Root)
    $deps = Get-Content -LiteralPath (Join-Path $Root 'wingpudoctor-worker.deps.json') -Raw | ConvertFrom-Json -AsHashtable
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $declared = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    function Add-WorkerAsset([string]$Path) {
        $normalized = ConvertTo-WorkerRelativePath $Path
        if (!$declared.Add($normalized)) { throw 'Duplicate normalized worker runtime asset.' }
        [void]$paths.Add($normalized)
    }
    foreach ($name in @('wingpudoctor-worker.dll', 'wingpudoctor-worker.deps.json', 'wingpudoctor-worker.runtimeconfig.json')) { [void]$paths.Add($name) }
    $target = $deps.targets[$deps.runtimeTarget.name]
    if (!$target) { throw 'Worker runtime target is missing.' }
    foreach ($library in $target.Values) {
        foreach ($group in @('runtime', 'native')) {
            if ($library.ContainsKey($group)) {
                foreach ($asset in $library[$group].Keys) {
                    $safe = ConvertTo-WorkerRelativePath $asset
                    Add-WorkerAsset ([IO.Path]::GetFileName($safe))
                }
            }
        }
        if ($library.ContainsKey('runtimeTargets')) {
            foreach ($asset in $library.runtimeTargets.Keys) { Add-WorkerAsset $asset }
        }
        if ($library.ContainsKey('resources')) {
            foreach ($asset in $library.resources.Keys) {
                $safe = ConvertTo-WorkerRelativePath $asset
                Add-WorkerAsset ($library.resources[$asset].locale + '/' + [IO.Path]::GetFileName($safe))
            }
        }
    }
    $directories = [Collections.Generic.Stack[string]]::new()
    $directories.Push($Root)
    while ($directories.Count) {
        $directory = Get-Item -LiteralPath $directories.Pop()
        if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse points are not accepted in worker deployments.' }
        foreach ($file in Get-ChildItem -LiteralPath $directory.FullName -Force) {
            if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse points are not accepted in worker deployments.' }
            if ($file.PSIsContainer) { $directories.Push($file.FullName) }
            elseif ($file.Extension -ine '.pdb') {
                $relative = ConvertTo-WorkerRelativePath ([IO.Path]::GetRelativePath($Root, $file.FullName))
                if (!$paths.Contains($relative)) { throw "Unexpected worker runtime asset: $relative" }
            }
        }
    }
    foreach ($relative in ($paths | Sort-Object)) {
        $file = Get-Item -LiteralPath (Join-Path $Root $relative) -ErrorAction Stop
        if ($file.PSIsContainer) { throw 'Runtime asset is not a file.' }
        [pscustomobject]@{ RelativePath = $relative; FullName = $file.FullName; Length = $file.Length; Extension = $file.Extension }
    }
}
