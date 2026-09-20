# Resource filtering

All conditions apply only to Small Crimson worlds. Special and secret seed selections do not restrict eligibility: analysis uses the ordinary numeric seed, while actual world creation retains the original selections. Fixed-seed entry continues to bypass random candidate selection.

The master switch and eligibility only disable controls and execution; they do not erase selected conditions.

1. If pyramid items are enabled, run the managed pyramid pre-screen with the item mask and gold-pile minimum. A negative result skips the candidate; a positive result cannot accept it.
2. Run ResourceJudge ABI/protocol 4 once with the analysis mask for all enabled conditions. Pyramid item, gold and entrance-depth conditions must match the same pyramid. Pyramid items use OR; jungle items use AND.
3. Accept only after the requested conditions are satisfied. Candidate simulation failures or uncertain results skip the candidate; three consecutive candidate failures stop UI/Race filtering with diagnostics. Missing or incompatible native components fail closed.
4. Generate the real world once. There is no resource validation of the generated file. File readiness and metadata checks remain for world-pool/Race installation.

Pyramid maximum entrance depth is independently selectable: disabled=0, Medium=30, Shallow=15, Open-air=5 tiles. These are inclusive upper bounds. The default is Medium. Other numeric thresholds are defined in Configuration/AppSettings.cs.

Ordinary filtering processes one candidate at a time with native threads=0 (automatic, up to four). Multi-threaded native calls retain a shared single-world lease until native completion, even if the caller times out. Race evaluates parallel candidates with threads=1 per native call. The DLL is loaded/copied from TerrariaResourceJudge/out/resourcejudge-pgo/current/TerrariaSplit.WorldFilter.dll, with no older ABI fallback.

World pool signatures include the independent depth limit and the new resource-analysis generation policy, so previously banked worlds do not satisfy the new signature.
