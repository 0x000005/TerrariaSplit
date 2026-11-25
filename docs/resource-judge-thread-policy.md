# 原生世界筛选线程与版本（2026-11-25）

使用双版本 TerrariaResourceJudge，ABI/JSON 协议7。60字节输入在偏移56追加目标版本：1.4.4/1.4.5组传1458，1.4.6+组传1461；直接调用默认1461。筛选条件和判断时点保持原接口。

- 单世界：`max(1,min(6,floor(3×可用物理核心数/4)))`。例如4核用3线程、6核用4线程、8核及以上用6线程。
- 物理核心按 Windows CPU Sets 去重，并限制在当前进程亲和性和处理器组内；识别失败采用1线程。
- 多候选并发：每世界1线程，沿用原有总并发预算。
- 显式请求7/8仍最多执行6线程，并受可用物理核心数限制。
- 托管端将实际预留线程数传给 DLL，避免自动模式超出预留预算；超时后仍等原生调用结束才释放预算。

默认 DLL 来源为 `../TerrariaResourceJudge/out/resourcejudge-release/current/TerrariaSplit.WorldFilter.dll`，GCC15.1 x64 严格Release，无PGO/LTO或额外MinGW运行库。SHA256：`f7898ecdddb611cd91a0957ecc1e9fde8103c774e4dc0ed920eb5825a5ad6b7e`。发布记录为 `../TerrariaResourceJudge/out/resourcejudge-release/latest.json`，旧PGO入口不参与当前发布。

返回值必须匹配请求的目标版本、筛选兼容标识及WorldGen生成指纹。世界池签名同时包含实际游戏版本、目标生成指纹、尺寸、邪恶、模式、种子设置和筛选条件；ABI6池签名不再复用。前台按游戏版本选目标，后台及竞速筛选按实际TerrariaServer版本选目标。

DLL上游验证见[双版本报告](../../TerrariaResourceJudge/test/Reports/VersionSync20261125/README.md)。主程序测试覆盖布局、版本映射、协议拒绝、世界池隔离、线程预算及实际DLL的顺序切换和并发请求。
