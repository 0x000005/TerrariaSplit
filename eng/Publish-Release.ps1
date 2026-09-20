[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version,
    [switch]$ClientOnly,
    [switch]$ServerOnly,
    [switch]$NoRestore,
    [switch]$ForceRestore
)

$ErrorActionPreference = 'Stop'

if ($ClientOnly -and $ServerOnly) {
    throw '-ClientOnly and -ServerOnly cannot be used together.'
}
if ($NoRestore -and $ForceRestore) {
    throw '-NoRestore and -ForceRestore cannot be used together.'
}

$publishClient = -not $ServerOnly
$publishServer = -not $ClientOnly

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$clientProject = Join-Path $repositoryRoot 'src/TerrariaSplit.WinForms/TerrariaSplit.WinForms.csproj'
$serverProject = Join-Path $repositoryRoot 'src/TerrariaSplit.Race.Server/TerrariaSplit.Race.Server.csproj'
$buildPropsPath = Join-Path $repositoryRoot 'Directory.Build.props'
$publishRoot = Join-Path $repositoryRoot 'publish'

function Get-ProductVersion {
    [xml]$buildProps = Get-Content -Raw -Encoding UTF8 $buildPropsPath
    $versionNode = $buildProps.SelectSingleNode('/Project/PropertyGroup/TerrariaSplitProductVersion')
    $value = if ($null -eq $versionNode) { '' } else { $versionNode.InnerText.Trim() }
    if ($value -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw 'Directory.Build.props does not define a four-part TerrariaSplitProductVersion.'
    }

    return $value
}

function Set-ProductVersion([string]$NewVersion) {
    $text = [System.IO.File]::ReadAllText($buildPropsPath)
    $pattern = '(<TerrariaSplitProductVersion>)[^<]*(</TerrariaSplitProductVersion>)'
    $matches = [regex]::Matches($text, $pattern)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one TerrariaSplitProductVersion element, found $($matches.Count)."
    }

    $updated = [regex]::Replace(
        $text,
        $pattern,
        {
            param($match)
            return $match.Groups[1].Value + $NewVersion + $match.Groups[2].Value
        })
    [System.IO.File]::WriteAllText(
        $buildPropsPath,
        $updated,
        [System.Text.UTF8Encoding]::new($false))
}

$currentProductVersion = Get-ProductVersion
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    if ([version]$Version -lt [version]$currentProductVersion) {
        throw "Refusing to publish older version $Version; current version is $currentProductVersion."
    }

    if (-not [string]::Equals($Version, $currentProductVersion, [System.StringComparison]::Ordinal)) {
        Set-ProductVersion $Version
        Write-Host "Updated TerrariaSplitProductVersion: $currentProductVersion -> $Version"
    }
}

$productVersion = Get-ProductVersion

$worldFilterSource = $null
if ($publishClient) {
    $physicalRepositoryRoot = [System.IO.Directory]::ResolveLinkTarget($repositoryRoot, $true)
    $worldFilterCandidates = @(
        Join-Path (Split-Path -Parent $repositoryRoot) 'TerrariaResourceJudge/out/resourcejudge-pgo/current/TerrariaSplit.WorldFilter.dll'
    )
    if ($null -ne $physicalRepositoryRoot) {
        $worldFilterCandidates += Join-Path (Split-Path -Parent $physicalRepositoryRoot.FullName) 'TerrariaResourceJudge/out/resourcejudge-pgo/current/TerrariaSplit.WorldFilter.dll'
    }

    $worldFilterSource = $worldFilterCandidates |
        Select-Object -Unique |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($worldFilterSource)) {
        throw "Terraria World Filter was not found. Checked: $($worldFilterCandidates -join '; ')"
    }
}

$releaseArtifactsPath = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot '.build/release')) +
    [System.IO.Path]::DirectorySeparatorChar
$restoreStateRoot = Join-Path $releaseArtifactsPath 'restore-state'
$stagingRoot = Join-Path $releaseArtifactsPath 'staging'
$clientReleaseName = "TerrariaSplit-v$productVersion-win-x64"

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-RestoreInputFingerprint([string]$RestoreName) {
    $inputPaths = [System.Collections.Generic.List[string]]::new()
    foreach ($fileName in @(
            'Directory.Build.props',
            'Directory.Build.targets',
            'Directory.Packages.props',
            'global.json',
            'NuGet.Config')) {
        $path = Join-Path $repositoryRoot $fileName
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $inputPaths.Add($path)
        }
    }

    $sourceRoot = Join-Path $repositoryRoot 'src'
    foreach ($searchPattern in @('*.csproj', '*.props', '*.targets')) {
        foreach ($path in [System.IO.Directory]::EnumerateFiles(
                $sourceRoot,
                $searchPattern,
                [System.IO.SearchOption]::AllDirectories)) {
            $inputPaths.Add($path)
        }
    }

    $dotnetVersion = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($dotnetVersion)) {
        throw 'Unable to determine the .NET SDK version for restore-state validation.'
    }

    $fingerprintSource = [System.Text.StringBuilder]::new()
    [void]$fingerprintSource.AppendLine("restore=$RestoreName")
    [void]$fingerprintSource.AppendLine("dotnet=$dotnetVersion")
    foreach ($path in $inputPaths | Sort-Object -Unique) {
        $relativePath = [System.IO.Path]::GetRelativePath($repositoryRoot, $path)
        $content = [System.IO.File]::ReadAllText($path)
        if ([string]::Equals(
                $path,
                $buildPropsPath,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            $content = [regex]::Replace(
                $content,
                '(<TerrariaSplitProductVersion>)[^<]*(</TerrariaSplitProductVersion>)',
                '$1<release-version>$2')
        }

        [void]$fingerprintSource.AppendLine($relativePath)
        [void]$fingerprintSource.AppendLine($content)
    }

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($fingerprintSource.ToString())
    return [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes))
}

