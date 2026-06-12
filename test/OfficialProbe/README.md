# Official Pyramid Pass Probe

This is a standalone diagnostic tool. It loads the real Terraria 1.4.5.6 assembly and wraps selected official world-gen passes to emit pass-stop CSV rows.

It is not part of `test/TerrariaSplit.Tests.csproj`; that project only compiles `test/*.cs`.

## Build

From the repository root:

```powershell
$out = "test\OfficialProbe\bin"
New-Item -ItemType Directory -Force $out | Out-Null
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" `
  /nologo `
  /platform:x86 `
  /target:exe `
  /out:"$out\OfficialPyramidPassProbe.exe" `
  /reference:"C:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe" `
  /reference:"D:\OneDrive - huzhaoran\Creative\Terraria\reference\Terraria1456\pyramid-probe\exactgen\bin\ReLogic.dll" `
  /reference:"D:\OneDrive - huzhaoran\Creative\Terraria\reference\Terraria1456\pyramid-probe\exactgen\bin\Newtonsoft.Json.dll" `
  /reference:"C:\Windows\Microsoft.NET\assembly\GAC_32\Microsoft.Xna.Framework.Game\v4.0_4.0.0.0__842cf8be1de50553\Microsoft.Xna.Framework.Game.dll" `
  test\OfficialProbe\OfficialPyramidPassProbe.cs
```

If runtime dependency loading fails, pass the copied dependency directory explicitly:

```powershell
test\OfficialProbe\bin\OfficialPyramidPassProbe.exe `
  --deps "D:\OneDrive - huzhaoran\Creative\Terraria\reference\Terraria1456\pyramid-probe\exactgen\bin" `
  --out test\official-pyramid-pass-diagnostics-current.csv `
  747007926 627520318 901484636 1620309102 1602021351 103045530
```

## Output

The CSV records:

- Official `GenVars.PyrX/PyrY/numPyr` candidates after `Dunes`, `Ocean Sand`, `Full Desert`, `Corruption`, and `Clean Up Dirt`.
- The pre-`Pyramids` official decision fields for every candidate: scan tile, spacing, dungeon exclusion, surface check, and reject reason.
- Post-`Pyramids` target-region pyramid tile count and target chest loot summary.

Use this as evidence before changing the integrated simulator. Dataset metrics are not rule sources by themselves.
