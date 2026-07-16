param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'Visual Studio Installer vswhere.exe was not found.'
}

$visualStudio = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if ([string]::IsNullOrWhiteSpace($visualStudio)) {
    throw 'Visual Studio C++ x86 build tools were not found.'
}

$toolRoot = Join-Path $visualStudio 'VC\Tools\MSVC'
$toolVersion = Get-ChildItem -LiteralPath $toolRoot -Directory |
    Sort-Object Name -Descending |
    Select-Object -First 1
if ($null -eq $toolVersion) {
    throw 'The Visual C++ toolset directory was not found.'
}

$kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'
$kitVersion = Get-ChildItem -LiteralPath (Join-Path $kitsRoot 'Include') -Directory |
    Sort-Object Name -Descending |
    Select-Object -First 1
if ($null -eq $kitVersion) {
    throw 'The Windows SDK include directory was not found.'
}

$netFxSdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\NETFXSDK'
$netFxSdkVersion = Get-ChildItem -LiteralPath $netFxSdkRoot -Directory |
    Sort-Object Name -Descending |
    Select-Object -First 1
if ($null -eq $netFxSdkVersion) {
    throw 'The .NET Framework SDK directory was not found.'
}

$cl = Join-Path $toolVersion.FullName 'bin\Hostx64\x86\cl.exe'
$source = Join-Path $PSScriptRoot 'Bootstrap.cpp'
$object = Join-Path $output 'TerrariaSplit.WorldGuard.Bootstrap.obj'
$dll = Join-Path $output 'TerrariaSplit.WorldGuard.Bootstrap.dll'
$vcInclude = Join-Path $toolVersion.FullName 'include'
$vcLib = Join-Path $toolVersion.FullName 'lib\x86'
$sdkInclude = $kitVersion.FullName
$sdkLib = Join-Path (Join-Path $kitsRoot 'Lib') $kitVersion.Name
$optimization = if ($Configuration -eq 'Release') { '/O2' } else { '/Od' }

& $cl @(
    '/nologo', '/LD', '/EHsc', '/MT', $optimization,
    '/DUNICODE', '/D_UNICODE',
    "/Fo$object", "/Fe$dll", $source,
    "/I$vcInclude",
    "/I$(Join-Path $sdkInclude 'ucrt')",
    "/I$(Join-Path $sdkInclude 'shared')",
    "/I$(Join-Path $sdkInclude 'um')",
    "/I$(Join-Path $netFxSdkVersion.FullName 'Include\um')",
    '/link', 'mscoree.lib',
    "/LIBPATH:$vcLib",
    "/LIBPATH:$(Join-Path $sdkLib 'ucrt\x86')",
    "/LIBPATH:$(Join-Path $sdkLib 'um\x86')",
    "/LIBPATH:$(Join-Path $netFxSdkVersion.FullName 'Lib\um\x86')"
)
if ($LASTEXITCODE -ne 0) {
    throw "cl.exe failed with exit code $LASTEXITCODE."
}
