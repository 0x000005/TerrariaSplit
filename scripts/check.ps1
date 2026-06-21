$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet restore TerrariaSplit.slnx
dotnet build TerrariaSplit.slnx -c Debug -p:UseSharedCompilation=false -warnaserror
dotnet run --project test\TerrariaSplit.Tests.csproj
.\scripts\check-architecture.ps1