function Invoke-CachedRestore(
    [string]$RestoreName,
    [string]$RootProjectName,
    [string[]]$Arguments) {
    $fingerprint = Get-RestoreInputFingerprint $RestoreName
    $stampPath = Join-Path $restoreStateRoot "$RestoreName.sha256"
    $assetsPath = Join-Path $releaseArtifactsPath "obj/$RootProjectName/project.assets.json"
    $cachedFingerprint = if (Test-Path -LiteralPath $stampPath -PathType Leaf) {
        [System.IO.File]::ReadAllText($stampPath).Trim()
    }
    else {
        ''
    }

    if (-not $ForceRestore -and
        (Test-Path -LiteralPath $assetsPath -PathType Leaf) -and
        [string]::Equals(
            $cachedFingerprint,
            $fingerprint,
            [System.StringComparison]::Ordinal)) {
        Write-Host "Reusing verified $RestoreName restore state."
        return
    }

    Invoke-DotNet $Arguments
    if (-not (Test-Path -LiteralPath $assetsPath -PathType Leaf)) {
        throw "Restore completed without producing the expected assets file: $assetsPath"
    }

    [System.IO.Directory]::CreateDirectory($restoreStateRoot) | Out-Null
    [System.IO.File]::WriteAllText(
        $stampPath,
        $fingerprint + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
}

function Assert-ReleaseFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required release file is missing: $Path"
    }

    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0) {
        throw "Required release file is empty: $Path"
    }

    return $file
}

function New-ReleaseFileRecord(
    [string]$Product,
    [string]$Platform,
    [System.IO.FileInfo]$File,
    [string]$ExpectedFileVersion = '') {
    if (-not [string]::IsNullOrWhiteSpace($ExpectedFileVersion)) {
        $actualVersion = $File.VersionInfo.FileVersion
        if (-not [string]::Equals(
                $actualVersion,
                $ExpectedFileVersion,
                [System.StringComparison]::Ordinal)) {
            throw "Release version mismatch for $($File.FullName): expected $ExpectedFileVersion, found $actualVersion."
        }
    }

    $hash = Get-FileHash -LiteralPath $File.FullName -Algorithm SHA256
    return [pscustomobject]@{
        Product = $Product
        Platform = $Platform
        File = [System.IO.Path]::GetRelativePath($repositoryRoot, $File.FullName)
        Bytes = $File.Length
        SHA256 = $hash.Hash
    }
}

function Test-ClientRelease([string]$ReleaseRoot) {
    $releaseDirectory = Join-Path $ReleaseRoot $clientReleaseName
    if (-not (Test-Path -LiteralPath $releaseDirectory -PathType Container)) {
        throw "Client release directory is missing: $releaseDirectory"
    }

    $client = Assert-ReleaseFile (Join-Path $releaseDirectory 'TerrariaSplit.exe')
    $memoryBridge = Assert-ReleaseFile (Join-Path $releaseDirectory 'TerrariaSplit.MemoryBridge.exe')
    $worldFilter = Assert-ReleaseFile (Join-Path $releaseDirectory 'TerrariaSplit.WorldFilter.dll')
    $manifestFile = Assert-ReleaseFile (Join-Path $releaseDirectory 'Runtime/terrariasplit-update-manifest.json')

    try {
        $manifest = [System.IO.File]::ReadAllText($manifestFile.FullName) | ConvertFrom-Json
    }
    catch {
        throw "Client update manifest is not valid JSON: $($manifestFile.FullName): $($_.Exception.Message)"
    }

    $expectedManagedRoots = @(
        'TerrariaSplit.exe',
        'TerrariaSplit.MemoryBridge.exe',
        'TerrariaSplit.WorldFilter.dll',
        'Runtime',
        'Assets'
    )
    if ($manifest.schemaVersion -ne 1 -or
        (@($manifest.managedRoots) -join "`n") -ne ($expectedManagedRoots -join "`n")) {
        throw "Client update manifest has an unexpected schema or managedRoots list: $($manifestFile.FullName)"
    }

    return @(
        New-ReleaseFileRecord 'Client' 'win-x64' $client $productVersion
        New-ReleaseFileRecord 'MemoryBridge' 'win-x86' $memoryBridge $productVersion
        New-ReleaseFileRecord 'WorldFilter' 'win-x64' $worldFilter
    )
}

