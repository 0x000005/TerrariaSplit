$ErrorActionPreference = 'Stop'

$NoRestore = $args -contains '-NoRestore'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$clientProject = Join-Path $repositoryRoot 'src/TerrariaSplit.WinForms/TerrariaSplit.WinForms.csproj'
$serverProject = Join-Path $repositoryRoot 'src/TerrariaSplit.Race.Server/TerrariaSplit.Race.Server.csproj'
$buildPropsPath = Join-Path $repositoryRoot 'Directory.Build.props'

$physicalRepositoryRoot = [System.IO.Directory]::ResolveLinkTarget($repositoryRoot, $true)
$worldFilterCandidates = @(
    Join-Path (Split-Path -Parent $repositoryRoot) 'TerrariaJungleJudge/out/build/x64-release/Release/TerrariaSplit.WorldFilter.dll'
)
if ($null -ne $physicalRepositoryRoot) {
    $worldFilterCandidates += Join-Path (Split-Path -Parent $physicalRepositoryRoot.FullName) 'TerrariaJungleJudge/out/build/x64-release/Release/TerrariaSplit.WorldFilter.dll'
}

$worldFilterSource = $worldFilterCandidates |
    Select-Object -Unique |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($worldFilterSource)) {
    throw "Terraria World Filter was not found. Checked: $($worldFilterCandidates -join '; ')"
}

[xml]$buildProps = Get-Content -Raw -Encoding UTF8 $buildPropsPath
$versionNode = $buildProps.SelectSingleNode('/Project/PropertyGroup/TerrariaSplitProductVersion')
$productVersion = if ($null -eq $versionNode) { '' } else { $versionNode.InnerText.Trim() }
if ([string]::IsNullOrWhiteSpace($productVersion)) {
    throw 'Directory.Build.props does not define TerrariaSplitProductVersion.'
}
$releaseArtifactsPath = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot ".build/release-$productVersion")) +
    [System.IO.Path]::DirectorySeparatorChar

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    if (-not $NoRestore) {
        Invoke-DotNet @(
            'restore', $clientProject,
            '-r', 'win-x64',
            '-m:1',
            "-p:ArtifactsPath=$releaseArtifactsPath"
        )
        Invoke-DotNet @(
            'restore', $serverProject,
            '-m:1',
            "-p:ArtifactsPath=$releaseArtifactsPath"
        )
    }

    Invoke-DotNet @(
        'publish', $clientProject,
        '--no-restore', '-c', 'Release', '-r', 'win-x64',
        '-m:1', '-p:UseSharedCompilation=false',
        "-p:ArtifactsPath=$releaseArtifactsPath",
        "-p:TerrariaWorldFilterSource=$worldFilterSource"
    )

    foreach ($runtimeIdentifier in @('win-x64', 'linux-x64')) {
        Invoke-DotNet @(
            'publish', $serverProject,
            '--no-restore', '-c', 'Release', '-r', $runtimeIdentifier,
            '--self-contained', 'true',
            '-m:1', '-p:UseSharedCompilation=false',
            "-p:ArtifactsPath=$releaseArtifactsPath"
        )
    }

    Write-Host "Published TerrariaSplit $productVersion to:"
    Write-Host "  publish/TerrariaSplit-v$productVersion-win-x64/"
    Write-Host "  publish/TerrariaSplit.Race.Server-v$productVersion-win-x64/"
    Write-Host "  publish/TerrariaSplit.Race.Server-v$productVersion-linux-x64/"
}
finally {
    Pop-Location
}
