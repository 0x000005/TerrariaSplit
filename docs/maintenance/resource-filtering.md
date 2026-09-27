# Resource filtering

All conditions apply only to Small Crimson worlds. Special and secret seed selections do not restrict eligibility: analysis uses the ordinary numeric seed, while actual world creation retains the original selections. Fixed-seed entry continues to bypass random candidate selection.

The master switch and eligibility only disable controls and execution; they do not erase selected conditions.

All game versions use the current 1.4.5.8 ResourceJudge rules for filtering, including 1.4.4.9. Cross-version generation differences are accepted; this does not change the actual game's menu automation, world generation or Race version compatibility requirements.

1. There is no managed pre-screen in any runtime filtering path. All enabled filters, including Pyramid, go directly to the existing DLL. Historical simulation utilities remain isolated to diagnostics/tests.
2. Run ResourceJudge ABI/protocol 5 once with all enabled requirements and thresholds. The DLL returns the final decision. Pyramid item, gold and entrance-depth conditions must match the same pyramid. Pyramid items use OR; jungle items use AND. Combined native requests can reject impossible pyramid candidates at P2/P32; Crimson distance is checked at P29. Pyramid items/gold are checked at P40, Finch Staff at P42, pyramid depth at P53, jungle depth at P59, Starfury at P69 and jungle resources at P97. Failed checkpoints stop native generation immediately.
3. Accept only after the requested conditions are satisfied. Candidate simulation failures skip the candidate; three consecutive candidate failures stop UI/Race filtering with diagnostics. Missing or incompatible native components fail closed.
4. Generate the real world once. There is no resource validation of the generated file. File readiness and metadata checks remain for world-pool/Race installation.

Pyramid maximum entrance depth has its own visible row and toggle, available only while the parent Pyramid filter is enabled: disabled=0, Medium=30, Shallow=15, Open-air=2 tiles. These are inclusive upper bounds. The default is Medium. Disabling Pyramid preserves the selected depth but removes it from execution and the effective world-pool signature. Other numeric thresholds are defined in Configuration/AppSettings.cs.

Ordinary filtering processes one candidate at a time with native threads=0 (automatic, up to four). Multi-threaded native calls retain a shared single-world lease until native completion, even if the caller times out. Race evaluates parallel candidates with threads=1 per native call. The DLL is loaded/copied from TerrariaResourceJudge/out/resourcejudge-pgo/current/TerrariaSplit.WorldFilter.dll, with no older ABI fallback.

The background world pool evaluates batches of ceil(logicalProcessorCount / 5) candidates (at least one), each with native threads=1, through the same evaluator and candidate-failure policy as foreground filtering. Accepted seeds are queued for serial TerrariaServer generation, without evaluating the same seed again. A changed filter signature discards queued seeds; outdated results are not banked. Disabled/ineligible filtering and fixed-seed generation retain their existing bypass behavior.

Save order is cleanup of old non-favorite saves (or inventory-only when PreserveExistingSaves is enabled), installation of a pooled world copy, then player creation. No save cleanup runs after installation. Removing the consumed pool entry deletes only the private pool file, never the installed copy. Background generation operates in private scratch storage and never cleans the user's Players/Worlds directories.

World pool signatures include the independent depth limit and the new resource-analysis generation policy, so previously banked worlds do not satisfy the new signature.

Starfury and Finch Staff are independent optional filters (off by default), available in ordinary, background and Race filtering. Their inclusive maximum horizontal distances from world center are Starfury=100/200/300 and Finch Staff=300/500/800 tiles. Any matching chest qualifies for its item; enabling both requires both. The DLL evaluates actual target-item chest top-left X coordinates; Y is ignored. Requirements request Starfury P69/Finch Staff P42 respectively, combined with other requirements in the same call. Both settings are persisted, sent in Race world settings and included in the effective world-pool signature.