function Test-ServerRelease([string]$ReleaseRoot) {
    $records = @()
    foreach ($runtimeIdentifier in @('win-x64', 'linux-x64')) {
        $releaseDirectory = Join-Path $ReleaseRoot "TerrariaSplit.Race.Server-v$productVersion-$runtimeIdentifier"
        if (-not (Test-Path -LiteralPath $releaseDirectory -PathType Container)) {
            throw "Server release directory is missing: $releaseDirectory"
        }

        $fileName = if ($runtimeIdentifier -eq 'win-x64') {
            'TerrariaSplit.Race.Server.exe'
        }
        else {
            'TerrariaSplit.Race.Server'
        }
        $server = Assert-ReleaseFile (Join-Path $releaseDirectory $fileName)
        $expectedVersion = if ($runtimeIdentifier -eq 'win-x64') { $productVersion } else { '' }
        $records += New-ReleaseFileRecord 'RaceServer' $runtimeIdentifier $server $expectedVersion
    }

    return $records
}

$finalReleaseDirectories = @()
if ($publishClient) {
    $finalReleaseDirectories += Join-Path $publishRoot $clientReleaseName
}
if ($publishServer) {
    foreach ($runtimeIdentifier in @('win-x64', 'linux-x64')) {
        $finalReleaseDirectories += Join-Path $publishRoot "TerrariaSplit.Race.Server-v$productVersion-$runtimeIdentifier"
    }
}

$existingReleaseDirectories = @(
    $finalReleaseDirectories |
        Where-Object { Test-Path -LiteralPath $_ }
)
if ($existingReleaseDirectories.Count -gt 0) {
    throw "Refusing to overwrite existing release directories. Publish a new version instead: $($existingReleaseDirectories -join '; ')"
}

Push-Location $repositoryRoot
try {
    if (-not $NoRestore) {
        if ($publishClient) {
            Invoke-CachedRestore 'client-release-r2r-win-x64' 'TerrariaSplit.WinForms' @(
                'restore', $clientProject,
                '-r', 'win-x64',
                '-p:Configuration=Release', '-p:PublishReadyToRun=true',
                '-m:1',
                "-p:ArtifactsPath=$releaseArtifactsPath"
            )
        }
        if ($publishServer) {
            Invoke-CachedRestore 'server-release-selfcontained-win-x64-linux-x64' 'TerrariaSplit.Race.Server' @(
                'restore', $serverProject,
                '-p:Configuration=Release', '-p:SelfContained=true',
                '-m:1',
                "-p:ArtifactsPath=$releaseArtifactsPath"
            )
        }
    }

    if ($publishClient) {
        $clientStagingDirectory = Join-Path $stagingRoot $clientReleaseName
        $clientPublishDirectory = [System.IO.Path]::GetFullPath($clientStagingDirectory) +
            [System.IO.Path]::DirectorySeparatorChar
        Invoke-DotNet @(
            'publish', $clientProject,
            '--no-restore', '-c', 'Release', '-r', 'win-x64',
            '-m:1', '-p:UseSharedCompilation=false',
            "-p:ArtifactsPath=$releaseArtifactsPath",
            "-p:PublishDir=$clientPublishDirectory",
            "-p:TerrariaWorldFilterSource=$worldFilterSource"
        )
    }

    if ($publishServer) {
        foreach ($runtimeIdentifier in @('win-x64', 'linux-x64')) {
            $serverReleaseName = "TerrariaSplit.Race.Server-v$productVersion-$runtimeIdentifier"
            $serverStagingDirectory = Join-Path $stagingRoot $serverReleaseName
            $serverPublishDirectory = [System.IO.Path]::GetFullPath($serverStagingDirectory) +
                [System.IO.Path]::DirectorySeparatorChar
            Invoke-DotNet @(
                'publish', $serverProject,
                '--no-restore', '-c', 'Release', '-r', $runtimeIdentifier,
                '--self-contained', 'true',
                '-m:1', '-p:UseSharedCompilation=false',
                "-p:ArtifactsPath=$releaseArtifactsPath",
                "-p:PublishDir=$serverPublishDirectory"
            )
        }
    }

    if ($publishClient) {
        Test-ClientRelease $stagingRoot | Out-Null
    }
    if ($publishServer) {
        Test-ServerRelease $stagingRoot | Out-Null
    }

    foreach ($finalReleaseDirectory in $finalReleaseDirectories) {
        $releaseName = Split-Path -Leaf $finalReleaseDirectory
        $stagingDirectory = Join-Path $stagingRoot $releaseName
        Move-Item -LiteralPath $stagingDirectory -Destination $finalReleaseDirectory
    }

    $verifiedFiles = @()
    if ($publishClient) {
        $verifiedFiles += Test-ClientRelease $publishRoot
    }
    if ($publishServer) {
        $verifiedFiles += Test-ServerRelease $publishRoot
    }

    Write-Host "Published and verified TerrariaSplit ${productVersion}:"
    $verifiedFiles | Format-List Product, Platform, File, Bytes, SHA256 | Out-Host
}
finally {
    Pop-Location
}
