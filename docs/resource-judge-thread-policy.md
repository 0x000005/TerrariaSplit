# 原生世界筛选线程（2026-11-21）

同步最新 WorldGen 生成基线至 TerrariaResourceJudge，并更新主程序调用。ABI/JSON 协议保持6，筛选条件和固定判断时点保持原接口。

- 单世界：`max(1,min(6,floor(3×可用物理核心数/4)))`。例如4核用3线程、6核用4线程、8核及以上用6线程。
- 物理核心按 Windows CPU Sets 去重，并限制在当前进程亲和性和处理器组内；识别失败采用1线程。
- 多候选并发：每世界1线程，沿用原有总并发预算。
- 托管端将实际预留线程数传给 DLL，避免自动模式超出预留预算；超时后仍等原生调用结束才释放预算。

默认 DLL 来源为 `../TerrariaResourceJudge/out/resourcejudge-release/current/TerrariaSplit.WorldFilter.dll`，GCC15.1 x64 严格Release，无PGO/LTO。SHA256：`7a4531f162bc1299b6258567d10f975d2100966a8c6aa1cd58f3b9b81b28c4ad`。上游WorldGen提交：`2d7337b2145d724de630f50dbb5d8624746be51a`。

验证：Release全解决方案构建成功；72项Core测试、8项资源筛选聚焦测试、2项真实DLL资源筛选测试通过。覆盖物理核心/SMT去重、亲和性、预算回收、实际线程传递、批量单线程，以及三尺寸两邪恶和早拒终点。原始输出位于WorldGen/out/apply-stage13-baseline-20261121。
