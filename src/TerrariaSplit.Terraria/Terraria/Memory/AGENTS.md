# Memory 集成说明

## 职责
- 本目录负责 Terraria 进程内存读取、MemoryProbe 托管运行时布局解析、托管对象读取、世界生成状态读取和创图种子读取。
- 这里输出的是外部状态快照或诊断，不决定自动化流程是否继续。

## 设计原则
- 内存读取必须失败可恢复；读不到、读错形状或版本不匹配时返回 `Unknown`、空结果或诊断，不让应用崩溃。
- 缓存地址时必须考虑 Terraria 页面切换、进程重启和对象失效；发现指针失效后应重置并允许重新扫描。
- 对 UI state、managed string、按钮 contents 等结构做形状校验，避免把其他页面误判成创图页面。
- 不要在这里做点击、等待、设置读取或筛选决策；这些属于 `Automation/` 或上层应用编排。
- 除 UI scale patch 外，不新增 Terraria 版本专用字节签名；运行时布局优先来自 MemoryProbe/CLRMD，当前读取逻辑以 x86 Terraria 进程为准。

## 验证
- 修改运行时布局、偏移或创图种子读取逻辑后，运行相关 `TerrariaMemoryResolverTests`、`WorldGenerationMemoryTests` 和筛塔聚焦测试。
- 无法自动覆盖的页面切换或真实进程场景，在最终回复列出需要人工验证的界面状态。
