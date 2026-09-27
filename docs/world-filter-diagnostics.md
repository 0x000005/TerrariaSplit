# 筛选诊断日志

诊断日志默认开启，不依赖 TERRARIA_SPLIT_ENABLE_LOG。文件位于程序目录 Logs/world-filter-UTC日期时间-进程号.jsonl，每行一个 JSON 事件，逐条写入。单文件到20MiB后轮换至同名.previous，保留最近两份。

记录：process.start（程序身份）、candidate.start/end（实际条件及主程序最终判定）、managed-prescreen.start/end（DLL之前的旧金字塔预筛）、dll.loaded（路径/ABI/SHA256）、native.queue/budget-acquired/enter/return（排队、线程预算及调用）、native.response（原始响应）、native.*timeout/error/late-release（超时与晚完成）、filter-loop.*（批量策略、选中种子）、automation.step-* 和 workflow.message（点击、种子漂移及世界生成/文件扫描消息）。用 seedText、traceId、requestId 和时间关联。同种子重复请求按 traceId 区分；DLL实例的requestId可能重复，因此也应结合seedText与时间。

hasNativeResult表示最终判定携带DLL结果；false可能是旧预筛拒绝，也可能是DLL调用异常，请结合native事件判断。日志不改变原有筛选判定、不自动复试。真实UI流程由用户运行几分钟采集，不用自动生成测试代替。

使用独立Release诊断包时先关闭旧程序，确认筛选设置，操作几分钟后停止筛选。分析时提供对应jsonl，存在.previous时一并提供。
