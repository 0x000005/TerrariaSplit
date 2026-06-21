$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Write-Section {
    param([string] $Title)
    Write-Host ''
    Write-Host "== $Title =="
}

function Show-Matches {
    param(
        [string] $Title,
        [string] $Path,
        [string] $Pattern
    )

    Write-Section $Title
    if (-not (Test-Path $Path)) {
        Write-Host "Missing path: $Path"
        return
    }

    $matches = Get-ChildItem -Path $Path -Recurse -Filter *.cs |
        Select-String -Pattern $Pattern

    if ($matches) {
        $matches | ForEach-Object {
            Write-Host "$($_.Path):$($_.LineNumber): $($_.Line.Trim())"
        }
    }
    else {
        Write-Host 'No matches.'
    }
}

Show-Matches `
    -Title 'Application -> AppSettingsStore references' `
    -Path 'TerrariaSplit\Application' `
    -Pattern 'AppSettingsStore'

Show-Matches `
    -Title 'Application -> AppLogger references' `
    -Path 'TerrariaSplit\Application' `
    -Pattern 'AppLogger'

Show-Matches `
    -Title 'Application -> WinForms references' `
    -Path 'TerrariaSplit\Application' `
    -Pattern 'System\.Windows\.Forms|\bForm\b|\bControl\b'

Show-Matches `
    -Title 'Terraria -> UI shell references' `
    -Path 'TerrariaSplit\Terraria' `
    -Pattern 'MainForm|SettingsPage|OverlayWindow|TimerOverlay|ApplicationShellEffectExecutor'

Write-Section 'Root namespace files'
Get-ChildItem -Path 'TerrariaSplit' -Recurse -Filter *.cs |
    Select-String -Pattern '^namespace TerrariaSplit;$' |
    Select-Object -ExpandProperty Path |
    Sort-Object |
    ForEach-Object { Write-Host $_ }

Write-Host ''
Write-Host 'Architecture script is informational in R0. Later phases should make remaining violations fail the check.'
