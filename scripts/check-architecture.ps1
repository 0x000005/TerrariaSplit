$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$failed = $false

function Write-Section {
    param([string] $Title)
    Write-Host ''
    Write-Host "== $Title =="
}

function Test-NoMatches {
    param(
        [string] $Title,
        [string] $Path,
        [string] $Pattern
    )

    Write-Section $Title
    if (-not (Test-Path $Path)) {
        Write-Host "Missing path: $Path"
        $script:failed = $true
        return
    }

    $matches = Get-ChildItem -Path $Path -Recurse -Filter *.cs |
        Select-String -Pattern $Pattern

    if ($matches) {
        $matches | ForEach-Object {
            Write-Host "$($_.Path):$($_.LineNumber): $($_.Line.Trim())"
        }
        $script:failed = $true
    }
    else {
        Write-Host 'No matches.'
    }
}

Test-NoMatches `
    -Title 'Domain -> outer layer references' `
    -Path 'src\TerrariaSplit.Domain\Domain' `
    -Pattern 'System\.Windows\.Forms|TerrariaSplit\.UI|TerrariaSplit\.Storage|TerrariaSplit\.Terraria|\bAppSettingsStore\b|\bAppLogger\b'

Test-NoMatches `
    -Title 'Application -> AppSettingsStore references' `
    -Path 'src\TerrariaSplit.Application\Application' `
    -Pattern '\bAppSettingsStore\b'

Test-NoMatches `
    -Title 'Application -> AppLogger references' `
    -Path 'src\TerrariaSplit.Application\Application' `
    -Pattern '\bAppLogger\b'

Test-NoMatches `
    -Title 'Application -> WinForms references' `
    -Path 'src\TerrariaSplit.Application\Application' `
    -Pattern 'System\.Windows\.Forms|\bForm\b|\bControl\b'

Test-NoMatches `
    -Title 'Terraria -> UI shell references' `
    -Path 'src\TerrariaSplit.Terraria\Terraria' `
    -Pattern 'MainForm|SettingsPage|SettingsForm|OverlayWindow|TimerOverlay|ApplicationShellEffectExecutor'

Test-NoMatches `
    -Title 'Storage -> outer layer references' `
    -Path 'src\TerrariaSplit.Storage\Storage' `
    -Pattern 'TerrariaSplit\.UI|TerrariaSplit\.Application|TerrariaSplit\.Terraria'

Test-NoMatches `
    -Title 'UI Settings -> shell side-effect starters' `
    -Path 'src\TerrariaSplit.WinForms\UI\Settings' `
    -Pattern 'StartCreateWorld|StartEnterWorld|TerrariaWorldAutomation|TerrariaMonitorCoordinator|WorldPoolFillService|GlobalHotkeyManager'

Write-Section 'Root namespace files'
$rootNamespaceMatches = Get-ChildItem -Path 'src' -Recurse -Filter *.cs |
    Select-String -Pattern '^namespace TerrariaSplit;$'

if ($rootNamespaceMatches) {
    $rootNamespaceMatches | ForEach-Object {
        Write-Host "$($_.Path):$($_.LineNumber): $($_.Line.Trim())"
    }
    $failed = $true
}
else {
    Write-Host 'No matches.'
}

if ($failed) {
    throw 'Architecture check failed.'
}

Write-Host ''
Write-Host 'Architecture check passed.'
