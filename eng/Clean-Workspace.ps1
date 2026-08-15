param(
    [switch]$Execute,
    [switch]$IncludeIde
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..')).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
$repositoryPrefix = $repositoryRoot + [System.IO.Path]::DirectorySeparatorChar

function Resolve-WorkspaceTarget([string]$RelativePath) {
    $resolved = [System.IO.Path]::GetFullPath(
        (Join-Path $repositoryRoot $RelativePath)).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    if (-not $resolved.StartsWith(
            $repositoryPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Cleanup target escapes the repository: $resolved"
    }

    $relative = [System.IO.Path]::GetRelativePath($repositoryRoot, $resolved)
    if ($relative -in @('.', '.git', 'src', 'test')) {
        throw "Refusing broad cleanup target: $resolved"
    }

    return $resolved
}

function Get-TargetBytes([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return [long]0
    }

    $item = Get-Item -LiteralPath $Path -Force
    if (-not $item.PSIsContainer) {
        return [long]$item.Length
    }

    return [long](Get-ChildItem -LiteralPath $Path -File -Force -Recurse -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum).Sum
}

$targetSet = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
foreach ($relativePath in @(
    '.build',
    'test/Temp',
    'src/TerrariaSplit.MemoryBridge.Bootstrap/bin',
    'src/TerrariaSplit.MemoryBridge.Bootstrap/obj')) {
    [void]$targetSet.Add((Resolve-WorkspaceTarget $relativePath))
}

# Remove output directories left by the pre-.build layout.
foreach ($projectFile in Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src'), (Join-Path $repositoryRoot 'test') -Filter '*.csproj' -File -Recurse) {
    foreach ($outputDirectoryName in @('bin', 'obj')) {
        [void]$targetSet.Add((Join-Path $projectFile.DirectoryName $outputDirectoryName))
    }
}

foreach ($userFile in Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src'), (Join-Path $repositoryRoot 'test') -Filter '*.user' -File -Recurse) {
    [void]$targetSet.Add($userFile.FullName)
}

if ($IncludeIde) {
    [void]$targetSet.Add((Resolve-WorkspaceTarget '.vs'))
}

$targets = @($targetSet |
    Where-Object { Test-Path -LiteralPath $_ } |
    ForEach-Object {
        $relative = [System.IO.Path]::GetRelativePath($repositoryRoot, $_)
        $resolved = Resolve-WorkspaceTarget $relative
        [pscustomobject]@{
            Path = $resolved
            RelativePath = $relative
            Bytes = Get-TargetBytes $resolved
        }
    } |
    Sort-Object RelativePath)

$targets | Select-Object RelativePath, @{Name='MiB'; Expression={ [math]::Round($_.Bytes / 1MB, 2) }} |
    Format-Table -AutoSize
Write-Output ('Total removable size: {0:N2} MiB' -f (($targets | Measure-Object Bytes -Sum).Sum / 1MB))

if (-not $Execute) {
    Write-Output 'Dry run only. Pass -Execute to remove the listed regenerable outputs.'
    exit 0
}

foreach ($target in $targets) {
    Remove-Item -LiteralPath $target.Path -Recurse -Force
    Write-Output ('Removed: {0}' -f $target.RelativePath)
}
