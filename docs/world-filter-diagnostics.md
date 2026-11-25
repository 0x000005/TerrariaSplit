# 筛选诊断日志

诊断日志默认关闭，不再在 `Logs/` 下生成独立文件。启用后，普通日志与结构化筛选事件都写入程序目录的 `terrariasplit.log`。

退出正在运行的计时器，在发布目录打开 PowerShell 7 后运行：

```powershell
$env:TERRARIA_SPLIT_ENABLE_LOG = '1'
.\TerrariaSplit.exe
```

复现问题后退出计时器，提供 `terrariasplit.log` 和报错弹窗的复制详情。关闭日志时，先退出计时器，再移除变量并重新启动：

```powershell
Remove-Item Env:TERRARIA_SPLIT_ENABLE_LOG
.\TerrariaSplit.exe
```

记录包括：`process.start`（程序身份）、`candidate.start/end`（目标版本、实际条件、最终判定）、`dll.loaded`（路径、ABI、SHA256）、`native.queue/budget-acquired/enter/return`（排队与线程预算）、`native.response`（原始响应）、`native.*timeout/error/late-release`（超时、异常与晚完成）、`filter-loop.*`、`automation.step-*` 和 `workflow.message`。

用 `seedText`、`traceId`、`requestId` 和时间关联请求。ABI7 响应包含 `terrariaVersion`、`compatibilityId` 和 `generationCompatibilityId`，应与请求目标及当前发布指纹一致。同一 DLL 可以交替或并发判定两版，不能仅按种子号合并事件。

`hasNativeResult=false` 表示没有附带 DLL 结果，例如筛选未启用或调用前后出现异常。原生调用超时后，线程预算会保留到调用实际结束；完整的普通未命中判定不计入候选生成失败。日志记录本身不改变筛选、点击确认或重试行为。
